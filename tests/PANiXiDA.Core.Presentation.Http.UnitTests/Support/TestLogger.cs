using Microsoft.Extensions.Logging;

namespace PANiXiDA.Core.Presentation.Http.UnitTests.Support;

internal sealed class TestLogger<T> : ILogger<T>
{
    private readonly List<LogEntry> entries = [];
    private readonly List<object?> scopes = [];

    public IReadOnlyList<LogEntry> Entries
    {
        get
        {
            return entries;
        }
    }

    public IReadOnlyList<object?> Scopes
    {
        get
        {
            return scopes;
        }
    }

    public IDisposable BeginScope<TState>(TState state)
        where TState : notnull
    {
        scopes.Add(state);

        return new TestScope();
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return true;
    }

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        entries.Add(new LogEntry(logLevel, eventId, formatter(state, exception), exception, state));
    }

    internal sealed record LogEntry(
        LogLevel LogLevel,
        EventId EventId,
        string Message,
        Exception? Exception,
        object? State);

    private sealed class TestScope : IDisposable
    {
        public void Dispose()
        {
        }
    }
}
