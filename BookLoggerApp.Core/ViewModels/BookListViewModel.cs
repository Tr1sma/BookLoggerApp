using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;

namespace BookLoggerApp.Core.ViewModels;

/// <summary>
/// VM for listing and adding books.
/// </summary>
public partial class BookListViewModel : ViewModelBase
{
    private readonly IBookService _books;

    public ObservableCollection<Book> Items { get; } = new();

    [ObservableProperty]
    private string _newTitle = string.Empty;

    [ObservableProperty]
    private string _newAuthor = string.Empty;

    public BookListViewModel(IBookService books)
    {
        _books = books;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        await ExecuteSafelyWithDbAsync(async () =>
        {
            Items.Clear();
            var all = await _books.GetAllAsync();
            foreach (var b in all) Items.Add(b);
        }, "Failed to load books");
    }

    [RelayCommand(CanExecute = nameof(CanAdd))]
    public async Task AddAsync()
    {
        var book = new Book
        {
            Title = NewTitle.Trim(),
            Author = NewAuthor.Trim(),
            Status = ReadingStatus.Planned
        };
        await _books.AddAsync(book);
        Items.Add(book);

        NewTitle = string.Empty;
        NewAuthor = string.Empty;
        NotifyCanExecuteChanged();
    }

    private bool CanAdd() => !string.IsNullOrWhiteSpace(NewTitle);

    private void NotifyCanExecuteChanged()
    {
        // Re-evaluate CanExecute for AddAsync
        AddAsyncCommand.NotifyCanExecuteChanged();
    }

    // Add this property to expose the RelayCommand instance for AddAsync
    public IRelayCommand AddAsyncCommand => AddAsyncCommandField ??= new AsyncRelayCommand(AddAsync, CanAdd);

    private AsyncRelayCommand? AddAsyncCommandField;
}
