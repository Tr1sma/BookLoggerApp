using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Core.ViewModels;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace BookLoggerApp.Tests.Unit.ViewModels;

public class StatsAnalysesViewModelTests
{
    private readonly IAdvancedStatsService _service;
    private readonly IStatsService _statsService;
    private readonly StatsAnalysesViewModel _viewModel;

    public StatsAnalysesViewModelTests()
    {
        BookLoggerApp.Core.Infrastructure.DatabaseInitializationHelper.MarkAsInitialized();
        _service = Substitute.For<IAdvancedStatsService>();
        _statsService = Substitute.For<IStatsService>();

        // Default returns
        _service.GetYearComparisonAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns((new YearStats(2025, 0, 0, 0, 0), new YearStats(2026, 0, 0, 0, 0)));
        _service.GetGenreRadarDataAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(new Dictionary<string, int>());
        _service.GetCompletionRateAsync(Arg.Any<CancellationToken>()).Returns((0, 0));
        _service.GetPageCountDistributionAsync(Arg.Any<CancellationToken>()).Returns(new Dictionary<string, int>());
        _service.GetTopAuthorsAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(new List<AuthorStats>());
        _statsService.GetActiveReadingPeriodsAsync(Arg.Any<CancellationToken>()).Returns(new List<(int, int)>());

        _viewModel = new StatsAnalysesViewModel(_service, _statsService);
    }

    [Fact]
    public async Task LoadAsync_PopulatesCompletionRate()
    {
        _service.GetCompletionRateAsync(Arg.Any<CancellationToken>()).Returns((24, 5));

        await _viewModel.LoadCommand.ExecuteAsync(null);

        _viewModel.CompletedCount.Should().Be(24);
        _viewModel.AbandonedCount.Should().Be(5);
        _viewModel.CompletionPercentage.Should().BeApproximately(82.8, 0.1);
    }

    [Fact]
    public async Task LoadAsync_PopulatesTopAuthors()
    {
        _service.GetTopAuthorsAsync(5, Arg.Any<CancellationToken>()).Returns(new List<AuthorStats>
        {
            new("Sanderson", 5, 4200),
            new("King", 3, 1800)
        });

        await _viewModel.LoadCommand.ExecuteAsync(null);

        _viewModel.TopAuthors.Should().HaveCount(2);
        _viewModel.TopAuthors[0].Author.Should().Be("Sanderson");
    }

    [Fact]
    public async Task ChangeComparisonYearsCommand_UpdatesYearComparison()
    {
        _service.GetYearComparisonAsync(2024, 2025, Arg.Any<CancellationToken>())
            .Returns((new YearStats(2024, 10, 3000, 5000, 4.0), new YearStats(2025, 12, 3400, 5200, 3.8)));

        await _viewModel.ChangeComparisonYearsCommand.ExecuteAsync((2024, 2025));

        _viewModel.Year1Stats.BooksCompleted.Should().Be(10);
        _viewModel.Year2Stats.BooksCompleted.Should().Be(12);
    }

    [Fact]
    public async Task LoadAsync_PopulatesAvailableYears()
    {
        _statsService.GetActiveReadingPeriodsAsync(Arg.Any<CancellationToken>()).Returns(new List<(int, int)>
        {
            (2025, 1), (2025, 3), (2026, 1), (2026, 2)
        });

        await _viewModel.LoadCommand.ExecuteAsync(null);

        _viewModel.AvailableYears.Should().Contain(2025);
        _viewModel.AvailableYears.Should().Contain(2026);
    }

    [Fact]
    public async Task LoadAsync_NoYears_DefaultsToCurrentAndPrevious()
    {
        await _viewModel.LoadCommand.ExecuteAsync(null);

        _viewModel.SelectedYear1.Should().Be(DateTime.UtcNow.Year - 1);
        _viewModel.SelectedYear2.Should().Be(DateTime.UtcNow.Year);
    }

    [Fact]
    public async Task LoadAsync_CompletionRateZeroBooks_ReturnsZeroPercent()
    {
        _service.GetCompletionRateAsync(Arg.Any<CancellationToken>()).Returns((0, 0));

        await _viewModel.LoadCommand.ExecuteAsync(null);

        _viewModel.CompletionPercentage.Should().Be(0);
    }
}
