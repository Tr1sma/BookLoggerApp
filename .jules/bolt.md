## 2024-05-23 - [StatsService Memory Usage]
**Learning:** `StatsService` was performing "client-side evaluation" for aggregates by fetching all entities into memory (e.g., `GetAllAsync()`) and then using LINQ to Objects (`Sum`, `Count`). This causes massive memory usage and slow performance as the dataset grows.
**Action:** Use `IUnitOfWork.Context` to execute `SumAsync`, `CountAsync`, etc., directly on the database. Always check if Repositories expose aggregate methods or if direct Context access is needed for performance critical paths.
