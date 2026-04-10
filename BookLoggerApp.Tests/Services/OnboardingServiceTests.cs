using BookLoggerApp.Core.Models;
using BookLoggerApp.Infrastructure.Services;
using BookLoggerApp.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookLoggerApp.Tests.Services;

public class OnboardingServiceTests
{
    [Fact]
    public async Task GetSnapshotAsync_ShouldShowIntro_ForFreshUser()
    {
        var databaseName = Guid.NewGuid().ToString();
        var service = CreateService(databaseName);

        var snapshot = await service.GetSnapshotAsync();

        snapshot.ShouldShowIntro.Should().BeTrue();
        snapshot.CurrentIntroStep.Should().Be(0);
        snapshot.IntroStatus.Should().Be(OnboardingIntroStatus.NotStarted);
        snapshot.NextRecommendedMission?.Id.Should().Be(OnboardingMissionId.AddFirstBook);
    }

    [Fact]
    public async Task GetSnapshotAsync_ShouldAutoCompleteIntro_ForExistingUsersUpgradingToVersionedFlow()
    {
        var databaseName = Guid.NewGuid().ToString();

        await using (var context = TestDbContext.Create(databaseName))
        {
            var settings = await context.AppSettings.FirstAsync();
            settings.OnboardingFlowVersion = 0;
            settings.OnboardingIntroStatus = OnboardingIntroStatus.NotStarted;
            settings.HasCompletedOnboarding = false;

            context.Books.Add(new Book
            {
                Title = "Dune",
                Author = "Frank Herbert",
                DateAdded = DateTime.UtcNow
            });

            await context.SaveChangesAsync();
        }

        var service = CreateService(databaseName);
        var snapshot = await service.GetSnapshotAsync();

        snapshot.ShouldShowIntro.Should().BeFalse();
        snapshot.IntroStatus.Should().Be(OnboardingIntroStatus.Completed);
        snapshot.Missions.Single(m => m.Id == OnboardingMissionId.AddFirstBook).IsCompleted.Should().BeTrue();

        await using var verificationContext = TestDbContext.Create(databaseName);
        var updatedSettings = await verificationContext.AppSettings.FirstAsync();
        updatedSettings.HasCompletedOnboarding.Should().BeTrue();
        updatedSettings.OnboardingAutoCompletedForExistingUser.Should().BeTrue();
    }

    [Fact]
    public async Task TrackEventAsync_ShouldPrepareTutorialPlantForImmediateWatering_WhenPlantIsPlaced()
    {
        var databaseName = Guid.NewGuid().ToString();
        Guid plantId;

        await using (var context = TestDbContext.Create(databaseName))
        {
            var species = await context.PlantSpecies.FirstAsync();
            var shelf = new Shelf
            {
                Name = "Main Shelf",
                SortOrder = 0
            };

            var plant = new UserPlant
            {
                SpeciesId = species.Id,
                Name = "Tutorial Plant",
                PlantedAt = DateTime.UtcNow,
                LastWatered = DateTime.UtcNow,
                Status = Core.Enums.PlantStatus.Healthy
            };

            context.Shelves.Add(shelf);
            context.UserPlants.Add(plant);
            await context.SaveChangesAsync();

            context.PlantShelves.Add(new PlantShelf
            {
                PlantId = plant.Id,
                ShelfId = shelf.Id,
                Position = 0
            });

            await context.SaveChangesAsync();
            plantId = plant.Id;
        }

        var service = CreateService(databaseName);
        await service.CompleteIntroAsync(skipped: false);

        var snapshot = await service.TrackEventAsync(OnboardingEvent.PlantPlacedOnShelf, plantId);

        snapshot.Missions.Single(m => m.Id == OnboardingMissionId.PlaceFirstPlantOnShelf).IsCompleted.Should().BeTrue();
        snapshot.Missions.Single(m => m.Id == OnboardingMissionId.WaterFirstPlant).Status.Should().Be(OnboardingMissionStatus.Available);

        await using var verificationContext = TestDbContext.Create(databaseName);
        var updatedPlant = await verificationContext.UserPlants
            .Include(p => p.Species)
            .FirstAsync(p => p.Id == plantId);
        var updatedSettings = await verificationContext.AppSettings.FirstAsync();

        updatedPlant.LastWatered.Should().BeOnOrBefore(DateTime.UtcNow.AddDays(-updatedPlant.Species.WaterIntervalDays));
        updatedSettings.OnboardingTutorialPlantId.Should().Be(plantId);
        updatedSettings.OnboardingTutorialPlantNeedsWateringAssist.Should().BeTrue();
    }

    [Fact]
    public async Task ResetIntroAsync_ShouldPreserveCompletedMissions()
    {
        var databaseName = Guid.NewGuid().ToString();
        var service = CreateService(databaseName);

        await service.TrackEventAsync(OnboardingEvent.IsbnScanned);
        var snapshot = await service.ResetIntroAsync();

        snapshot.ShouldShowIntro.Should().BeTrue();
        snapshot.Missions.Single(m => m.Id == OnboardingMissionId.ScanIsbn).IsCompleted.Should().BeTrue();
    }

    private static OnboardingService CreateService(string databaseName)
    {
        var contextFactory = new TestDbContextFactory(databaseName);
        var settingsProvider = new AppSettingsProvider(contextFactory);
        return new OnboardingService(contextFactory, settingsProvider, NullLogger<OnboardingService>.Instance);
    }
}
