using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;

namespace BookLoggerApp.Core.ViewModels;

/// <summary>
/// VM for a single book, including progress.
/// </summary>
public partial class BookDetailViewModel : ObservableObject
{
    private readonly IBookService _books;
    private readonly IProgressService _progress;

    [ObservableProperty] private Book? _book;
    [ObservableProperty] private int _totalMinutes;

    public BookDetailViewModel(IBookService books, IProgressService progress)
    {
        _books = books;
        _progress = progress;
    }

    [RelayCommand]
    public async Task LoadAsync(Guid bookId)
    {
        this.Book = await _books.GetByIdAsync(bookId);
        this.TotalMinutes = (this.Book is null) ? 0 : await _progress.GetTotalMinutesAsync(bookId);
    }

    [RelayCommand]
    public async Task AddSessionAsync(int minutes)
    {
        if (this.Book is null || minutes <= 0) return;
        await _progress.AddSessionAsync(new ReadingSession
        {
            BookId = this.Book.Id,
            Minutes = minutes,
            StartedAt = DateTime.UtcNow
        });
        this.TotalMinutes = await _progress.GetTotalMinutesAsync(this.Book.Id);
    }
}
