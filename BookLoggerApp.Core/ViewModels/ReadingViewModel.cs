using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;
using System.Timers;
using Timer = System.Timers.Timer;

namespace BookLoggerApp.Core.ViewModels;

public partial class ReadingViewModel : ViewModelBase, IDisposable
{
    private readonly IProgressService _progressService;
    private readonly IBookService _bookService;
    private readonly IProgressionService _progressionService;
    private readonly ITimerStateService _timerStateService;
    private Timer? _timer;

    public ReadingViewModel(
        IProgressService progressService,
        IBookService bookService,
        IProgressionService progressionService,
        ITimerStateService timerStateService)
    {
        _progressService = progressService;
        _bookService = bookService;
        _progressionService = progressionService;
        _timerStateService = timerStateService;
        _timerStateService.AppResumed += OnAppResumed;
    }

    [ObservableProperty]
    private ReadingSession? _session;

    [ObservableProperty]
    private Book? _book;

    [ObservableProperty]
    private TimeSpan _elapsedTime = TimeSpan.Zero;

    [ObservableProperty]
    private bool _isPaused = true;

    [ObservableProperty]
    private int _currentPage;

    [ObservableProperty]
    private int _startPage;

    [ObservableProperty]
    private int _xpEarned;

    [ObservableProperty]
    private DateTime _sessionStartTime;

    [ObservableProperty]
    private bool _showSessionCelebration;

    [ObservableProperty]
    private ProgressionResult? _sessionProgressionResult;

    [ObservableProperty]
    private bool _showLevelUpCelebration;

    [ObservableProperty]
    private LevelUpResult? _levelUpResult;

    public bool IsRunning => Session != null && !IsPaused;

    [RelayCommand]
    public async Task LoadAsync(Guid sessionId)
    {
        await ExecuteSafelyWithDbAsync(async () =>
        {
            // Load existing session or create new one
            var sessions = await _progressService.GetSessionsByBookAsync(sessionId);
            Session = sessions.FirstOrDefault(s => s.Id == sessionId);

            if (Session == null)
            {
                SetError("Session not found");
                return;
            }

            Book = await _bookService.GetByIdAsync(Session.BookId);
            StartPage = Session.StartPage ?? Book?.CurrentPage ?? 0;
            CurrentPage = Session.EndPage ?? (StartPage + (Session.PagesRead ?? 0));
            XpEarned = Session.XpEarned;
            SessionStartTime = Session.StartedAt;
            
            // Calculate elapsed time
            if (Session.EndedAt.HasValue)
            {
                ElapsedTime = Session.EndedAt.Value - Session.StartedAt;
            }
            else
            {
                ElapsedTime = DateTime.UtcNow - Session.StartedAt;
                StartTimer();
            }
        }, "Failed to load reading session");
    }

    [RelayCommand]
    public async Task StartAsync(Guid bookId)
    {
        await ExecuteSafelyWithDbAsync(async () =>
        {
            Session = await _progressService.StartSessionAsync(bookId);
            Book = await _bookService.GetByIdAsync(bookId);
            SessionStartTime = Session.StartedAt;
            ElapsedTime = TimeSpan.Zero;
            IsPaused = false;
            StartPage = Book?.CurrentPage ?? 0;
            CurrentPage = StartPage;
            XpEarned = 0;

            // Set StartPage in the session
            Session.StartPage = StartPage;
            await _progressService.UpdateSessionAsync(Session);

            StartTimer();
        }, "Failed to start reading session");
    }

    [RelayCommand]
    public void Pause()
    {
        IsPaused = true;
        StopTimer();
    }

    [RelayCommand]
    public void Resume()
    {
        IsPaused = false;
        StartTimer();
    }

    [RelayCommand]
    public async Task EndSessionAsync()
    {
        if (Session == null) return;

        await ExecuteSafelyAsync(async () =>
        {
            StopTimer();

            // Calculate pages read during this session
            var pagesRead = Math.Max(0, CurrentPage - StartPage);

            // End the session with the correct pages read count (now returns SessionEndResult)
            var result = await _progressService.EndSessionAsync(Session.Id, pagesRead);
            Session = result.Session;
            SessionProgressionResult = result.ProgressionResult;

            // Check if book was already completed before update
            var wasCompleted = Book?.Status == ReadingStatus.Completed;

            // Update book progress to the current page
            if (Book != null && Book.CurrentPage != CurrentPage)
            {
                await _bookService.UpdateProgressAsync(Book.Id, CurrentPage);

                // Reload the book to get the updated progress
                Book = await _bookService.GetByIdAsync(Book.Id);
            }

            // Check if book just completed
            if (!wasCompleted && Book?.Status == ReadingStatus.Completed)
            {
                // Award Book Completion XP
                var completionResult = await _progressionService.AwardBookCompletionXpAsync(null);
                
                // Merge results
                SessionProgressionResult = MergeProgressionResults(SessionProgressionResult, completionResult);
                
                // Update session XP to reflect total
                Session.XpEarned = SessionProgressionResult.XpEarned;
                // Note: We don't save the merged XP to session in DB because session XP should reflect just the session.
                // But for display we show total.
            }

            XpEarned = SessionProgressionResult?.XpEarned ?? 0;

            // Show celebration overlay with XP breakdown
            ShowSessionCelebration = true;

            // Check if there was a level-up to show afterwards
            if (SessionProgressionResult?.LevelUp != null)
            {
                LevelUpResult = SessionProgressionResult.LevelUp;
            }
        }, "Failed to end session");
    }

