namespace PANiXiDA.Core.Presentation.Http.UnitTests.Endpoints;

internal static class EndpointMappingRecorder
{
    private static readonly AsyncLocal<List<string>?> entries = new();

    internal static IReadOnlyList<string> Entries
    {
        get
        {
            return entries.Value ?? [];
        }
    }

    internal static void Add(string entry)
    {
        entries.Value ??= [];
        entries.Value.Add(entry);
    }

    internal static void Clear()
    {
        entries.Value = [];
    }
}
