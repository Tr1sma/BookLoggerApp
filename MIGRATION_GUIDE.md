# Migration Guide: Multi-Category Rating System

## Overview

This guide documents the database migration from the single `Rating` column to a multi-category rating system with 7 distinct rating categories.

## Migration Details

### Migration Name
`20251106065030_AddMultiCategoryRatings`

### What Changed

#### New Columns Added to Books Table
1. `CharactersRating` (INTEGER, nullable) - Rating for character development
2. `PlotRating` (INTEGER, nullable) - Rating for plot quality
3. `WritingStyleRating` (INTEGER, nullable) - Rating for writing style
4. `SpiceLevelRating` (INTEGER, nullable) - Rating for spice/romance level
5. `PacingRating` (INTEGER, nullable) - Rating for pacing
6. `WorldBuildingRating` (INTEGER, nullable) - Rating for world building
7. `OverallRating` (INTEGER, nullable) - Overall rating

### Data Migration

The migration automatically copies existing `Rating` values to `OverallRating`:

```sql
UPDATE Books
SET OverallRating = Rating
WHERE Rating IS NOT NULL
```

## Backwards Compatibility

### Obsolete Rating Property

The old `Rating` property has been marked as `[Obsolete]` but remains functional:

```csharp
[Obsolete("Use OverallRating instead")]
public int? Rating
{
    get => OverallRating;
    set => OverallRating = value;
}
```

This ensures that:
- Existing code using `Rating` continues to work
- New code should use `OverallRating` directly
- No breaking changes for existing functionality

### AverageRating Calculation

Books now have an `AverageRating` computed property that:
1. Calculates the average of all non-null category ratings
2. Returns `OverallRating` if no category ratings are set
3. Returns `null` if no ratings exist at all

```csharp
public double? AverageRating
{
    get
    {
        var ratings = new List<int?>
        {
            CharactersRating, PlotRating, WritingStyleRating,
            SpiceLevelRating, PacingRating, WorldBuildingRating
        };
        var validRatings = ratings.Where(r => r.HasValue).Select(r => r!.Value).ToList();
        if (!validRatings.Any())
            return OverallRating;
        return validRatings.Average();
    }
}
```

## How to Apply the Migration

### Development Environment

1. **Ensure you have a backup** (recommended for production)
   ```bash
   # Backup your database if needed
   ```

2. **Apply the migration**
   ```bash
   dotnet ef database update --project BookLoggerApp.Infrastructure/BookLoggerApp.Infrastructure.csproj
   ```

3. **Verify migration success**
   ```bash
   dotnet ef migrations list --project BookLoggerApp.Infrastructure/BookLoggerApp.Infrastructure.csproj
   ```

   You should see `20251106065030_AddMultiCategoryRatings` in the list.

### Production Environment

1. **Create a database backup**
   ```bash
   # Use your database backup tool
   # For SQLite: cp booklogger.db3 booklogger.db3.backup
   ```

2. **Test migration on a copy first**
   - Copy production database
   - Apply migration to the copy
   - Verify data integrity
   - Test application functionality

3. **Apply to production**
   ```bash
   dotnet ef database update --project BookLoggerApp.Infrastructure/BookLoggerApp.Infrastructure.csproj
   ```

4. **Verify in production**
   - Check that old books still have their ratings (in OverallRating)
   - Verify new rating features work correctly
   - Monitor application logs for errors

## Migration Scenarios

### Scenario 1: Existing Books with Ratings

**Before Migration:**
```json
{
  "Id": "...",
  "Title": "Old Book",
  "Rating": 4
}
```

**After Migration:**
```json
{
  "Id": "...",
  "Title": "Old Book",
  "Rating": 4,  // Obsolete, redirects to OverallRating
  "OverallRating": 4,
  "CharactersRating": null,
  "PlotRating": null,
  "WritingStyleRating": null,
  "SpiceLevelRating": null,
  "PacingRating": null,
  "WorldBuildingRating": null,
  "AverageRating": 4  // Returns OverallRating when no category ratings
}
```

### Scenario 2: Upgrading Old Book to New System

Users can gradually upgrade old books with category ratings:

```csharp
// Old book with only OverallRating
var book = await bookService.GetByIdAsync(bookId);
book.OverallRating == 4; // From old Rating

// Add new category ratings
book.CharactersRating = 5;
book.PlotRating = 4;
book.WritingStyleRating = 5;

await bookService.UpdateAsync(book);

// AverageRating now calculates from category ratings
book.AverageRating == 4.67; // (5 + 4 + 5) / 3
```

### Scenario 3: New Books