    private ProgressionResult MergeProgressionResults(ProgressionResult? r1, ProgressionResult r2)
    {
        if (r1 == null) return r2;

        var merged = new ProgressionResult
        {
            XpEarned = r1.XpEarned + r2.XpEarned,
            BaseXp = r1.BaseXp + r2.BaseXp,
            MinutesXp = r1.MinutesXp + r2.MinutesXp,
            PagesXp = r1.PagesXp + r2.PagesXp,
            LongSessionBonusXp = r1.LongSessionBonusXp + r2.LongSessionBonusXp,
            StreakBonusXp = r1.StreakBonusXp + r2.StreakBonusXp,
            BookCompletionXp = r1.BookCompletionXp + r2.BookCompletionXp,
            PlantBoostPercentage = r1.PlantBoostPercentage, // Assuming same boost
            BoostedXp = r1.BoostedXp + r2.BoostedXp,
            NewTotalXp = r2.NewTotalXp, // Result 2 is later
            LevelUp = null
        };

        if (r1.LevelUp != null && r2.LevelUp != null)
        {
            merged.LevelUp = new LevelUpResult
            {
                OldLevel = r1.LevelUp.OldLevel,
                NewLevel = r2.LevelUp.NewLevel,
                CoinsAwarded = r1.LevelUp.CoinsAwarded + r2.LevelUp.CoinsAwarded,
                NewTotalCoins = r2.LevelUp.NewTotalCoins
            };
        }
        else if (r1.LevelUp != null)
        {
            merged.LevelUp = r1.LevelUp;
        }
        else
        {
            merged.LevelUp = r2.LevelUp;
        }

        return merged;
    }

    [RelayCommand]
    public async Task UpdatePageAsync(int? page)
    {
        if (Session == null || Book == null || !page.HasValue) return;

        CurrentPage = page.Value;

        // Calculate pages read during this session
        var pagesRead = Math.Max(0, CurrentPage - StartPage);

        // Calculate estimated XP (approximate preview - final calculation at session end)
        // Formula: (minutes × 5) + (pages × 20) + bonuses
        int minutes = (int)ElapsedTime.TotalMinutes;
        int baseXp = (minutes * 5) + (pagesRead * 20);

        // Add long session bonus estimate (50 XP for 60+ minutes)
        if (minutes >= 60)
        {
            baseXp += 50;
        }

        // Note: Streak bonus (+20) and plant boost are not included in preview
        // They will be applied in the final calculation at session end
        XpEarned = baseXp;

        // Update session with correct values
        Session.PagesRead = pagesRead;
        Session.EndPage = CurrentPage;
        Session.XpEarned = XpEarned;
        await _progressService.UpdateSessionAsync(Session);
    }

    private void StartTimer()
    {
        _timer?.Stop();
        _timer = new Timer(1000); // Update every second
        _timer.Elapsed += OnTimerElapsed;
        _timer.Start();
    }

    private void StopTimer()
    {
        _timer?.Stop();
        _timer?.Dispose();
        _timer = null;
    }

    private void OnTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        if (!IsPaused && Session != null)
        {
            ElapsedTime = DateTime.UtcNow - SessionStartTime;
        }
    }

    /// <summary>
    /// Called when session celebration is closed. Shows level-up celebration if applicable.
    /// </summary>
    public void OnSessionCelebrationClose()
    {
        ShowSessionCelebration = false;

        // If there was a level-up, show that celebration next
        if (LevelUpResult != null)
        {
            ShowLevelUpCelebration = true;
        }
    }

    /// <summary>
    /// Called when level-up celebration is closed.
    /// </summary>
    public void OnLevelUpCelebrationClose()
    {
        ShowLevelUpCelebration = false;
        LevelUpResult = null;
    }

    private void OnAppResumed()
    {
        if (!IsPaused && Session != null)
        {
            // Recalculate elapsed from the original session start time
            ElapsedTime = DateTime.UtcNow - SessionStartTime;
            StartTimer();
        }
    }

    public void Dispose()
    {
        _timerStateService.AppResumed -= OnAppResumed;
        StopTimer();
    }
}

