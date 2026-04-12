using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;
using System.Threading;
using System.Timers;
using Timer = System.Timers.Timer;

namespace BookLoggerApp.Core.ViewModels;

public partial class ReadingViewModel : ViewModelBase, IDisposable
{
    private readonly IProgressService _progressService;
    private readonly IBookService _bookService;
    private readonly IProgressionService _progressionService;
    private readonly ITimerStateService _timerStateService;
    private readonly IShareCardService _shareCardService;
    private readonly IImageService _imageService;
    private Timer? _timer;
    private bool _bookCompletedDuringSession;
    private bool _goalCompletedDuringSession;
    private bool _reviewPromptMomentPending;
    private bool _streakCelebrationPending;
    private readonly object _timerLock = new();
    private readonly SynchronizationContext? _uiSynchronizationContext;

    /// <summary>
    /// Raised when a book recommendation share card PNG is ready. The component handles file write + sharing.
    /// </summary>
    public event Action<byte[]>? BookShareCardReady;

    public ReadingViewModel(
        IProgressService progressService,
        IBookService bookService,
        IProgressionService progressionService,
        ITimerStateService timerStateService,
        IShareCardService shareCardService,
        IImageService imageService)
    {
        _progressService = progressService;
        _bookService = bookService;
        _progressionService = progressionService;
        _timerStateService = timerStateService;
        _shareCardService = shareCardService;
        _imageService = imageService;
        _uiSynchronizationContext = SynchronizationContext.Current;
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
    private bool _showStreakCelebration;

    [ObservableProperty]
    private LevelUpResult? _levelUpResult;

    [ObservableProperty]
    private bool _showBookCompletionCelebration;

    [ObservableProperty]
    private bool _isGeneratingBookCard;

    public bool IsRunning => Session != null && !IsPaused;
    public bool HasReviewPromptMoment => _reviewPromptMomentPending || _bookCompletedDuringSession || _goalCompletedDuringSession || LevelUpResult != null;

    [RelayCommand]
    public async Task LoadAsync(Guid sessionId)
    {
        await ExecuteSafelyWithDbAsync(async () =>
        {
            // Load existing session by its ID
            Session = await _progressService.GetSessionByIdAsync(sessionId);

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
            _bookCompletedDuringSession = false;
            _goalCompletedDuringSession = false;
            _reviewPromptMomentPending = false;
            _streakCelebrationPending = false;
            LevelUpResult = null;
            ShowStreakCelebration = false;

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

            // End the session with the correct pages read count
            var result = await _progressService.EndSessionAsync(Session.Id, pagesRead);
            Session = result.Session;
            SessionProgressionResult = result.ProgressionResult;
            _goalCompletedDuringSession = result.GoalCompleted;

            // Capture status before any updates
            var wasAlreadyCompleted = Book?.Status == ReadingStatus.Completed;

            // Update book progress (auto-completes + awards XP if last page reached)
            ProgressionResult? completionResult = null;
            if (Book != null && Book.CurrentPage != CurrentPage)
            {
                completionResult = await _bookService.UpdateProgressAsync(Book.Id, CurrentPage);
            }

            // Always reload the book to get the latest status
            if (Book != null)
            {
                Book = await _bookService.GetByIdAsync(Book.Id);
            }

            // Detect book completion: return value OR status change (fallback)
            _bookCompletedDuringSession = completionResult != null
                || (!wasAlreadyCompleted && Book?.Status == ReadingStatus.Completed);

            if (_bookCompletedDuringSession && completionResult != null)
            {
                // Merge completion XP into the session result for display
                SessionProgressionResult = MergeProgressionResults(SessionProgressionResult, completionResult);
                Session.XpEarned = SessionProgressionResult.XpEarned;
            }

            XpEarned = SessionProgressionResult?.XpEarned ?? 0;
            _streakCelebrationPending = ShouldShowStreakCelebration(SessionProgressionResult);

            // Show the appropriate celebration
            if (_bookCompletedDuringSession)
            {
                // Skip session XP modal — go directly to book completion celebration
                ShowBookCompletionCelebration = true;
            }
            else
            {
                ShowSessionCelebration = true;
            }

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
            StreakDays = Math.Max(r1.StreakDays, r2.StreakDays),
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

        // Note: Streak bonus and plant boost are not included in preview
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
        lock (_timerLock)
        {
            _timer?.Stop();
            _timer?.Dispose();

            _timer = new Timer(1000); // Update every second
            _timer.Elapsed += OnTimerElapsed;
            _timer.AutoReset = true;
            _timer.Start();
        }
    }

    private void StopTimer()
    {
        lock (_timerLock)
        {
            _timer?.Stop();
            _timer?.Dispose();
            _timer = null;
        }
    }

    private void OnTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        DispatchToUiThread(UpdateElapsedTime);
    }

    private void UpdateElapsedTime()
    {
        if (!IsPaused && Session != null)
        {
            ElapsedTime = DateTime.UtcNow - SessionStartTime;
        }
    }

    private void DispatchToUiThread(Action action)
    {
        if (_uiSynchronizationContext == null || SynchronizationContext.Current == _uiSynchronizationContext)
        {
            action();
            return;
        }

        _uiSynchronizationContext.Post(static state =>
        {
            if (state is Action callback)
            {
                callback();
            }
        }, action);
    }

    /// <summary>
    /// Called when session celebration is closed. Shows streak or follow-up celebrations if applicable.
    /// </summary>
    public Task OnSessionCelebrationClose()
    {
        ShowSessionCelebration = false;

        if (_streakCelebrationPending)
        {
            _streakCelebrationPending = false;
            ShowStreakCelebration = true;
        }
        else if (LevelUpResult != null)
        {
            ShowLevelUpCelebration = true;
        }
        else if (_bookCompletedDuringSession)
        {
            ShowBookCompletionCelebration = true;
        }
        else
        {
            _goalCompletedDuringSession = false;
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Called when streak celebration is closed. Shows level-up celebration if applicable.
    /// </summary>
    public Task OnStreakCelebrationClose()
    {
        ShowStreakCelebration = false;
        _reviewPromptMomentPending = false;

        if (!_bookCompletedDuringSession && LevelUpResult != null)
        {
            ShowLevelUpCelebration = true;
        }
        else
        {
            _bookCompletedDuringSession = false;
            _goalCompletedDuringSession = false;
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Called when level-up celebration is closed. Shows book completion celebration if applicable.
    /// </summary>
    public Task OnLevelUpCelebrationClose()
    {
        ShowLevelUpCelebration = false;
        LevelUpResult = null;
        _reviewPromptMomentPending = false;

        if (_bookCompletedDuringSession)
        {
            ShowBookCompletionCelebration = true;
        }
        else
        {
            _goalCompletedDuringSession = false;
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Called when book completion celebration is closed.
    /// </summary>
    public Task OnBookCompletionCelebrationClose()
    {
        ShowBookCompletionCelebration = false;

        if (_streakCelebrationPending)
        {
            _reviewPromptMomentPending = HasReviewPromptMoment;
            _bookCompletedDuringSession = false;
            _streakCelebrationPending = false;
            ShowStreakCelebration = true;
            return Task.CompletedTask;
        }

        _bookCompletedDuringSession = false;
        _goalCompletedDuringSession = false;
        _reviewPromptMomentPending = false;
        return Task.CompletedTask;
    }

    private static bool ShouldShowStreakCelebration(ProgressionResult? result)
    {
        return result?.StreakDays >= 2 && result.StreakBonusXp > 0;
    }

    /// <summary>
    /// Generates a book recommendation share card for the completed book.
    /// </summary>
    [RelayCommand]
    public async Task GenerateAndShareBookCardAsync()
    {
        if (Book == null) return;

        await ExecuteSafelyAsync(async () =>
        {
            IsGeneratingBookCard = true;

            int totalMinutes = await _progressService.GetTotalMinutesAsync(Book.Id);

            byte[]? coverBytes = null;
            var coverResult = await _imageService.GetResizedCoverImageAsync(Book.Id, 600, 900);
            if (coverResult.HasValue)
            {
                coverBytes = coverResult.Value.Bytes;
            }

            var data = new BookShareData
            {
                Title = Book.Title,
                Author = Book.Author,
                PageCount = Book.PageCount,
                TotalMinutesRead = totalMinutes,
                AverageRating = Book.AverageRating,
                CoverImageBytes = coverBytes,
                CategoryRatings = new Dictionary<RatingCategory, int?>
                {
                    { RatingCategory.Characters, Book.CharactersRating },
                    { RatingCategory.Plot, Book.PlotRating },
                    { RatingCategory.WritingStyle, Book.WritingStyleRating },
                    { RatingCategory.SpiceLevel, Book.SpiceLevelRating },
                    { RatingCategory.Pacing, Book.PacingRating },
                    { RatingCategory.WorldBuilding, Book.WorldBuildingRating },
                    { RatingCategory.Spannung, Book.SpannungRating },
                    { RatingCategory.Humor, Book.HumorRating },
                    { RatingCategory.Informationsgehalt, Book.InformationsgehaltRating },
                    { RatingCategory.EmotionaleTiefe, Book.EmotionaleTiefeRating },
                    { RatingCategory.Atmosphaere, Book.AtmosphaereRating }
                }
            };

            byte[] cardBytes = await _shareCardService.GenerateBookCardAsync(data);
            BookShareCardReady?.Invoke(cardBytes);

            IsGeneratingBookCard = false;
        }, "Failed to generate book recommendation card");

        IsGeneratingBookCard = false;
    }

    private void OnAppResumed()
    {
        DispatchToUiThread(() =>
        {
            if (!IsPaused && Session != null)
            {
                // Recalculate elapsed from the original session start time
                ElapsedTime = DateTime.UtcNow - SessionStartTime;
                StartTimer();
            }
        });
    }

    public void Dispose()
    {
        _timerStateService.AppResumed -= OnAppResumed;
        StopTimer();
    }
}

