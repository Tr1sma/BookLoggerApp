## 2024-05-23 - Database-Side Grouping for Many-to-Many Relationships
**Learning:** Loading `Include(b => b.BookGenres)` then `ThenInclude(bg => bg.Genre)` just to count genres is a massive performance anti-pattern. EF Core can group directly on the join entity `BookGenre` to perform `COUNT()` in SQL.
**Action:** When calculating statistics on many-to-many relationships, query the join entity (e.g., `_context.Set<BookGenre>()`) directly with `GroupBy` instead of loading the main entities.

## 2024-05-24 - Database-Side Aggregation for Stats
**Learning:** Moving `Count` and `Average` operations from in-memory (loading all entities) to database queries (using `CountAsync` and `AverageAsync`) significantly reduces memory usage and data transfer.
**Action:** When calculating statistics, always prefer specialized Repository methods that execute aggregations in the database. Ensure date filters are sargable (range check vs .Year) and `AverageAsync` handles empty sets by projecting to nullable types.
