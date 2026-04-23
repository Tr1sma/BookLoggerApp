using System.Globalization;
using BookLoggerApp.Core.Resources;
using BookLoggerApp.Core.ViewModels;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace BookLoggerApp.Tests.TestHelpers;

/// <summary>
/// Real resx-backed <see cref="IStringLocalizer{TResource}"/> for tests: loads the
/// actual neutral (English) values from <c>AppResources.resx</c> via the
/// Microsoft.Extensions.Localization infrastructure. Missing keys fall back to the
/// key name itself so tests never crash on a typo.
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
        // ResourcesPath is deliberately empty: the marker type AppResources already
        // lives in the BookLoggerApp.Core.Resources namespace, and the factory forms
        // the base name from the type's namespace. Setting ResourcesPath = "Resources"
        // would double it (BookLoggerApp.Core.Resources.Resources.AppResources).
        IOptions<LocalizationOptions> options = Options.Create(new LocalizationOptions());
        return new ResourceManagerStringLocalizerFactory(options, NullLoggerFactory.Instance);
    }
}

/// <summary>
/// Assembly-wide test initialization: wires the real resx-backed localizer onto
/// <see cref="ViewModelBase.Localizer"/> so that tests asserting on the English
/// fallback text (e.g. "Failed to load books") keep working without touching every
/// test fixture.
/// </summary>
internal static class TestLocalizationInitializer
{
    [System.Runtime.CompilerServices.ModuleInitializer]
    public static void Initialize()
    {
        // Pin tests to the invariant (English) culture regardless of the dev machine's
        // Windows language, so assertions like .Contain("Failed to load books") are
        // stable. Tests that exercise the German path can override this locally.
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;

        ViewModelBase.Localizer ??= new TestStringLocalizer<AppResources>();
    }
}