New books can use the full multi-category system:

```csharp
var newBook = new Book
{
    Title = "New Book",
    Author = "Author",
    Status = ReadingStatus.Completed,
    CharactersRating = 5,
    PlotRating = 4,
    WritingStyleRating = 5,
    SpiceLevelRating = 3,
    PacingRating = 4,
    WorldBuildingRating = 5,
    OverallRating = 4
};
```

## Rollback

If you need to rollback the migration:

```bash
dotnet ef database update 20251105132656_AddSpineColorToBook --project BookLoggerApp.Infrastructure/BookLoggerApp.Infrastructure.csproj
```

**Warning:** Rolling back will delete the new rating columns and their data!

## Verification

### Verify Migration Applied

```bash
dotnet ef migrations list --project BookLoggerApp.Infrastructure/BookLoggerApp.Infrastructure.csproj
```

Look for `20251106065030_AddMultiCategoryRatings` in the output.

### Verify Data Integrity

Run the migration tests:

```bash
dotnet test BookLoggerApp.Tests/BookLoggerApp.Tests.csproj --filter "FullyQualifiedName~MigrationTests"
```

All 7 tests should pass:
- ✅ Migration_OldBooksWithRating_ShouldHaveOverallRating
- ✅ Migration_OldBooksCanBeUpgradedToNewRatings
- ✅ Migration_NewBooksWithMultipleRatings_ShouldWorkCorrectly
- ✅ Migration_MixedBooks_ShouldCoexist
- ✅ Migration_DatabaseSchema_ShouldHaveAllColumns
- ✅ Migration_NullRatings_ShouldBeHandledCorrectly
- ✅ Migration_ObsoleteRatingProperty_ShouldWorkThroughAllLayers

## Common Issues

### Issue: "No migrations were applied"

**Solution:** This means the database is already up to date. The migration has already been applied.

### Issue: "Migration already applied but data not migrated"

**Solution:** The SQL data migration script may have failed. You can manually run:

```sql
UPDATE Books
SET OverallRating = Rating
WHERE Rating IS NOT NULL AND OverallRating IS NULL;
```

### Issue: "Old Rating property not working"

**Cause:** The obsolete `Rating` property getter/setter might not be working correctly.

**Solution:** Use `OverallRating` directly instead:

```csharp
// Old way (deprecated)
book.Rating = 5;

// New way (recommended)
book.OverallRating = 5;
```

## Statistics and Reporting

### New Service Methods

The `IStatsService` has been extended with new methods for multi-category ratings:

```csharp
Task<double> GetAverageRatingByCategoryAsync(RatingCategory category, ...);
Task<Dictionary<RatingCategory, double>> GetAllAverageRatingsAsync(...);
Task<List<BookRatingSummary>> GetTopRatedBooksAsync(int count = 10, RatingCategory? category = null, ...);
Task<List<BookRatingSummary>> GetBooksWithRatingsAsync(...);
```

### Rating Categories

```csharp
public enum RatingCategory
{
    Characters = 0,
    Plot = 1,
    WritingStyle = 2,
    SpiceLevel = 3,
    Pacing = 4,
    WorldBuilding = 5,
    Overall = 6
}
```

## UI Changes

### New Components

- **RatingInput.razor**: Reusable component for rating input with emojis
- **Rating sections** in BookDetail.razor and BookEdit.razor
- **Statistics sections** in Stats.razor showing category averages and top books

### CSS

New styling in `wwwroot/css/ratings.css` with:
- Star rating animations
- Hover effects
- Responsive design
- Dark mode support

## Testing

### Test Coverage

- ✅ 11 Book Model tests
- ✅ 16 StatsService tests
- ✅ 9 Integration tests
- ✅ 7 Migration tests

**Total: 43 rating-related tests**

Run all rating tests:

```bash
dotnet test BookLoggerApp.Tests/BookLoggerApp.Tests.csproj --filter "FullyQualifiedName~Rating"
```

## Support

If you encounter issues with the migration:

1. Check the migration test results
2. Verify database backup is available
3. Review application logs
4. Check this migration guide for common issues
5. Consult the development team

## Additional Resources

- **Plan Document**: `BookRatingPlan.md`
- **Migration File**: `BookLoggerApp.Infrastructure/Migrations/20251106065030_AddMultiCategoryRatings.cs`
- **Tests**: `BookLoggerApp.Tests/Integration/MigrationTests.cs`
- **Model**: `BookLoggerApp.Core/Models/Book.cs`
- **Service**: `BookLoggerApp.Infrastructure/Services/StatsService.cs`
