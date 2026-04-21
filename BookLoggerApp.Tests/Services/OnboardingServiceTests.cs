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

    // ─────────────────────────────────────────────────────────────────────────
    // Coverage-ergänzende Tests (Advance, Retreat, Complete, ResetAll,
    // TrackEvent varieties, PlantWatered flow)
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AdvanceIntroAsync_IncrementsStepAndMarksInProgress()
    {
        var service = CreateService(Guid.NewGuid().ToString());

        var after = await service.AdvanceIntroAsync();

        after.CurrentIntroStep.Should().Be(1);
        after.IntroStatus.Should().Be(OnboardingIntroStatus.InProgress);
    }

    [Fact]
    public async Task AdvanceIntroAsync_AlreadyCompleted_IsNoOp()
    {
        var service = CreateService(Guid.NewGuid().ToString());
        await service.CompleteIntroAsync(skipped: false);

        var after = await service.AdvanceIntroAsync();

        after.IntroStatus.Should().Be(OnboardingIntroStatus.Completed);
    }

    [Fact]
    public async Task AdvanceIntroAsync_ClampsAtLastStep()
    {
        var service = CreateService(Guid.NewGuid().ToString());

        // Advance far enough to hit the clamp
        var snapshot = await service.GetSnapshotAsync();
        var steps = snapshot.IntroStepCount;
        for (int i = 0; i < steps + 3; i++)
        {
            snapshot = await service.AdvanceIntroAsync();
        }

        snapshot.CurrentIntroStep.Should().Be(steps - 1);
    }

    [Fact]
    public async Task RetreatIntroAsync_FromStep0_StaysAt0()
    {
        var service = CreateService(Guid.NewGuid().ToString());

        var after = await service.RetreatIntroAsync();

        after.CurrentIntroStep.Should().Be(0);
        after.IntroStatus.Should().Be(OnboardingIntroStatus.NotStarted);
    }

    [Fact]
    public async Task RetreatIntroAsync_FromStep2_DecrementsStep()
    {
        var service = CreateService(Guid.NewGuid().ToString());
        await service.AdvanceIntroAsync();
        await service.AdvanceIntroAsync();

        var after = await service.RetreatIntroAsync();

        after.CurrentIntroStep.Should().Be(1);
        after.IntroStatus.Should().Be(OnboardingIntroStatus.InProgress);
    }

    [Fact]
    public async Task RetreatIntroAsync_AlreadyCompleted_IsNoOp()
    {
        var service = CreateService(Guid.NewGuid().ToString());
        await service.CompleteIntroAsync(skipped: false);

        var after = await service.RetreatIntroAsync();

        after.IntroStatus.Should().Be(OnboardingIntroStatus.Completed);
    }

    [Fact]
    public async Task CompleteIntroAsync_Skipped_SetsSkippedStatus()
    {
        var service = CreateService(Guid.NewGuid().ToString());

        var after = await service.CompleteIntroAsync(skipped: true);

        after.IntroStatus.Should().Be(OnboardingIntroStatus.Skipped);
        after.ShouldShowIntro.Should().BeFalse();
    }

    [Fact]
    public async Task CompleteIntroAsync_NotSkipped_SetsCompletedStatus()
    {
        var service = CreateService(Guid.NewGuid().ToString());

        var after = await service.CompleteIntroAsync(skipped: false);

        after.IntroStatus.Should().Be(OnboardingIntroStatus.Completed);
    }

    [Fact]
    public async Task ResetAllAsync_ClearsAllMissionsAndResetsIntro()
    {
        var service = CreateService(Guid.NewGuid().ToString());
        await service.TrackEventAsync(OnboardingEvent.IsbnScanned);
        await service.CompleteIntroAsync(skipped: false);

        var after = await service.ResetAllAsync();

        after.IntroStatus.Should().Be(OnboardingIntroStatus.NotStarted);
        after.ShouldShowIntro.Should().BeTrue();
        after.Missions.Single(m => m.Id == OnboardingMissionId.ScanIsbn).Status
            .Should().NotBe(OnboardingMissionStatus.Completed);
    }

    [Fact]
    public async Task TrackEventAsync_IntroCompleted_MarksIntroCompleted()
    {
        var service = CreateService(Guid.NewGuid().ToString());

        var after = await service.TrackEventAsync(OnboardingEvent.IntroCompleted);

        after.IntroStatus.Should().Be(OnboardingIntroStatus.Completed);
    }

    [Fact]
    public async Task TrackEventAsync_IntroSkipped_MarksIntroSkipped()
    {
        var service = CreateService(Guid.NewGuid().ToString());

        var after = await service.TrackEventAsync(OnboardingEvent.IntroSkipped);

        after.IntroStatus.Should().Be(OnboardingIntroStatus.Skipped);
    }

    [Fact]
    public async Task TrackEventAsync_BookCreated_CompletesMission()
    {
        var service = CreateService(Guid.NewGuid().ToString());
        await service.CompleteIntroAsync(skipped: true);

        var after = await service.TrackEventAsync(OnboardingEvent.BookCreated);

        after.Missions.Single(m => m.Id == OnboardingMissionId.AddFirstBook).Status
            .Should().Be(OnboardingMissionStatus.Completed);
    }

    [Fact]
    public async Task TrackEventAsync_GoalCreated_CompletesMission()
    {
        var service = CreateService(Guid.NewGuid().ToString());
        await service.CompleteIntroAsync(skipped: true);

        var after = await service.TrackEventAsync(OnboardingEvent.GoalCreated);

        after.Missions.Single(m => m.Id == OnboardingMissionId.CreateFirstGoal).Status
            .Should().Be(OnboardingMissionStatus.Completed);
    }

    [Fact]
    public async Task TrackEventAsync_BackupCreated_CompletesMission()
    {
        var service = CreateService(Guid.NewGuid().ToString());
        await service.CompleteIntroAsync(skipped: true);

        var after = await service.TrackEventAsync(OnboardingEvent.BackupCreated);

        after.Missions.Single(m => m.Id == OnboardingMissionId.CreateBackup).Status
            .Should().Be(OnboardingMissionStatus.Completed);
    }

    [Fact]
    public async Task TrackEventAsync_PlantWatered_ClearsTutorialFlag()
    {
        var databaseName = Guid.NewGuid().ToString();
        Guid plantId;

        await using (var context = TestDbContext.Create(databaseName))
        {
            var species = await context.PlantSpecies.FirstAsync();
            var plant = new UserPlant
            {
                SpeciesId = species.Id,
                Name = "P",
                PlantedAt = DateTime.UtcNow,
                LastWatered = DateTime.UtcNow,
                Status = Core.Enums.PlantStatus.Healthy
            };
            var shelf = new Shelf { Name = "S", SortOrder = 0 };
            context.UserPlants.Add(plant);
            context.Shelves.Add(shelf);
            await context.SaveChangesAsync();
            context.PlantShelves.Add(new PlantShelf { PlantId = plant.Id, ShelfId = shelf.Id, Position = 0 });
            await context.SaveChangesAsync();
            plantId = plant.Id;
        }

        var service = CreateService(databaseName);
        await service.CompleteIntroAsync(skipped: false);
        await service.TrackEventAsync(OnboardingEvent.PlantPlacedOnShelf, plantId);

        var after = await service.TrackEventAsync(OnboardingEvent.PlantWatered, plantId);

        await using var verify = TestDbContext.Create(databaseName);
        var settings = await verify.AppSettings.FirstAsync();
        settings.OnboardingTutorialPlantNeedsWateringAssist.Should().BeFalse();
        settings.OnboardingTutorialPlantId.Should().BeNull();
    }

    [Fact]
    public async Task RefreshSnapshotAsync_ReturnsConsistentSnapshot()
    {
        var service = CreateService(Guid.NewGuid().ToString());

        var s1 = await service.RefreshSnapshotAsync();
        var s2 = await service.RefreshSnapshotAsync();

        s1.Missions.Count.Should().Be(s2.Missions.Count);
    }

    [Fact]
    public async Task StateChanged_RaisedOnMutation()
    {
        var service = CreateService(Guid.NewGuid().ToString());
        int callCount = 0;
        service.StateChanged += (_, _) => callCount++;

        await service.AdvanceIntroAsync();
        await service.CompleteIntroAsync(skipped: false);

        callCount.Should().BeGreaterThanOrEqualTo(1);
    }
}
