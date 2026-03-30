using System.Collections.ObjectModel;
using System.Net.Http;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BookLoggerApp.Core.Enums;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;

namespace BookLoggerApp.Core.ViewModels;

/// <summary>
/// VM for managing the book wishlist.
/// </summary>
public partial class WishlistViewModel : ViewModelBase
{
    private readonly IWishlistService _wishlistService;
    private readonly ILookupService _lookupService;

    public ObservableCollection<Book> WishlistBooks { get; } = new();

    [ObservableProperty]
    private int _wishlistCount;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private string _sortBy = "DateAdded";

    // Add-to-wishlist form fields
    [ObservableProperty]
    private string _newTitle = string.Empty;

    [ObservableProperty]
    private string _newAuthor = string.Empty;

    [ObservableProperty]
    private string _newIsbn = string.Empty;

    [ObservableProperty]
    private WishlistPriority _newPriority = WishlistPriority.Medium;

    [ObservableProperty]
    private string _newRecommendedBy = string.Empty;

    [ObservableProperty]
    private string _newWishlistNotes = string.Empty;

    [ObservableProperty]
    private bool _isLookingUp;

    [ObservableProperty]
    private string? _lookupMessage;

    // Lookup result fields for auto-fill
    [ObservableProperty]
    private int? _lookupPageCount;

    [ObservableProperty]
    private string? _lookupCoverUrl;

    [ObservableProperty]
    private string? _lookupDescription;

    public WishlistViewModel(IWishlistService wishlistService, ILookupService lookupService)
    {
        _wishlistService = wishlistService;
        _lookupService = lookupService;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        await ExecuteSafelyWithDbAsync(async () =>
        {
            WishlistBooks.Clear();
            var books = await _wishlistService.GetWishlistBooksAsync();

            foreach (var b in ApplySort(books))
                WishlistBooks.Add(b);

            WishlistCount = books.Count;
        }, "Failed to load wishlist");
    }

    [RelayCommand]
    public async Task AddToWishlistAsync()
    {
        await ExecuteSafelyAsync(async () =>
        {
            var book = new Book
            {
                Title = NewTitle.Trim(),
                Author = NewAuthor.Trim(),
                ISBN = string.IsNullOrWhiteSpace(NewIsbn) ? null : NewIsbn.Trim(),
                Status = ReadingStatus.Wishlist,
                PageCount = _lookupPageCount,
                CoverImagePath = _lookupCoverUrl,
                Description = _lookupDescription
            };

            var info = new WishlistInfo
            {
                Priority = NewPriority,
                RecommendedBy = string.IsNullOrWhiteSpace(NewRecommendedBy) ? null : NewRecommendedBy.Trim(),
                WishlistNotes = string.IsNullOrWhiteSpace(NewWishlistNotes) ? null : NewWishlistNotes.Trim(),
                DateAddedToWishlist = DateTime.UtcNow
            };

            var added = await _wishlistService.AddToWishlistAsync(book, info);
            WishlistBooks.Insert(0, added);
            WishlistCount = WishlistBooks.Count;
            ClearAddForm();
        }, "Failed to add to wishlist");
    }

    [RelayCommand]
    public async Task LookupByIsbnAsync()
    {
        if (string.IsNullOrWhiteSpace(NewIsbn)) return;

        IsLookingUp = true;
        LookupMessage = null;

        try
        {
            var metadata = await _lookupService.LookupByISBNAsync(NewIsbn.Trim());
            if (metadata != null)
            {
                NewTitle = metadata.Title;
                NewAuthor = metadata.Author;
                _lookupPageCount = metadata.PageCount;
                _lookupCoverUrl = metadata.CoverImageUrl;
                _lookupDescription = metadata.Description;
                LookupMessage = "Book found!";
            }
            else
            {
                LookupMessage = "No book found for this ISBN.";
            }
        }
        catch (HttpRequestException ex) when (ex.InnerException is System.Net.Sockets.SocketException)
        {
            LookupMessage = "No internet connection. Please check your network and try again.";
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
        {
            LookupMessage = "Too many requests. Please wait a moment and try again.";
        }
        catch (TaskCanceledException)
        {
            LookupMessage = "Request timed out. Please try again.";
        }
        catch (Exception ex)
        {
            LookupMessage = $"Lookup failed: {ex.Message}";
        }
        finally
        {
            IsLookingUp = false;
        }
    }

    [RelayCommand]
    public async Task MoveToLibraryAsync(Guid bookId)
    {
        await ExecuteSafelyAsync(async () =>
        {
            await _wishlistService.MoveToLibraryAsync(bookId);
            var book = WishlistBooks.FirstOrDefault(b => b.Id == bookId);
            if (book != null)
                WishlistBooks.Remove(book);
            WishlistCount = WishlistBooks.Count;
        }, "Failed to move to library");
    }

    [RelayCommand]
    public async Task RemoveFromWishlistAsync(Guid bookId)
    {
        await ExecuteSafelyAsync(async () =>
        {
            await _wishlistService.RemoveFromWishlistAsync(bookId);
            var book = WishlistBooks.FirstOrDefault(b => b.Id == bookId);
            if (book != null)
                WishlistBooks.Remove(book);
            WishlistCount = WishlistBooks.Count;
        }, "Failed to remove from wishlist");
    }

    [RelayCommand]
    public async Task SearchAsync()
    {
        await ExecuteSafelyAsync(async () =>
        {
            WishlistBooks.Clear();
            IReadOnlyList<Book> books;

            if (string.IsNullOrWhiteSpace(SearchQuery))
                books = await _wishlistService.GetWishlistBooksAsync();
            else
                books = await _wishlistService.SearchWishlistAsync(SearchQuery);

            foreach (var b in ApplySort(books))
                WishlistBooks.Add(b);
            WishlistCount = books.Count;
        }, "Failed to search wishlist");
    }

    [RelayCommand]
    public async Task SortAsync()
    {
        if (!WishlistBooks.Any()) return;

        var sorted = ApplySort(WishlistBooks).ToList();
        WishlistBooks.Clear();
        foreach (var b in sorted)
            WishlistBooks.Add(b);
    }

    private IEnumerable<Book> ApplySort(IEnumerable<Book> books) => SortBy switch
    {
        "Priority" => books.OrderByDescending(b => b.WishlistInfo?.Priority ?? WishlistPriority.Low),
        "Title" => books.OrderBy(b => b.Title),
        "Author" => books.OrderBy(b => b.Author),
        _ => books.OrderByDescending(b => b.WishlistInfo?.DateAddedToWishlist ?? b.DateAdded)
    };

    public void ClearAddForm()
    {
        NewTitle = string.Empty;
        NewAuthor = string.Empty;
        NewIsbn = string.Empty;
        NewPriority = WishlistPriority.Medium;
        NewRecommendedBy = string.Empty;
        NewWishlistNotes = string.Empty;
        _lookupPageCount = null;
        _lookupCoverUrl = null;
        _lookupDescription = null;
        LookupMessage = null;
    }
}
