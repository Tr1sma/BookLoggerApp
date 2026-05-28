using System.Globalization;
using BookLoggerApp.Core.Enums;
using BookLoggerApp.Core.Resources;
using BookLoggerApp.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace BookLoggerApp.Tests.Unit.Resources;

public class WishlistPriorityLocalizationTests
{
    private readonly TestStringLocalizer<AppResources> _localizer = new();

    [Fact]
    public void LocalizedLabel_UsesGermanTranslation_WhenCurrentCultureIsGerman()
    {
        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        CultureInfo originalUiCulture = CultureInfo.CurrentUICulture;

        try
        {
            CultureInfo german = CultureInfo.GetCultureInfo("de");
            CultureInfo.CurrentCulture = german;
            CultureInfo.CurrentUICulture = german;

            WishlistPriority.High.LocalizedLabel(_localizer).Should().Be("Hoch");
            WishlistPriority.Medium.LocalizedLabel(_localizer).Should().Be("Mittel");
            WishlistPriority.Low.LocalizedLabel(_localizer).Should().Be("Niedrig");
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }
}
