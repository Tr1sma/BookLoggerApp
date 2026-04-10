using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BookLoggerApp.Infrastructure.Services;

public class OnboardingService : IOnboardingService
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly AppSettingsProvider _settingsProvider;
    private readonly ILogger<OnboardingService> _logger;
    private readonly SemaphoreSlim _syncLock = new(1, 1);
    private OnboardingSnapshot? _cachedSnapshot;

    public OnboardingService(
        IDbContextFactory<AppDbContext> contextFactory,
        AppSettingsProvider settingsProvider,
        ILogger<OnboardingService> logger)
    {
        _contextFactory = contextFactory;
        _settingsProvider = settingsProvider;
        _logger = logger;
    }

    public event EventHandler? StateChanged;

    public async Task<OnboardingSnapshot> GetSnapshotAsync(CancellationToken ct = default)
    {
        return await RefreshSnapshotAsync(ct);
    }

    public async Task<OnboardingSnapshot> RefreshSnapshotAsync(CancellationToken ct = default)
    {
        await _syncLock.WaitAsync(ct);

        try
        {
            return await RefreshSnapshotInternalAsync(ct);
        }
        finally
        {
            _syncLock.Release();
        }
    }

    public async Task<OnboardingSnapshot> AdvanceIntroAsync(CancellationToken ct = default)
    {
        return await MutateAsync(async context =>
        {
            var settings = await EnsureSettingsAsync(context, ct);
            var missionStates = await context.OnboardingMissionStates.ToListAsync(ct);

            await ReconcileSettingsAsync(context, settings, ct);
            await SynchronizeMissionStatesAsync(context, settings, missionStates, ct);

            if (settings.OnboardingIntroStatus is OnboardingIntroStatus.Completed or OnboardingIntroStatus.Skipped)
            {
                return BuildSnapshot(settings, missionStates);
            }

            if (settings.OnboardingCurrentStep < OnboardingMissionCatalog.IntroStepCount - 1)
            {
                settings.OnboardingCurrentStep++;
            }

            settings.OnboardingIntroStatus = OnboardingIntroStatus.InProgress;
            settings.OnboardingFlowVersion = OnboardingMissionCatalog.CurrentFlowVersion;

            return await SaveAndBuildSnapshotAsync(context, settings, missionStates, ct);
        }, ct);
    }

    public async Task<OnboardingSnapshot> RetreatIntroAsync(CancellationToken ct = default)
    {
        return await MutateAsync(async context =>
        {
            var settings = await EnsureSettingsAsync(context, ct);
            var missionStates = await context.OnboardingMissionStates.ToListAsync(ct);

            await ReconcileSettingsAsync(context, settings, ct);
            await SynchronizeMissionStatesAsync(context, settings, missionStates, ct);

            if (settings.OnboardingIntroStatus is OnboardingIntroStatus.Completed or OnboardingIntroStatus.Skipped)
            {
                return BuildSnapshot(settings, missionStates);
            }

            if (settings.OnboardingCurrentStep > 0)
            {
                settings.OnboardingCurrentStep--;
            }

            settings.OnboardingIntroStatus = settings.OnboardingCurrentStep == 0
                ? OnboardingIntroStatus.NotStarted
                : OnboardingIntroStatus.InProgress;
            settings.OnboardingFlowVersion = OnboardingMissionCatalog.CurrentFlowVersion;

            return await SaveAndBuildSnapshotAsync(context, settings, missionStates, ct);
        }, ct);
    }

    public async Task<OnboardingSnapshot> CompleteIntroAsync(bool skipped, CancellationToken ct = default)
    {
        return await MutateAsync(async context =>
        {
            var settings = await EnsureSettingsAsync(context, ct);
            var missionStates = await context.OnboardingMissionStates.ToListAsync(ct);

            await ReconcileSettingsAsync(context, settings, ct);
            await SynchronizeMissionStatesAsync(context, settings, missionStates, ct);

            ApplyIntroCompletion(settings, skipped);

            return await SaveAndBuildSnapshotAsync(context, settings, missionStates, ct);
        }, ct);
    }

    public async Task<OnboardingSnapshot> ResetIntroAsync(CancellationToken ct = default)
    {
        return await MutateAsync(async context =>
        {
            var settings = await EnsureSettingsAsync(context, ct);
            var missionStates = await context.OnboardingMissionStates.ToListAsync(ct);

            settings.OnboardingFlowVersion = OnboardingMissionCatalog.CurrentFlowVersion;
            settings.OnboardingIntroStatus = OnboardingIntroStatus.NotStarted;
            settings.OnboardingCurrentStep = 0;
            settings.HasCompletedOnboarding = false;
            settings.OnboardingCompletedAt = null;
            settings.OnboardingAutoCompletedForExistingUser = false;

            await SynchronizeMissionStatesAsync(context, settings, missionStates, ct);

            return await SaveAndBuildSnapshotAsync(context, settings, missionStates, ct);
        }, ct);
    }

    public async Task<OnboardingSnapshot> ResetAllAsync(CancellationToken ct = default)
    {
        return await MutateAsync(async context =>
        {
            var settings = await EnsureSettingsAsync(context, ct);

            settings.OnboardingFlowVersion = OnboardingMissionCatalog.CurrentFlowVersion;
            settings.OnboardingIntroStatus = OnboardingIntroStatus.NotStarted;
            settings.OnboardingCurrentStep = 0;
            settings.HasCompletedOnboarding = false;
            settings.OnboardingCompletedAt = null;
            settings.OnboardingAutoCompletedForExistingUser = false;
            settings.OnboardingTutorialPlantId = null;
            settings.OnboardingTutorialPlantNeedsWateringAssist = false;

            var existingMissionStates = await context.OnboardingMissionStates.ToListAsync(ct);
            context.OnboardingMissionStates.RemoveRange(existingMissionStates);
            var missionStates = new List<OnboardingMissionState>();
            await SynchronizeMissionStatesAsync(context, settings, missionStates, ct);

            return await SaveAndBuildSnapshotAsync(context, settings, missionStates, ct);
        }, ct);
    }

    public async Task<OnboardingSnapshot> TrackEventAsync(
        OnboardingEvent onboardingEvent,
        Guid? entityId = null,
        CancellationToken ct = default)
    {
        return await MutateAsync(async context =>
        {
            var settings = await EnsureSettingsAsync(context, ct);
            var missionStates = await context.OnboardingMissionStates.ToListAsync(ct);

            await ReconcileSettingsAsync(context, settings, ct);
            await SynchronizeMissionStatesAsync(context, settings, missionStates, ct);

            var now = DateTime.UtcNow;
            var missionStateIndex = missionStates.ToDictionary(m => m.MissionId, StringComparer.Ordinal);

            if (onboardingEvent == OnboardingEvent.IntroCompleted)
            {
                ApplyIntroCompletion(settings, skipped: false);
            }
            else if (onboardingEvent == OnboardingEvent.IntroSkipped)
            {
                ApplyIntroCompletion(settings, skipped: true);
            }

            var missionId = GetMissionForEvent(onboardingEvent);
            if (missionId.HasValue)
            {
                var missionState = EnsureMissionState(context, missionStates, missionStateIndex, missionId.Value, now);
                MarkMissionCompleted(missionState, now);
            }

            if (onboardingEvent == OnboardingEvent.PlantPlacedOnShelf && entityId.HasValue)
            {
                await PrepareTutorialPlantForWateringAsync(context, settings, entityId.Value, ct);
            }

            if (onboardingEvent == OnboardingEvent.PlantWatered)
            {
                settings.OnboardingTutorialPlantNeedsWateringAssist = false;

                if (entityId.HasValue && settings.OnboardingTutorialPlantId == entityId.Value)
                {
                    settings.OnboardingTutorialPlantId = null;
                }
            }

            await SynchronizeMissionStatesAsync(context, settings, missionStates, ct);

            return await SaveAndBuildSnapshotAsync(context, settings, missionStates, ct);
        }, ct);
    }

    private async Task<OnboardingSnapshot> RefreshSnapshotInternalAsync(CancellationToken ct)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var settings = await EnsureSettingsAsync(context, ct);
        var missionStates = await context.OnboardingMissionStates.ToListAsync(ct);

        await ReconcileSettingsAsync(context, settings, ct);
        await SynchronizeMissionStatesAsync(context, settings, missionStates, ct);

        return await SaveAndBuildSnapshotAsync(context, settings, missionStates, ct, raiseStateChanged: false);
    }

    private async Task<OnboardingSnapshot> MutateAsync(
        Func<AppDbContext, Task<OnboardingSnapshot>> mutation,
        CancellationToken ct)
    {
        await _syncLock.WaitAsync(ct);

        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync(ct);
            return await mutation(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mutate onboarding state");
            throw;
        }
        finally
        {
            _syncLock.Release();
        }
    }

    private async Task<AppSettings> EnsureSettingsAsync(AppDbContext context, CancellationToken ct)
    {
        var settings = await context.AppSettings.FirstOrDefaultAsync(ct);
        if (settings != null)
        {
            return settings;
        }

        settings = new AppSettings
        {
            Theme = "Light",
            Language = "en",
            UserLevel = 1,
            TotalXp = 0,
            Coins = 100,
            OnboardingFlowVersion = OnboardingMissionCatalog.CurrentFlowVersion,
            OnboardingIntroStatus = OnboardingIntroStatus.NotStarted
        };

        context.AppSettings.Add(settings);
        return settings;
    }

    private async Task ReconcileSettingsAsync(AppDbContext context, AppSettings settings, CancellationToken ct)
    {
        settings.OnboardingCurrentStep = Math.Clamp(
            settings.OnboardingCurrentStep,
            0,
            OnboardingMissionCatalog.IntroStepCount - 1);

        if (settings.OnboardingFlowVersion < OnboardingMissionCatalog.CurrentFlowVersion)
        {
            if (await HasExistingUserDataAsync(context, ct))
            {
                settings.OnboardingFlowVersion = OnboardingMissionCatalog.CurrentFlowVersion;
                settings.OnboardingIntroStatus = OnboardingIntroStatus.Completed;
                settings.OnboardingCurrentStep = 0;
                settings.HasCompletedOnboarding = true;
                settings.OnboardingCompletedAt ??= DateTime.UtcNow;
                settings.OnboardingAutoCompletedForExistingUser = true;
                settings.OnboardingTutorialPlantId = null;
                settings.OnboardingTutorialPlantNeedsWateringAssist = false;
            }
            else
            {
                settings.OnboardingFlowVersion = OnboardingMissionCatalog.CurrentFlowVersion;
                settings.OnboardingIntroStatus = OnboardingIntroStatus.NotStarted;
                settings.OnboardingCurrentStep = 0;
                settings.HasCompletedOnboarding = false;
                settings.OnboardingCompletedAt = null;
                settings.OnboardingAutoCompletedForExistingUser = false;
                settings.OnboardingTutorialPlantId = null;
                settings.OnboardingTutorialPlantNeedsWateringAssist = false;
            }
        }

        var introFinished = settings.OnboardingIntroStatus is OnboardingIntroStatus.Completed or OnboardingIntroStatus.Skipped;
        settings.HasCompletedOnboarding = introFinished;

        if (!introFinished)
        {
            settings.OnboardingCompletedAt = null;
        }
    }

    private async Task<bool> HasExistingUserDataAsync(AppDbContext context, CancellationToken ct)
    {
        if (await context.Books.AnyAsync(ct))
        {
            return true;
        }

        if (await context.ReadingSessions.AnyAsync(ct))
        {
            return true;
        }

        if (await context.ReadingGoals.AnyAsync(ct))
        {
            return true;
        }

        if (await context.UserPlants.AnyAsync(ct))
        {
            return true;
        }

        return await context.WishlistInfos.AnyAsync(ct);
    }

    private async Task SynchronizeMissionStatesAsync(
        AppDbContext context,
        AppSettings settings,
        List<OnboardingMissionState> missionStates,
        CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var missionStateIndex = missionStates.ToDictionary(m => m.MissionId, StringComparer.Ordinal);
        var completedFromHistory = await GetCompletedMissionsFromHistoryAsync(context, settings, ct);

        foreach (var definition in OnboardingMissionCatalog.Missions)
        {
            EnsureMissionState(context, missionStates, missionStateIndex, definition.Id, now);
        }

        var completedMissionIds = missionStates
            .Where(m => m.Status == OnboardingMissionStatus.Completed)
            .Select(m => ParseMissionId(m.MissionId))
            .ToHashSet();

        completedMissionIds.UnionWith(completedFromHistory);

        foreach (var definition in OnboardingMissionCatalog.Missions)
        {
            var missionState = missionStateIndex[GetMissionKey(definition.Id)];
            var desiredStatus = completedMissionIds.Contains(definition.Id)
                ? OnboardingMissionStatus.Completed
                : definition.Prerequisites.All(completedMissionIds.Contains)
                    ? OnboardingMissionStatus.Available
                    : OnboardingMissionStatus.Locked;

            if (missionState.Status != desiredStatus)
            {
                missionState.Status = desiredStatus;
                missionState.UpdatedAt = now;
            }

            if (desiredStatus == OnboardingMissionStatus.Completed)
            {
                missionState.CompletedAt ??= now;
                completedMissionIds.Add(definition.Id);
            }
            else
            {
                missionState.CompletedAt = null;
            }
        }

        if (missionStateIndex.TryGetValue(GetMissionKey(OnboardingMissionId.WaterFirstPlant), out var waterMission) &&
            waterMission.Status == OnboardingMissionStatus.Completed)
        {
            settings.OnboardingTutorialPlantId = null;
            settings.OnboardingTutorialPlantNeedsWateringAssist = false;
        }
    }

    private async Task<HashSet<OnboardingMissionId>> GetCompletedMissionsFromHistoryAsync(
        AppDbContext context,
        AppSettings settings,
        CancellationToken ct)
    {
        var completed = new HashSet<OnboardingMissionId>();

        if (await context.Books.AnyAsync(ct))
        {
            completed.Add(OnboardingMissionId.AddFirstBook);
        }

        if (await context.ReadingSessions.AnyAsync(ct))
        {
            completed.Add(OnboardingMissionId.LogFirstSession);
        }

        if (await context.Books.AnyAsync(book =>
            book.Status == ReadingStatus.Completed &&
            book.CharactersRating.HasValue &&
            book.PlotRating.HasValue &&
            book.WritingStyleRating.HasValue &&
            book.SpiceLevelRating.HasValue &&
            book.PacingRating.HasValue &&
            book.WorldBuildingRating.HasValue, ct))
        {
            completed.Add(OnboardingMissionId.RateCompletedBookAll6);
        }

        if (await context.ReadingGoals.AnyAsync(ct))
        {
            completed.Add(OnboardingMissionId.CreateFirstGoal);
        }

        if (await context.UserPlants.AnyAsync(ct))
        {
            completed.Add(OnboardingMissionId.BuyFirstPlant);
        }

        if (await context.PlantShelves.AnyAsync(ct))
        {
            completed.Add(OnboardingMissionId.PlaceFirstPlantOnShelf);
        }

        if (await context.WishlistInfos.AnyAsync(ct))
        {
            completed.Add(OnboardingMissionId.AddToWishlist);
        }

        if (settings.LastBackupDate.HasValue)
        {
            completed.Add(OnboardingMissionId.CreateBackup);
        }

        return completed;
    }

    private async Task PrepareTutorialPlantForWateringAsync(
        AppDbContext context,
        AppSettings settings,
        Guid plantId,
        CancellationToken ct)
    {
        if (settings.OnboardingFlowVersion != OnboardingMissionCatalog.CurrentFlowVersion ||
            settings.OnboardingAutoCompletedForExistingUser)
        {
            return;
        }

        var plant = await context.UserPlants
            .Include(p => p.Species)
            .FirstOrDefaultAsync(p => p.Id == plantId, ct);

        if (plant?.Species == null)
        {
            return;
        }

        settings.OnboardingTutorialPlantId = plant.Id;
        settings.OnboardingTutorialPlantNeedsWateringAssist = true;
        plant.LastWatered = DateTime.UtcNow.AddDays(-(plant.Species.WaterIntervalDays + 1));
    }

    private static void ApplyIntroCompletion(AppSettings settings, bool skipped)
    {
        settings.OnboardingFlowVersion = OnboardingMissionCatalog.CurrentFlowVersion;
        settings.OnboardingIntroStatus = skipped ? OnboardingIntroStatus.Skipped : OnboardingIntroStatus.Completed;
        settings.OnboardingCurrentStep = 0;
        settings.HasCompletedOnboarding = true;
        settings.OnboardingCompletedAt = DateTime.UtcNow;
        settings.OnboardingAutoCompletedForExistingUser = false;
    }

    private async Task<OnboardingSnapshot> SaveAndBuildSnapshotAsync(
        AppDbContext context,
        AppSettings settings,
        List<OnboardingMissionState> missionStates,
        CancellationToken ct,
        bool raiseStateChanged = true)
    {
        if (context.ChangeTracker.HasChanges())
        {
            settings.UpdatedAt = DateTime.UtcNow;
            await context.SaveChangesAsync(ct);
        }

        _settingsProvider.SetCachedSettings(settings);
        _cachedSnapshot = BuildSnapshot(settings, missionStates);

        if (raiseStateChanged)
        {
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        return _cachedSnapshot;
    }

    private static OnboardingSnapshot BuildSnapshot(AppSettings settings, IReadOnlyCollection<OnboardingMissionState> missionStates)
    {
        var missionStateIndex = missionStates.ToDictionary(m => m.MissionId, StringComparer.Ordinal);
        var completedMissionIds = missionStates
            .Where(m => m.Status == OnboardingMissionStatus.Completed)
            .Select(m => ParseMissionId(m.MissionId))
            .ToHashSet();

        var missions = OnboardingMissionCatalog.Missions
            .Select(definition =>
            {
                var status = missionStateIndex.TryGetValue(GetMissionKey(definition.Id), out var state)
                    ? state.Status
                    : OnboardingMissionStatus.Locked;

                string? note = null;
                if (status == OnboardingMissionStatus.Locked)
                {
                    foreach (var prereq in definition.Prerequisites)
                    {
                        if (!completedMissionIds.Contains(prereq))
                        {
                            note = $"Complete \"{OnboardingMissionCatalog.GetDefinition(prereq).Title}\" first.";
                            break;
                        }
                    }
                }
                else if (definition.Id == OnboardingMissionId.WaterFirstPlant &&
                         settings.OnboardingTutorialPlantNeedsWateringAssist)
                {
                    note = "Your tutorial plant was prepared so you can water it right away.";
                }

                return new OnboardingMissionProgress
                {
                    Id = definition.Id,
                    Icon = definition.Icon,
                    Title = definition.Title,
                    Description = definition.Description,
                    CtaLabel = definition.CtaLabel,
                    Route = definition.DefaultRoute,
                    Status = status,
                    CompletedAt = state?.CompletedAt,
                    IsCore = definition.IsCore,
                    IsTimeGated = definition.IsTimeGated,
                    Note = note
                };
            })
            .ToArray();

        return new OnboardingSnapshot
        {
            FlowVersion = settings.OnboardingFlowVersion,
            IntroStepCount = OnboardingMissionCatalog.IntroStepCount,
            CurrentIntroStep = settings.OnboardingCurrentStep,
            IntroStatus = settings.OnboardingIntroStatus,
            ShouldShowIntro = settings.OnboardingFlowVersion == OnboardingMissionCatalog.CurrentFlowVersion &&
                              settings.OnboardingIntroStatus is OnboardingIntroStatus.NotStarted or OnboardingIntroStatus.InProgress,
            Missions = missions,
            FeatureAtlas = OnboardingMissionCatalog.FeatureAtlas
        };
    }

    private static OnboardingMissionState EnsureMissionState(
        AppDbContext context,
        ICollection<OnboardingMissionState> missionStates,
        IDictionary<string, OnboardingMissionState> missionStateIndex,
        OnboardingMissionId missionId,
        DateTime now)
    {
        var key = GetMissionKey(missionId);
        if (missionStateIndex.TryGetValue(key, out var existing))
        {
            return existing;
        }

        var created = new OnboardingMissionState
        {
            MissionId = key,
            Status = OnboardingMissionStatus.Locked,
            CreatedAt = now
        };

        missionStates.Add(created);
        context.OnboardingMissionStates.Add(created);
        missionStateIndex[key] = created;
        return created;
    }

    private static void MarkMissionCompleted(OnboardingMissionState missionState, DateTime now)
    {
        missionState.Status = OnboardingMissionStatus.Completed;
        missionState.CompletedAt ??= now;
        missionState.UpdatedAt = now;
    }

    private static string GetMissionKey(OnboardingMissionId missionId) => missionId.ToString();

    private static OnboardingMissionId ParseMissionId(string missionId) =>
        Enum.TryParse<OnboardingMissionId>(missionId, out var parsed)
            ? parsed
            : throw new InvalidOperationException($"Unknown onboarding mission id '{missionId}'.");

    private static OnboardingMissionId? GetMissionForEvent(OnboardingEvent onboardingEvent) =>
        onboardingEvent switch
        {
            OnboardingEvent.BookCreated => OnboardingMissionId.AddFirstBook,
            OnboardingEvent.ReadingSessionLogged => OnboardingMissionId.LogFirstSession,
            OnboardingEvent.BookRated => OnboardingMissionId.RateCompletedBookAll6,
            OnboardingEvent.GoalCreated => OnboardingMissionId.CreateFirstGoal,
            OnboardingEvent.PlantPurchased => OnboardingMissionId.BuyFirstPlant,
            OnboardingEvent.PlantPlacedOnShelf => OnboardingMissionId.PlaceFirstPlantOnShelf,
            OnboardingEvent.PlantWatered => OnboardingMissionId.WaterFirstPlant,
            OnboardingEvent.StatsShared => OnboardingMissionId.ShareStatsCard,
            OnboardingEvent.BookShared => OnboardingMissionId.ShareCompletedBook,
            OnboardingEvent.IsbnScanned => OnboardingMissionId.ScanIsbn,
            OnboardingEvent.WishlistAdded => OnboardingMissionId.AddToWishlist,
            OnboardingEvent.BackupCreated => OnboardingMissionId.CreateBackup,
            _ => null
        };
}
