using BookLoggerApp.Core.Infrastructure;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Core.ViewModels;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace BookLoggerApp.Tests.Unit.ViewModels;

public class SettingsViewModelTests
{
    private readonly IImportExportService _importExport;
    private readonly IAppSettingsProvider _settingsProvider;
    private readonly IFileSaverService _fileSaver;
    private readonly IShareService _share;
    private readonly IFilePickerService _filePicker;
    private readonly IMigrationService _migration;
    private readonly INotificationService _notifications;
    private readonly IAppVersionService _appVersion;
    private readonly ILanguageService _language;

    public SettingsViewModelTests()
    {
        DatabaseInitializationHelper.MarkAsInitialized();
        _importExport = Substitute.For<IImportExportService>();
        _settingsProvider = Substitute.For<IAppSettingsProvider>();
        _fileSaver = Substitute.For<IFileSaverService>();
        _share = Substitute.For<IShareService>();
        _filePicker = Substitute.For<IFilePickerService>();
        _migration = Substitute.For<IMigrationService>();
        _notifications = Substitute.For<INotificationService>();
        _appVersion = Substitute.For<IAppVersionService>();
        _language = Substitute.For<ILanguageService>();

        _settingsProvider.GetSettingsAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(new AppSettings()));
        _migration.GetMigrationLog().Returns("");
        _appVersion.CurrentVersion.Returns("1.0.0");
        _language.CurrentLanguage.Returns("en");
        _language.SupportedLanguages.Returns(new[]
        {
            new SupportedLanguage("en", "English"),
            new SupportedLanguage("de", "Deutsch"),
        });
    }

    private SettingsViewModel CreateVm() => new(
        _importExport, _settingsProvider, _fileSaver, _share, _filePicker,
        _migration, _notifications, _appVersion, _language);

    [Fact]
    public async Task LoadAsync_ReadsAppVersionFromService()
    {
        _appVersion.CurrentVersion.Returns("0.8.0");
        var vm = CreateVm();

        await vm.LoadAsync();

        vm.AppVersion.Should().Be("0.8.0");
    }

    [Fact]
    public async Task LoadAsync_PopulatesReminderTime()
    {
        _settingsProvider.GetSettingsAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(new AppSettings
        {
            ReminderTime = new TimeSpan(21, 30, 0)
        }));
        var vm = CreateVm();

        await vm.LoadAsync();

        vm.ReminderHour.Should().Be(21);
        vm.ReminderMinute.Should().Be(30);
    }

    [Fact]
    public async Task LoadAsync_PopulatesShelfColors()
    {
        _settingsProvider.GetSettingsAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(new AppSettings
        {
            ShelfLedgeColor = "#111111",
            ShelfBaseColor = "#222222"
        }));
        var vm = CreateVm();

        await vm.LoadAsync();

        vm.ShelfLedgeColor.Should().Be("#111111");
        vm.ShelfBaseColor.Should().Be("#222222");
    }

    [Fact]
    public async Task ToggleNotificationsAsync_Enabled_PermissionGranted_PersistsChange()
    {
        _notifications.RequestNotificationPermissionAsync().Returns(Task.FromResult(true));
        var vm = CreateVm();

        await vm.ToggleNotificationsCommand.ExecuteAsync(true);

        vm.Settings.NotificationsEnabled.Should().BeTrue();
        await _settingsProvider.Received(1).UpdateSettingsAsync(Arg.Any<AppSettings>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ToggleNotificationsAsync_Enabled_PermissionDenied_SetsError()
    {
        _notifications.RequestNotificationPermissionAsync().Returns(Task.FromResult(false));
        var vm = CreateVm();

        await vm.ToggleNotificationsCommand.ExecuteAsync(true);

        vm.Settings.NotificationsEnabled.Should().BeFalse();
        vm.ErrorMessage.Should().NotBeNull();
        vm.ErrorMessage!.Should().Contain("Notification permission");
    }

    [Fact]
    public async Task ToggleNotificationsAsync_Disabled_CancelsReminders()
    {
        var vm = CreateVm();
        vm.Settings.ReadingRemindersEnabled = true;

        await vm.ToggleNotificationsCommand.ExecuteAsync(false);

        vm.Settings.NotificationsEnabled.Should().BeFalse();
        vm.Settings.ReadingRemindersEnabled.Should().BeFalse();
        await _notifications.Received(1).CancelReadingReminderAsync();
    }

    [Fact]
    public async Task ToggleReadingRemindersAsync_Enabled_SchedulesReminder()
    {
        _notifications.RequestNotificationPermissionAsync().Returns(Task.FromResult(true));
        var vm = CreateVm();
        vm.ReminderHour = 19;
        vm.ReminderMinute = 45;

        await vm.ToggleReadingRemindersCommand.ExecuteAsync(true);

        vm.Settings.ReadingRemindersEnabled.Should().BeTrue();
        await _notifications.Received(1).ScheduleReadingReminderAsync(new TimeSpan(19, 45, 0));
    }

    [Fact]
    public async Task ToggleReadingRemindersAsync_PermissionDenied_SetsError()
    {
        _notifications.RequestNotificationPermissionAsync().Returns(Task.FromResult(false));
        var vm = CreateVm();

        await vm.ToggleReadingRemindersCommand.ExecuteAsync(true);

        vm.Settings.ReadingRemindersEnabled.Should().BeFalse();
        vm.ErrorMessage.Should().NotBeNull();
    }

    [Fact]
    public async Task ToggleReadingRemindersAsync_Disabled_CancelsReminder()
    {
        var vm = CreateVm();

        await vm.ToggleReadingRemindersCommand.ExecuteAsync(false);

        vm.Settings.ReadingRemindersEnabled.Should().BeFalse();
        await _notifications.Received(1).CancelReadingReminderAsync();
    }

    [Fact]
    public async Task UpdateReminderTimeAsync_WhenEnabled_ReschedulesReminder()
    {
        var vm = CreateVm();
        vm.Settings.ReadingRemindersEnabled = true;
        vm.ReminderHour = 7;
        vm.ReminderMinute = 15;

        await vm.UpdateReminderTimeCommand.ExecuteAsync(null);

        await _notifications.Received(1).ScheduleReadingReminderAsync(new TimeSpan(7, 15, 0));
    }

    [Fact]
    public async Task UpdateReminderTimeAsync_WhenDisabled_DoesNotSchedule()
    {
        var vm = CreateVm();
        vm.Settings.ReadingRemindersEnabled = false;

        await vm.UpdateReminderTimeCommand.ExecuteAsync(null);

        await _notifications.DidNotReceive().ScheduleReadingReminderAsync(Arg.Any<TimeSpan>());
    }

    [Fact]
    public async Task ToggleGettingStartedCtaAsync_PersistsSetting()
    {
        var vm = CreateVm();

        await vm.ToggleGettingStartedCtaCommand.ExecuteAsync(true);

        vm.Settings.HideGettingStartedCta.Should().BeTrue();
        await _settingsProvider.Received(1).UpdateSettingsAsync(Arg.Any<AppSettings>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateShelfLedgeColorAsync_UpdatesColor()
    {
        var vm = CreateVm();

        await vm.UpdateShelfLedgeColorCommand.ExecuteAsync("#ABCDEF");

        vm.ShelfLedgeColor.Should().Be("#ABCDEF");
        vm.Settings.ShelfLedgeColor.Should().Be("#ABCDEF");
    }

    [Fact]
    public async Task UpdateShelfBaseColorAsync_UpdatesColor()
    {
        var vm = CreateVm();

        await vm.UpdateShelfBaseColorCommand.ExecuteAsync("#123456");

        vm.ShelfBaseColor.Should().Be("#123456");
        vm.Settings.ShelfBaseColor.Should().Be("#123456");
    }

    [Fact]
    public async Task SaveAsync_CallsUpdateSettings()
    {
        var vm = CreateVm();

        await vm.SaveCommand.ExecuteAsync(null);

        await _settingsProvider.Received(1).UpdateSettingsAsync(Arg.Any<AppSettings>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExportDataAsync_SavesFileWithTimestamp()
    {
        _importExport.ExportToJsonAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult("{\"data\":1}"));
        var vm = CreateVm();

        await vm.ExportDataCommand.ExecuteAsync(null);

        await _importExport.Received(1).ExportToJsonAsync(Arg.Any<CancellationToken>());
        await _fileSaver.Received(1).SaveFileAsync(
            Arg.Is<string>(s => s.StartsWith("BookLoggerExport_") && s.EndsWith(".json")),
            "{\"data\":1}");
    }

    [Fact]
    public async Task ImportDataAsync_CallsImportService()
    {
        var vm = CreateVm();

        await vm.ImportDataCommand.ExecuteAsync("some json");

        await _importExport.Received(1).ImportFromJsonAsync("some json", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAllDataAsync_CallsService()
    {
        var vm = CreateVm();

        await vm.DeleteAllDataCommand.ExecuteAsync(null);

        await _importExport.Received(1).DeleteAllDataAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BackupToCloudAsync_CreatesAndSharesBackup()
    {
        _importExport.CreateBackupAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult("/tmp/backup.zip"));
        var vm = CreateVm();

        await vm.BackupToCloudCommand.ExecuteAsync(null);

        await _importExport.Received(1).CreateBackupAsync(Arg.Any<CancellationToken>());
        await _share.Received(1).ShareFileAsync("BookLogger Backup", "/tmp/backup.zip", "application/zip");
    }

    [Fact]
    public async Task RestoreFromBackupAsync_NullPath_SetsError()
    {
        _filePicker.PickFileAsync(Arg.Any<string>(), Arg.Any<string>()).Returns(Task.FromResult<string?>(null));
        var vm = CreateVm();

        await vm.RestoreFromBackupCommand.ExecuteAsync(null);

        vm.ErrorMessage.Should().NotBeNull();
        vm.BackupRestoreSucceeded.Should().BeFalse();
    }

    [Fact]
    public async Task RestoreFromBackupAsync_ValidPath_CallsRestoreAndSetsFlag()
    {
        _filePicker.PickFileAsync(Arg.Any<string>(), Arg.Any<string>()).Returns(Task.FromResult<string?>("/tmp/b.zip"));
        var vm = CreateVm();

        await vm.RestoreFromBackupCommand.ExecuteAsync(null);

        await _importExport.Received(1).RestoreFromBackupAsync("/tmp/b.zip", Arg.Any<IProgress<string>>(), Arg.Any<CancellationToken>());
        vm.BackupRestoreSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task RestoreFromBackupAsync_ImportThrows_SetsError()
    {
        _filePicker.PickFileAsync(Arg.Any<string>(), Arg.Any<string>()).Returns(Task.FromResult<string?>("/tmp/b.zip"));
        _importExport.RestoreFromBackupAsync(Arg.Any<string>(), Arg.Any<IProgress<string>>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new InvalidOperationException("corrupt zip"));
        var vm = CreateVm();

        await vm.RestoreFromBackupCommand.ExecuteAsync(null);

        vm.BackupRestoreSucceeded.Should().BeFalse();
        vm.ErrorMessage.Should().NotBeNull();
        vm.ErrorMessage!.Should().Contain("corrupt zip");
    }
}
