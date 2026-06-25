using BookLoggerApp.Core.Entitlements;
using BookLoggerApp.Core.Infrastructure;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Core.ViewModels;
using BookLoggerApp.Infrastructure.Services;
using BookLoggerApp.Tests.TestHelpers;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace BookLoggerApp.Tests.Unit.ViewModels;

/// <summary>
/// SEC-15: shelf color commands now require <see cref="FeatureKey.CustomShelfColors"/> before
/// persisting, instead of relying only on the Settings.razor LockedFeatureButton overlay.
/// </summary>
public class SettingsViewModelEntitlementTests
{
    private readonly IImportExportService _importExport = Substitute.For<IImportExportService>();
    private readonly IAppSettingsProvider _settingsProvider = Substitute.For<IAppSettingsProvider>();
    private readonly IFileSaverService _fileSaver = Substitute.For<IFileSaverService>();
    private readonly IShareService _share = Substitute.For<IShareService>();
    private readonly IFilePickerService _filePicker = Substitute.For<IFilePickerService>();
    private readonly IMigrationService _migration = Substitute.For<IMigrationService>();
    private readonly INotificationService _notifications = Substitute.For<INotificationService>();
    private readonly IReadingTimerNotificationService _timerNotification = Substitute.For<IReadingTimerNotificationService>();
    private readonly IAppVersionService _appVersion = Substitute.For<IAppVersionService>();
    private readonly ILanguageService _language = Substitute.For<ILanguageService>();

    public SettingsViewModelEntitlementTests()
    {
        DatabaseInitializationHelper.MarkAsInitialized();
        _settingsProvider.GetSettingsAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(new AppSettings()));
        _migration.GetMigrationLog().Returns("");
        _appVersion.CurrentVersion.Returns("1.0.0");
        _language.CurrentLanguage.Returns("en");
        _language.SupportedLanguages.Returns(new[] { new SupportedLanguage("en", "English") });
    }

    private SettingsViewModel CreateVm(SubscriptionTier tier) => new(
        _importExport, _settingsProvider, _fileSaver, _share, _filePicker,
        _migration, _notifications, _timerNotification, _appVersion, _language,
        analytics: null,
        featureGuard: new FeatureGuard(new FakeEntitlementService(tier)));

    [Fact]
    public async Task UpdateShelfLedgeColorAsync_FreeUser_DoesNotPersist()
    {
        var vm = CreateVm(SubscriptionTier.Free);

        await vm.UpdateShelfLedgeColorAsync("#123456");

        await _settingsProvider.DidNotReceive().UpdateSettingsAsync(Arg.Any<AppSettings>(), Arg.Any<CancellationToken>());
        vm.ShelfLedgeColor.Should().NotBe("#123456");
        vm.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task UpdateShelfBaseColorAsync_FreeUser_DoesNotPersist()
    {
        var vm = CreateVm(SubscriptionTier.Free);

        await vm.UpdateShelfBaseColorAsync("#654321");

        await _settingsProvider.DidNotReceive().UpdateSettingsAsync(Arg.Any<AppSettings>(), Arg.Any<CancellationToken>());
        vm.ShelfBaseColor.Should().NotBe("#654321");
    }

    [Fact]
    public async Task UpdateShelfLedgeColorAsync_PlusUser_Persists()
    {
        var vm = CreateVm(SubscriptionTier.Plus);

        await vm.UpdateShelfLedgeColorAsync("#123456");

        vm.ShelfLedgeColor.Should().Be("#123456");
        await _settingsProvider.Received(1).UpdateSettingsAsync(Arg.Any<AppSettings>(), Arg.Any<CancellationToken>());
    }
}
