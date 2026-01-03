## 2024-05-23 - Client-Side Aggregation Optimization
**Learning:** In scenarios where Entity Framework's computed properties prevent database-side aggregation (like `AverageRating` being a C# property), fetching data once and performing multiple in-memory aggregations is significantly more efficient than repeated database calls for each aggregation metric.
**Action:** Always verify if iterative service methods calling other service methods are causing hidden N+1 queries, especially in statistical or reporting features.
