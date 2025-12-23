## 2024-05-22 - Environment and Architecture
**Learning:** The development environment lacks the `dotnet` CLI, making it impossible to run builds or tests.
**Action:** Rely on static code analysis and careful manual verification. Avoid creating temporary test files that cannot be executed.

## 2024-05-22 - Data Access Pattern
**Learning:** `StatsService` bypasses repositories and accesses `_unitOfWork.Context` directly for complex aggregation queries.
**Action:** When optimizing complex queries, it is acceptable to use `_unitOfWork.Context` if the repository interface does not support the required LINQ operations (like `GroupBy`).
