namespace BookLoggerApp.Core.Services.Analytics;

/// <summary>
/// Builds a parameter dictionary for analytics events while enforcing privacy guards.
/// In DEBUG, throws on non-allowlisted keys or suspiciously long string values (likely raw
/// content). In RELEASE, silently drops disallowed keys and truncates long strings as a
/// defense-in-depth.
/// </summary>
public sealed class AnalyticsParamBuilder
{
    // Z.508: separate the two length concerns. Firebase caps a string param value at 100 chars,
    // so that is BOTH the storage-truncation length and the PII tripwire — a value longer than the
    // Firebase cap is certainly raw content (titles/notes/queries), never a short bucket/category
    // label. Previously a single 32-char constant did double duty: throwing in DEBUG but silently
    // truncating to 32 in RELEASE for the very same value.
    private const int FirebaseMaxStringLength = 100;
    private const int PiiTripwireLength = FirebaseMaxStringLength;

    // OrdinalIgnoreCase so a mis-cased key dedupes/compares the same way the Forbidden/Allowed
    // sets do (Z.501) — previously the dictionary was Ordinal while the guards were IgnoreCase.
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

        // Z.501: allowlist is the primary gate — anything not declared as a const in
        // AnalyticsParamNames is rejected, so a new raw-content key can't leak by default.
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
