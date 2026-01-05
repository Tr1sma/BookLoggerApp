## 2024-05-22 - Inefficient In-Memory Aggregation
**Learning:** Detected a severe performance anti-pattern where the entire dataset (Books + Genres) was loaded into memory to calculate simple statistics. This causes massive memory pressure and slow startup times as the library grows.
**Action:** Always prefer database-side aggregation (`GroupBy`, `Count`, `Sum`) over `IEnumerable` in-memory processing. Use `Select` projection to fetch only needed data.
