using Microsoft.AspNetCore.WebUtilities;
using Microsoft.IdentityModel.Tokens;

using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;

namespace PANiXiDA.Core.Presentation.Http.UnitTests.Authentication;

internal sealed class IdentityServerHandler : HttpClientHandler
{
    internal const string Issuer = "https://identity.example.test/";
    internal const string Audience = "panixida-api";
    internal const string ClientSecret = "test-secret";
    internal static readonly Guid UserId = Guid.Parse("01992746-2c04-7f48-b5a0-c42487c72453");

    internal List<string> IntrospectedTokens { get; } = [];

    internal bool IsRevoked { get; set; }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (request.RequestUri?.AbsolutePath == "/.well-known/openid-configuration")
        {
            return Json(new
            {
                issuer = Issuer,
                jwks_uri = Issuer + ".well-known/jwks",
                introspection_endpoint = Issuer + "connect/introspect",
                introspection_endpoint_auth_methods_supported = new[] { "client_secret_post" }
            });
        }

        if (request.RequestUri?.AbsolutePath == "/.well-known/jwks")
        {
            using var rsa = RSA.Create(2048);
            var key = new RsaSecurityKey(rsa.ExportParameters(false)) { KeyId = "test-key" };
            return Json(new { keys = new[] { JsonWebKeyConverter.ConvertFromRSASecurityKey(key) } });
        }

        request.RequestUri.ShouldBe(new Uri(Issuer + "connect/introspect"));
        request.Method.ShouldBe(HttpMethod.Post);
        var form = QueryHelpers.ParseQuery(await request.Content!.ReadAsStringAsync(cancellationToken));
        form["client_id"].ToString().ShouldBe(Audience);
        form["client_secret"].ToString().ShouldBe(ClientSecret);
        var token = form["token"].ToString();
        IntrospectedTokens.Add(token);

        if (token == "unavailable")
        {
            return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        }

        if (IsRevoked || token == "invalid")
        {
            return Json(new { active = false });
        }

        var now = DateTimeOffset.UtcNow;
        return Json(new
        {
            active = true,
            iss = token == "wrong-issuer" ? "https://other.example.test/" : Issuer,
            aud = token == "wrong-audience" ? "another-api" : Audience,
            sub = token == "service" ? "background-worker" : UserId.ToString(),
            name = "Test User",
            role = new[] { "Worker", "Reviewer" },
            permission = new[] { "tasks.read", "tasks.comment" },
            scope = "tasks.read tasks.comment",
            client_id = "test-web",
            token_type = "Bearer",
            token_usage = token == "refresh" ? "refresh_token" : "access_token",
            iat = now.AddMinutes(-10).ToUnixTimeSeconds(),
            exp = (token == "expired" ? now.AddHours(-1) : now.AddMinutes(5)).ToUnixTimeSeconds()
        });
    }

    private static HttpResponseMessage Json(object content)
    {
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(content) };
    }
}
