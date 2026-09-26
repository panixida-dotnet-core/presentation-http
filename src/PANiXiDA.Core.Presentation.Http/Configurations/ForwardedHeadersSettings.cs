using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;

using System.Net;

namespace PANiXiDA.Core.Presentation.Http.Configurations;

internal sealed class ForwardedHeadersSettings
{
    public string ForwardedForHeaderName { get; set; } = string.Empty;
    public string ForwardedHostHeaderName { get; set; } = string.Empty;
    public string ForwardedProtoHeaderName { get; set; } = string.Empty;
    public string ForwardedPrefixHeaderName { get; set; } = string.Empty;
    public string OriginalForHeaderName { get; set; } = string.Empty;
    public string OriginalHostHeaderName { get; set; } = string.Empty;
    public string OriginalProtoHeaderName { get; set; } = string.Empty;
    public string OriginalPrefixHeaderName { get; set; } = string.Empty;
    public ForwardedHeaders ForwardedHeaders { get; set; }
    public int? ForwardLimit { get; set; }
    public bool RequireHeaderSymmetry { get; set; }
    public List<string> AllowedHosts { get; set; } = [];
    public List<string> KnownProxies { get; set; } = [];
    public List<NetworkSettings> KnownIPNetworks { get; set; } = [];
    public List<NetworkSettings> KnownNetworks { get; set; } = [];

    internal static ForwardedHeadersSettings FromOptions(ForwardedHeadersOptions options)
    {
        return new ForwardedHeadersSettings
        {
            ForwardedForHeaderName = options.ForwardedForHeaderName,
            ForwardedHostHeaderName = options.ForwardedHostHeaderName,
            ForwardedProtoHeaderName = options.ForwardedProtoHeaderName,
            ForwardedPrefixHeaderName = options.ForwardedPrefixHeaderName,
            OriginalForHeaderName = options.OriginalForHeaderName,
            OriginalHostHeaderName = options.OriginalHostHeaderName,
            OriginalProtoHeaderName = options.OriginalProtoHeaderName,
            OriginalPrefixHeaderName = options.OriginalPrefixHeaderName,
            ForwardedHeaders = options.ForwardedHeaders,
            ForwardLimit = options.ForwardLimit,
            RequireHeaderSymmetry = options.RequireHeaderSymmetry,
            AllowedHosts = [.. options.AllowedHosts]
        };
    }

    internal void Apply(ForwardedHeadersOptions options)
    {
        var proxies = KnownProxies
            .Select(IPAddress.Parse)
            .ToArray();
        var networks = KnownIPNetworks
            .Concat(KnownNetworks)
            .Select(network => new System.Net.IPNetwork(IPAddress.Parse(network.Prefix), network.PrefixLength))
            .ToArray();

        options.ForwardedForHeaderName = ForwardedForHeaderName;
        options.ForwardedHostHeaderName = ForwardedHostHeaderName;
        options.ForwardedProtoHeaderName = ForwardedProtoHeaderName;
        options.ForwardedPrefixHeaderName = ForwardedPrefixHeaderName;
        options.OriginalForHeaderName = OriginalForHeaderName;
        options.OriginalHostHeaderName = OriginalHostHeaderName;
        options.OriginalProtoHeaderName = OriginalProtoHeaderName;
        options.OriginalPrefixHeaderName = OriginalPrefixHeaderName;
        options.ForwardedHeaders = ForwardedHeaders;
        options.ForwardLimit = ForwardLimit;
        options.RequireHeaderSymmetry = RequireHeaderSymmetry;
        options.AllowedHosts.Clear();
        foreach (var host in AllowedHosts)
        {
            options.AllowedHosts.Add(host);
        }

        options.KnownProxies.Clear();
        foreach (var proxy in proxies)
        {
            options.KnownProxies.Add(proxy);
        }

        options.KnownIPNetworks.Clear();
        foreach (var network in networks)
        {
            options.KnownIPNetworks.Add(network);
        }
    }

    internal sealed class NetworkSettings
    {
        public string Prefix { get; set; } = string.Empty;
        public int PrefixLength { get; set; }
    }
}
