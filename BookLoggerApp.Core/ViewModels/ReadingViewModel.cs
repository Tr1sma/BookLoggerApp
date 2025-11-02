using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;
using System.Timers;
using Timer = System.Timers.Timer;

namespace BookLoggerApp.Core.ViewModels;

public partial class ReadingViewModel : ViewModelBase
{
    private readonly IProgressService _progressService;
    private readonly IBookService _bookService;
    private Timer? _timer;

    public ReadingViewModel(IProgressService progressService, IBookService bookService)
    {
        _progressService = progressService;
        _bookService = bookService;
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
    private int _xpEarned;

    [ObservableProperty]
    private DateTime _sessionStartTime;

    public bool IsRunning => Session != null && !IsPaused;

    [RelayCommand]
    public async Task LoadAsync(Guid sessionId)
    {
        await ExecuteSafelyAsync(async () =>
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
            CurrentPage = Session.PagesRead ?? 0;
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
        await ExecuteSafelyAsync(async () =>
        {
            Session = await _progressService.StartSessionAsync(bookId);
            Book = await _bookService.GetByIdAsync(bookId);
            SessionStartTime = Session.StartedAt;
            ElapsedTime = TimeSpan.Zero;
            IsPaused = false;
            CurrentPage = Book?.CurrentPage ?? 0;
            XpEarned = 0;
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
            
            Session = await _progressService.EndSessionAsync(Session.Id, CurrentPage);
            
            // Update book progress if page changed
            if (Book != null && Book.CurrentPage != CurrentPage)
            {
                await _bookService.UpdateProgressAsync(Book.Id, CurrentPage);
            }

            XpEarned = Session.XpEarned;
        }, "Failed to end session");
    }

    [RelayCommand]
    public async Task UpdatePageAsync(int? page)
    {
        if (Session == null || Book == null || !page.HasValue) return;

        CurrentPage = page.Value;
        
        // Calculate XP based on pages read
        var pagesRead = Math.Max(0, CurrentPage - (Session.PagesRead ?? 0));
        XpEarned = pagesRead * 2; // 2 XP per page
        
        // Update session
        Session.PagesRead = CurrentPage;
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
}

