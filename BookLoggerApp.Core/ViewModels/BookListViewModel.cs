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
        await ExecuteSafelyWithDbAsync(async ct =>
        {
            var all = await _books.GetAllAsync(ct);
            Items.Clear();
            foreach (var b in all) Items.Add(b);
        }, Tr("Error_FailedTo_LoadBooks"));
    }

    [RelayCommand(CanExecute = nameof(CanAdd))]
    public async Task AddAsync()
    {
        ClearError();
        if (!CanAdd())
        {
            SetError(Tr("Error_BookTitleAuthorRequired"));
            return;
        }

        var book = new Book
        {
            Title = NewTitle.Trim(),
            Author = NewAuthor.Trim(),
            Status = ReadingStatus.Planned
        };
        await _books.AddAsync(book);
        Items.Insert(0, book);

        NewTitle = string.Empty;
        NewAuthor = string.Empty;
        NotifyCanExecuteChanged();
    }

    private bool CanAdd() =>
        !string.IsNullOrWhiteSpace(NewTitle) &&
        !string.IsNullOrWhiteSpace(NewAuthor);

    private void NotifyCanExecuteChanged()
    {
        // Re-evaluate CanExecute for AddAsync
        AddAsyncCommand.NotifyCanExecuteChanged();
    }

    partial void OnNewTitleChanged(string value)
    {
        NotifyCanExecuteChanged();
    }

    partial void OnNewAuthorChanged(string value)
    {
        NotifyCanExecuteChanged();
    }

    // Add this property to expose the RelayCommand instance for AddAsync
    public IRelayCommand AddAsyncCommand => AddAsyncCommandField ??= new AsyncRelayCommand(AddAsync, CanAdd);

    private AsyncRelayCommand? AddAsyncCommandField;
}
