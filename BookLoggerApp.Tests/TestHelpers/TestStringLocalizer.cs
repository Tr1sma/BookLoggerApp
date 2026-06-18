using System.Globalization;
using BookLoggerApp.Core.Resources;
using BookLoggerApp.Core.ViewModels;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace BookLoggerApp.Tests.TestHelpers;

/// <summary>
/// Resx-backed localizer for tests. Loads English values from AppResources.resx;
/// missing keys fall back to the key name so tests never crash on a typo.
/// </summary>
public sealed class TestStringLocalizer<TResource> : IStringLocalizer<TResource>
{
    private static readonly IStringLocalizerFactory _factory = BuildFactory();

    private readonly IStringLocalizer _inner = _factory.Create(typeof(TResource));

    public LocalizedString this[string name]
    {
        get
        {
            LocalizedString value = _inner[name];
            return value.ResourceNotFound
                ? new LocalizedString(name, name, resourceNotFound: false)
                : value;
        }
    }

    public LocalizedString this[string name, params object[] arguments]
    {
        get
        {
            LocalizedString value = _inner[name, arguments];
            return value.ResourceNotFound
                ? new LocalizedString(name, string.Format(name, arguments), resourceNotFound: false)
                : value;
        }
    }

    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
        => _inner.GetAllStrings(includeParentCultures);

    private static IStringLocalizerFactory BuildFactory()
    {
        // ResourcesPath must be empty: AppResources already lives in
        // BookLoggerApp.Core.Resources, so setting "Resources" would double it.
        IOptions<LocalizationOptions> options = Options.Create(new LocalizationOptions());
        return new ResourceManagerStringLocalizerFactory(options, NullLoggerFactory.Instance);
    }
}

/// <summary>
/// Wires the resx localizer onto ViewModelBase.Localizer and pins culture to
/// InvariantCulture so string assertions are stable on non-English dev machines.
/// </summary>
internal static class TestLocalizationInitializer
{
    [System.Runtime.CompilerServices.ModuleInitializer]
    public static void Initialize()
    {
        // Invariant culture prevents German dev machines from producing
        // "Datenbank wird noch vorbereitet..." instead of English fallback text.
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;

        ViewModelBase.Localizer ??= new TestStringLocalizer<AppResources>();
    }
}
