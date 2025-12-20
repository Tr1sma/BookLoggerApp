using BookLoggerApp.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace BookLoggerApp.Infrastructure.Extensions;

/// <summary>
/// Extension methods for IQueryable.
/// </summary>
public static class QueryableExtensions
{
    /// <summary>
    /// Filters books by a search term using Like operator on Title, Author, and ISBN.
    /// </summary>
    public static IQueryable<Book> Search(this IQueryable<Book> query, string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return query;

        return query.Where(b => EF.Functions.Like(b.Title, $"%{searchTerm}%") ||
                                EF.Functions.Like(b.Author, $"%{searchTerm}%") ||
                                (b.ISBN != null && EF.Functions.Like(b.ISBN, $"%{searchTerm}%")));
    }
}
