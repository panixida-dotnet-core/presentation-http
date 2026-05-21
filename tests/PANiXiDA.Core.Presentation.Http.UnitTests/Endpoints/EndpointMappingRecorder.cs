namespace PANiXiDA.Core.Presentation.Http.UnitTests.Endpoints;

internal static class EndpointMappingRecorder
{
    private static readonly List<string> entries = [];

    internal static IReadOnlyList<string> Entries
    {
        get
        {
            return entries;
        }
    }

    internal static void Add(string entry)
    {
        entries.Add(entry);
    }

    internal static void Clear()
    {
        entries.Clear();
    }
}
