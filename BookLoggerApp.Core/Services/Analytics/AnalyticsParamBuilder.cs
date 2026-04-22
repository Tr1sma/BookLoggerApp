namespace BookLoggerApp.Core.Services.Analytics;

/// <summary>
/// Builds a parameter dictionary for analytics events while enforcing privacy guards.
/// In DEBUG, throws on forbidden keys or suspiciously long string values (likely raw content).
/// In RELEASE, silently drops forbidden keys and truncates long strings as a defense-in-depth.
/// </summary>
public sealed class AnalyticsParamBuilder
{
    private const int MaxStringLength = 32;

    private readonly Dictionary<string, object?> _params = new(StringComparer.Ordinal);

    public static AnalyticsParamBuilder Create() => new();

    public AnalyticsParamBuilder Add(string key, string? value)
    {
        if (!Guard(key, value)) return this;
        _params[key] = value;
        return this;
    }

    public AnalyticsParamBuilder Add(string key, bool value)
    {
        if (!Guard(key, value)) return this;
        _params[key] = value;
        return this;
    }

    public AnalyticsParamBuilder Add(string key, int value)
    {
        if (!Guard(key, value)) return this;
        _params[key] = value;
        return this;
    }

    public AnalyticsParamBuilder Add(string key, long value)
    {
        if (!Guard(key, value)) return this;
        _params[key] = value;
        return this;
    }

    public AnalyticsParamBuilder Add(string key, double value)
    {
        if (!Guard(key, value)) return this;
        _params[key] = value;
        return this;
    }

    public IReadOnlyDictionary<string, object?> Build() => _params;

    public IDictionary<string, object?> BuildMutable() => new Dictionary<string, object?>(_params);

    private bool Guard(string key, object? value)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
#if DEBUG
            throw new ArgumentException("Analytics parameter key must not be empty.", nameof(key));
#else
            return false;
#endif
        }

        if (AnalyticsParamNames.Forbidden.Contains(key))
        {
#if DEBUG
            throw new InvalidOperationException($"Analytics param key '{key}' is on the PII forbidden list.");
#else
            return false;
#endif
        }

        if (value is string s && s.Length > MaxStringLength)
        {
#if DEBUG
            throw new InvalidOperationException(
                $"Analytics param '{key}' has suspiciously long string value ({s.Length} chars). " +
                "Likely raw PII — bucket or truncate before logging.");
#else
            _params[key] = s.Substring(0, MaxStringLength);
            return false;
#endif
        }

        return true;
    }
}
