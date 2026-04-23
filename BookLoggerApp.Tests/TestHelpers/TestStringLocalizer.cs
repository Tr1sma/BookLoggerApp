using Microsoft.Extensions.Localization;

namespace BookLoggerApp.Tests.TestHelpers;

/// <summary>
/// Minimal <see cref="IStringLocalizer{TResource}"/> implementation for tests: returns
/// the key itself as the value (and treats it as "found"), so production code can be
/// exercised without loading real resource assemblies.
/// </summary>
public sealed class TestStringLocalizer<TResource> : IStringLocalizer<TResource>
{
    public LocalizedString this[string name] => new(name, name, resourceNotFound: false);

    public LocalizedString this[string name, params object[] arguments]
        => new(name, string.Format(name, arguments), resourceNotFound: false);

    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
        => Enumerable.Empty<LocalizedString>();
}
