using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Core.ViewModels;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace BookLoggerApp.Tests.Unit.ViewModels;

public class StatsTrendsViewModelTests
{
    private readonly IAdvancedStatsService _service;
    private readonly IStatsService _statsService;
    private readonly StatsTrendsViewModel _viewModel;

    public StatsTrendsViewModelTests()
    {
        BookLoggerApp.Core.Infrastructure.DatabaseInitializationHelper.MarkAsInitialized();
        _service = Substitute.For<IAdvancedStatsService>();
        _statsService = Substitute.For<IStatsService>();

        // Default returns
        _statsService.GetActiveReadingPeriodsAsync(Arg.Any<CancellationToken>()).Returns(new List<(int, int)>());
        _service.GetReadingHeatmapAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(new Dictionary<DateTime, int>());
        _service.GetWeekdayDistributionAsync(Arg.Any<CancellationToken>()).Returns(new Dictionary<DayOfWeek, int>());
        _service.GetTimeOfDayDistributionAsync(Arg.Any<CancellationToken>()).Returns(new Dictionary<string, int>
        {
            ["Morning"] = 0, ["Afternoon"] = 0, ["Evening"] = 0, ["Night"] = 0
        });
        _service.GetSessionLengthDistributionAsync(Arg.Any<CancellationToken>()).Returns(new Dictionary<string, int>());
        _service.GetMonthlyVolumeAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(new Dictionary<int, int>());
        _service.GetReadingSpeedTrendAsync(Arg.Any<CancellationToken>()).Returns((0.0, 0.0));
        _service.GetAverageFinishTimeTrendAsync(Arg.Any<CancellationToken>()).Returns((0.0, 0.0));

        _viewModel = new StatsTrendsViewModel(_service, _statsService);
    }

    [Fact]
    public async Task LoadAsync_PopulatesHeatmapData()
    {
        var today = DateTime.UtcNow.Date;
        _service.GetReadingHeatmapAsync(today.Year, Arg.Any<CancellationToken>())
            .Returns(new Dictionary<DateTime, int> { [today] = 45 });

        await _viewModel.LoadCommand.ExecuteAsync(null);

        _viewModel.HeatmapData.Should().ContainKey(today);
        _viewModel.HeatmapData[today].Should().Be(45);
    }

    [Fact]
    public async Task LoadAsync_PopulatesTimeOfDayLabel_Evening()
    {
        _service.GetTimeOfDayDistributionAsync(Arg.Any<CancellationToken>()).Returns(new Dictionary<string, int>
        {
            ["Morning"] = 10, ["Afternoon"] = 5, ["Evening"] = 80, ["Night"] = 30
        });

        await _viewModel.LoadCommand.ExecuteAsync(null);

        _viewModel.TimeOfDayLabel.Should().Be("Abendleser 🌙");
    }

    [Fact]
    public async Task LoadAsync_PopulatesTimeOfDayLabel_Night()
    {
        _service.GetTimeOfDayDistributionAsync(Arg.Any<CancellationToken>()).Returns(new Dictionary<string, int>
        {
            ["Morning"] = 0, ["Afternoon"] = 0, ["Evening"] = 20, ["Night"] = 100
        });

        await _viewModel.LoadCommand.ExecuteAsync(null);

        _viewModel.TimeOfDayLabel.Should().Be("Nachteule 🦉");
    }

    [Fact]
    public async Task LoadAsync_PopulatesReadingSpeed()
    {
        _service.GetReadingSpeedTrendAsync(Arg.Any<CancellationToken>()).Returns((32.0, 28.0));

        await _viewModel.LoadCommand.ExecuteAsync(null);

        _viewModel.CurrentSpeed.Should().Be(32);
        _viewModel.SpeedDifference.Should().Be(4);
    }

    [Fact]
    public async Task LoadAsync_PopulatesFinishTime()
    {
        _service.GetAverageFinishTimeTrendAsync(Arg.Any<CancellationToken>()).Returns((8.5, 9.7));

        await _viewModel.LoadCommand.ExecuteAsync(null);

        _viewModel.CurrentFinishDays.Should().Be(8.5);
        _viewModel.FinishDaysDifference.Should().Be(-1.2);
    }

    [Fact]
    public async Task ChangeHeatmapYearCommand_ReloadsHeatmapData()
    {
        _service.GetReadingHeatmapAsync(2025, Arg.Any<CancellationToken>())
            .Returns(new Dictionary<DateTime, int> { [new DateTime(2025, 6, 1)] = 30 });

        await _viewModel.ChangeHeatmapYearCommand.ExecuteAsync(2025);

        _viewModel.HeatmapYear.Should().Be(2025);
        _viewModel.HeatmapData.Should().ContainKey(new DateTime(2025, 6, 1));
    }

    [Fact]
    public async Task LoadAsync_AllZeroTimeOfDay_ReturnsEmptyLabel()
    {
        _service.GetTimeOfDayDistributionAsync(Arg.Any<CancellationToken>()).Returns(new Dictionary<string, int>
        {
            ["Morning"] = 0, ["Afternoon"] = 0, ["Evening"] = 0, ["Night"] = 0
        });

        await _viewModel.LoadCommand.ExecuteAsync(null);

        _viewModel.TimeOfDayLabel.Should().BeEmpty();
    }

    [Fact]
    public async Task LoadAsync_SetsMinYearFromActivePeriods()
    {
        _statsService.GetActiveReadingPeriodsAsync(Arg.Any<CancellationToken>()).Returns(new List<(int, int)>
        {
            (2024, 3), (2025, 1), (2026, 2)
        });

        await _viewModel.LoadCommand.ExecuteAsync(null);

        _viewModel.MinYear.Should().Be(2024);
        _viewModel.MaxYear.Should().Be(DateTime.UtcNow.Year);
    }

    [Fact]
    public async Task ChangeMonthlyVolumeYearCommand_ReloadsMonthlyData()
    {
        _service.GetMonthlyVolumeAsync(2025, Arg.Any<CancellationToken>())
            .Returns(new Dictionary<int, int> { [3] = 2, [6] = 1 });

        await _viewModel.ChangeMonthlyVolumeYearCommand.ExecuteAsync(2025);

        _viewModel.MonthlyVolumeYear.Should().Be(2025);
        _viewModel.MonthlyVolumeData[3].Should().Be(2);
    }
}
