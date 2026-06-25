using System.Globalization;
using BookLoggerApp.Core.Resources;
using BookLoggerApp.Core.ViewModels;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace BookLoggerApp.Tests.TestHelpers;

/// <summary>
/// Real resx-backed <see cref="IStringLocalizer{TResource}"/> for tests, loading neutral (English)
/// values from <c>AppResources.resx</c>. Missing keys fall back to the key name.
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
        // ResourcesPath deliberately empty: AppResources already lives in ...Core.Resources and the
        // factory derives the base name from the namespace; setting it would double the path.
        IOptions<LocalizationOptions> options = Options.Create(new LocalizationOptions());
        return new ResourceManagerStringLocalizerFactory(options, NullLoggerFactory.Instance);
    }
}

/// <summary>
/// Assembly-wide test init: wires the real localizer onto <see cref="ViewModelBase.Localizer"/>
/// so tests asserting on English fallback text work without touching every fixture.
/// </summary>
internal static class TestLocalizationInitializer
{
    [System.Runtime.CompilerServices.ModuleInitializer]
    public static void Initialize()
    {
        // Pin tests to invariant (English) culture regardless of dev machine language for stable
        // assertions. Tests exercising the German path override this locally.
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;

        ViewModelBase.Localizer ??= new TestStringLocalizer<AppResources>();
    }
}
