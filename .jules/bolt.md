## 2024-05-23 - Streak Calculation Performance
**Learning:** Calculating streaks by fetching all entities into memory (`GetAllAsync`) is a scalability bottleneck.
**Action:** Use database-side projection (`Select`) and `Distinct` to fetch only the necessary data (e.g., dates) for aggregation logic.
