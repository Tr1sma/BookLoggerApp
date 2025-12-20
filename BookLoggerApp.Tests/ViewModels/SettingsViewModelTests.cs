using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Core.ViewModels;
using FluentAssertions;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace BookLoggerApp.Tests.ViewModels;

public class SettingsViewModelTests
{
    private class MockImportExportService : IImportExportService
    {
        public Task<string> ExportToJsonAsync(CancellationToken ct = default) => Task.FromResult("{}");
        public Task ImportFromJsonAsync(string json, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAllDataAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private class MockAppSettingsProvider : IAppSettingsProvider
    {
        public AppSettings Settings { get; private set; } = new AppSettings();
        public event EventHandler? ProgressionChanged;

        public Task<AppSettings> GetSettingsAsync(CancellationToken ct = default)
        {
            return Task.FromResult(Settings);
        }

        public Task UpdateSettingsAsync(AppSettings settings, CancellationToken ct = default)
        {
            Settings = settings;
            return Task.CompletedTask;
        }

        public Task<int> GetUserCoinsAsync(CancellationToken ct = default) => Task.FromResult(0);
        public Task<int> GetUserLevelAsync(CancellationToken ct = default) => Task.FromResult(1);
        public Task SpendCoinsAsync(int amount, CancellationToken ct = default) => Task.CompletedTask;
        public Task AddCoinsAsync(int amount, CancellationToken ct = default) => Task.CompletedTask;
        public Task IncrementPlantsPurchasedAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task<int> GetPlantsPurchasedAsync(CancellationToken ct = default) => Task.FromResult(0);
        public void InvalidateCache() { }
    }

    [Fact]
    public async Task LoadAsync_ShouldLoadSettingsFromProvider()
    {
        // Arrange
        var importExportService = new MockImportExportService();
        var appSettingsProvider = new MockAppSettingsProvider();

        var expectedSettings = new AppSettings { Theme = "Dark", Language = "de" };
        await appSettingsProvider.UpdateSettingsAsync(expectedSettings);

        var viewModel = new SettingsViewModel(importExportService, appSettingsProvider);

        // Act
        await viewModel.LoadAsync();

        // Assert
        viewModel.Settings.Should().BeEquivalentTo(expectedSettings);
    }

    [Fact]
    public async Task SaveAsync_ShouldUpdateSettingsInProvider()
    {
        // Arrange
        var importExportService = new MockImportExportService();
        var appSettingsProvider = new MockAppSettingsProvider();
        var viewModel = new SettingsViewModel(importExportService, appSettingsProvider);

        await viewModel.LoadAsync();
        viewModel.Settings.Theme = "Dark";
        viewModel.Settings.Language = "fr";

        // Act
        await viewModel.SaveAsync();

        // Assert
        var savedSettings = await appSettingsProvider.GetSettingsAsync();
        savedSettings.Theme.Should().Be("Dark");
        savedSettings.Language.Should().Be("fr");
        savedSettings.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }
}
