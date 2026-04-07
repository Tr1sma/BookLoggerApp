using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Core.ViewModels;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace BookLoggerApp.Tests.Unit.ViewModels;

public class SettingsViewModelTests
{
    [Fact]
    public async Task LoadAsync_ShouldReadAppVersion_FromVersionService()
    {
        // Arrange
        var importExportService = Substitute.For<IImportExportService>();
        var settingsProvider = Substitute.For<IAppSettingsProvider>();
        var fileSaverService = Substitute.For<IFileSaverService>();
        var shareService = Substitute.For<IShareService>();
        var filePickerService = Substitute.For<IFilePickerService>();
        var migrationService = Substitute.For<IMigrationService>();
        var notificationService = Substitute.For<INotificationService>();
        var appVersionService = Substitute.For<IAppVersionService>();

        settingsProvider.GetSettingsAsync(Arg.Any<CancellationToken>()).Returns(new AppSettings());
        migrationService.GetMigrationLog().Returns(string.Empty);
        appVersionService.CurrentVersion.Returns("0.8.0");

        var viewModel = new SettingsViewModel(
            importExportService,
            settingsProvider,
            fileSaverService,
            shareService,
            filePickerService,
            migrationService,
            notificationService,
            appVersionService);

        // Act
        await viewModel.LoadAsync();

        // Assert
        viewModel.AppVersion.Should().Be("0.8.0");
    }
}
