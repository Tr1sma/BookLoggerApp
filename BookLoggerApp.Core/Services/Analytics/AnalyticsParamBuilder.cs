namespace BookLoggerApp.Core.Services.Analytics;

/// <summary>
/// Builds a parameter dictionary for analytics events while enforcing privacy guards.
/// In DEBUG, throws on non-allowlisted keys or suspiciously long string values (likely raw
/// content). In RELEASE, silently drops disallowed keys and truncates long strings as a
/// defense-in-depth.
/// </summary>
public sealed class AnalyticsParamBuilder
{
    // Firebase caps string param values at 100 chars; reuse that as the PII tripwire since any
    // longer value is certainly raw content (titles/notes/queries), not a short bucket label.
    private const int FirebaseMaxStringLength = 100;
    private const int PiiTripwireLength = FirebaseMaxStringLength;

    // OrdinalIgnoreCase so a mis-cased key compares the same way the Forbidden/Allowed sets do.
    private readonly Dictionary<string, object?> _params = new(StringComparer.OrdinalIgnoreCase);

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

    public IDictionary<string, object?> BuildMutable() =>
        new Dictionary<string, object?>(_params, StringComparer.OrdinalIgnoreCase);

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

        // Allowlist is the primary gate: any key not declared as a const in AnalyticsParamNames
        // is rejected, so a new raw-content key can't leak by default.
        if (!AnalyticsParamNames.Allowed.Contains(key))
        {
#if DEBUG
            throw new InvalidOperationException(
                $"Analytics param key '{key}' is not on the allowlist. Add a const to AnalyticsParamNames " +
                "(or use an existing bucketed key) instead of emitting an ad-hoc key.");
#else
            return false;
#endif
        }

        if (value is string s && s.Length > PiiTripwireLength)
        {
#if DEBUG
            throw new InvalidOperationException(
                $"Analytics param '{key}' has suspiciously long string value ({s.Length} chars, " +
                $"> {PiiTripwireLength}). Likely raw PII — bucket or truncate before logging.");
#else
            _params[key] = s.Substring(0, FirebaseMaxStringLength);
            return false;
#endif
        }

        return true;
    }
}
