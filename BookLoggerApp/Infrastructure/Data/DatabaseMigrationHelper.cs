using Microsoft.Data.Sqlite;
using System.IO;

namespace BookLoggerApp.Infrastructure.Data;

/// <summary>
/// Helper class to migrate database from legacy location if it contains more progress (XP).
/// </summary>
public static class DatabaseMigrationHelper
{
    public static System.Text.StringBuilder Log { get; } = new System.Text.StringBuilder();

    private static void LogMessage(string message)
    {
        System.Diagnostics.Debug.WriteLine(message);
        Log.AppendLine(message);
    }
    
    public static void MigrateIfNecessary(string currentDbPath)
    {
        try
        {
            var folder = Path.GetDirectoryName(currentDbPath);
            if (string.IsNullOrEmpty(folder)) return;

            var fileName = Path.GetFileName(currentDbPath);
            
            // Legacy path (Personal folder)
            var legacyFolder = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
            var legacyDbPath = Path.Combine(legacyFolder, fileName);

            LogMessage($"[DatabaseMigrationHelper] Checking legacy path: {legacyDbPath}");
            LogMessage($"[DatabaseMigrationHelper] Legacy folder: {legacyFolder}");

            // DIAGNOSTIC LOOP: valid only for debugging this issue
            if (Directory.Exists(legacyFolder))
            {
                try
                {
                    var allFiles = Directory.GetFiles(legacyFolder);
                    LogMessage($"[DatabaseMigrationHelper] Found {allFiles.Length} files in legacy folder ({legacyFolder}):");
                    foreach (var f in allFiles)
                    {
                        LogMessage($" - {Path.GetFileName(f)}");
                    }
                }
                catch (Exception ex)
                {
                     LogMessage($"[DatabaseMigrationHelper] Failed to list files in legacy folder: {ex.Message}");
                }
            }
            else
            {
                 LogMessage($"[DatabaseMigrationHelper] Legacy folder does not exist: {legacyFolder}");
                 
                 // Try listing parent folder
                 try 
                 {
                    var parent = Directory.GetParent(legacyFolder);
                    if (parent != null && parent.Exists)
                    {
                        LogMessage($"[DatabaseMigrationHelper] Listing parent folder ({parent.FullName}):");
                        foreach (var f in Directory.GetFiles(parent.FullName)) LogMessage($" - F: {Path.GetFileName(f)}");
                        foreach (var d in Directory.GetDirectories(parent.FullName)) LogMessage($" - D: {Path.GetFileName(d)}");
                    }
                 }
                 catch(Exception pEx) { LogMessage($"Parent check failed: {pEx.Message}"); }
            }

            // If legacy DB doesn't exist, check if we renamed it to .bak in a previous run
            if (!File.Exists(legacyDbPath))
            {
                LogMessage("[DatabaseMigrationHelper] No standard legacy database found at expected path.");
                
                // CRITICAL FIX: Only check for backups if folder exists
                if (Directory.Exists(legacyFolder))
                {
                    LogMessage("[DatabaseMigrationHelper] Checking legacy folder for backups...");
                    var backupFiles = Directory.GetFiles(legacyFolder, "booklogger.db3*");
                    // ... filtering logic ...
                    var validBackups = backupFiles.Where(f => f.Contains(".bak")).OrderByDescending(f => File.GetLastWriteTime(f)).ToList();
                    if (validBackups.Any()) 
                    {
                         legacyDbPath = validBackups.First();
                         LogMessage($"[DatabaseMigrationHelper] Found legacy backup: {legacyDbPath}");
                    }
                }
                else
                {
                    LogMessage("[DatabaseMigrationHelper] Legacy folder does not exist. Skipping backup check in that folder.");
                }

                // === DEEP SEARCH START ===
                // If we still haven't found a legacy DB, let's search the ENTIRE app sandbox for ANY user files
                if (!File.Exists(legacyDbPath))
                {
                    LogMessage("[DatabaseMigrationHelper] Initiating DEEP SEARCH for ANY user files...");
                    try 
                    {
                        var appRoot = Directory.GetParent(Environment.GetFolderPath(Environment.SpecialFolder.Personal))?.Parent?.FullName;
                        if (string.IsNullOrEmpty(appRoot)) 
                        {
                             var filesDir = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
                             appRoot = Directory.GetParent(filesDir)?.FullName;
                             appRoot = Directory.GetParent(appRoot!)?.FullName;
                        }

                        if (!string.IsNullOrEmpty(appRoot) && Directory.Exists(appRoot))
                        {
                            LogMessage($"[DatabaseMigrationHelper] Searching from App Root: {appRoot}");
                            
                            // SEARCH ALL FILES
                            var allFiles = Directory.GetFiles(appRoot, "*", SearchOption.AllDirectories);
                            
                            LogMessage($"[DatabaseMigrationHelper] Found {allFiles.Length} total files in sandbox.");
                            
                            foreach(var f in allFiles)
                            {
                                var info = new FileInfo(f);
                                var name = info.Name.ToLower();
                                var size = info.Length;

                                // Filter out obvious system/cache files to find the needle in the haystack
                                if (name.EndsWith(".so") || name.EndsWith(".dex") || name.EndsWith(".xml") || 
                                    name.EndsWith(".json") || name.Contains("cache") || size == 0)
                                    continue;

                                LogMessage($" - File: {f} (Size: {size})");

                                // Check if this looks like a SQLite DB (sqlite header starts with "SQLite format 3")
                                // Or simplistically, if it ends in .db3 or .db or has significant size and "book" in name
                                bool looksLikeDb = name.EndsWith(".db3") || name.EndsWith(".db") || name.EndsWith(".sqlite");

                                if (looksLikeDb && f != currentDbPath)
                                {
                                    LogMessage($"   -> CANDIDATE FOUND! Trying to migrate...");
                                    try 
                                    {
                                         long candidateXp = GetTotalXp(f);
                                         
                                         if (candidateXp > 0)
                                         {
                                              LogMessage($"   -> VALID DB! XP: {candidateXp}");
                                              
                                              // Double check against current
                                              long searchCurrentXp = GetTotalXp(currentDbPath);
                                              LogMessage($"   -> Current XP: {searchCurrentXp}");

                                              if (candidateXp > searchCurrentXp)
                                              {
                                                  legacyDbPath = f;
                                                  goto Migrationcheck;
                                              }
                                              else
                                              {
                                                  LogMessage($"   -> Candidate valid but has less/equal XP. Ignoring.");
                                              }
                                         }
                                    }
                                    catch { /* Ignore unreadable files */ }
                                }
                            }
                        }
                    }
                    catch (Exception searchEx)
                    {
                        LogMessage($"[DatabaseMigrationHelper] Deep search error: {searchEx.Message}");
                    }
                }
                // === DEEP SEARCH END ===
            }

            Migrationcheck:




            // BREAK IF LEGACY STILL NOT FOUND
            if (!File.Exists(legacyDbPath))
            {
                 LogMessage("[DatabaseMigrationHelper] No valid legacy database found after exhaustive search. Creating new DB.");
                 return;
            }

            // Case 1: Current DB doesn't exist at all -> Safe to copy
            if (!File.Exists(currentDbPath))
            {
                LogMessage("[DatabaseMigrationHelper] Current DB missing. Restoring from legacy...");
                RestoreFromLegacy(legacyDbPath, currentDbPath);
                return;
            }

            // Case 2: Both exist -> Compare XP
            LogMessage("[DatabaseMigrationHelper] Both databases exist. Comparing XP...");
            
            long legacyXp = GetTotalXp(legacyDbPath);
            long currentXp = GetTotalXp(currentDbPath);

            LogMessage($"[DatabaseMigrationHelper] Legacy XP: {legacyXp}, Current XP: {currentXp}");

            if (legacyXp > currentXp)
            {
                LogMessage("[DatabaseMigrationHelper] Legacy has more XP. Overwriting current DB...");
                
                // Backup current just in case (e.g., they just made a new book they don't want to lose? 
                // Well, user said overwrite if XP is higher, but safety first)
                var backupCurrent = currentDbPath + ".bak_lowxp_" + DateTime.Now.ToString("yyyyMMddHHmmss");
                File.Copy(currentDbPath, backupCurrent);
                LogMessage($"[DatabaseMigrationHelper] Backed up low-XP DB to: {backupCurrent}");

                RestoreFromLegacy(legacyDbPath, currentDbPath);
            }
            else
            {
                LogMessage("[DatabaseMigrationHelper] Current DB has equal or more XP. Skipping migration.");
                // Optionally rename legacy so we don't check again every time?
                // Let's leave it for now or rename it to .verified_legacy to skip next time?
                // User might want to keep it as backup.
            }

        }
        
        catch (Exception ex)
        {
            LogMessage($"[DatabaseMigrationHelper] Migration check failed: {ex.Message}");
            LogMessage(ex.StackTrace);
        }
    }

    private static void RestoreFromLegacy(string legacyPath, string currentPath)
    {
        try
        {
            File.Copy(legacyPath, currentPath, overwrite: true);
            
            // Rename legacy to mark as migrated/backup
            var backupLegacy = legacyPath + ".bak_migrated_" + DateTime.Now.ToString("yyyyMMddHHmmss");
            File.Move(legacyPath, backupLegacy);
            
            System.Diagnostics.Debug.WriteLine($"[DatabaseMigrationHelper] Successfully restored legacy DB. Original moved to: {backupLegacy}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DatabaseMigrationHelper] Failed to copy/move files: {ex.Message}");
            throw; 
        }
    }

    private static long GetTotalXp(string dbPath)
    {
        try
        {
            using var connection = new SqliteConnection($"Data Source={dbPath}");
            connection.Open();

            // Check if AppSettings table exists using SQLite master table
            var checkCmd = connection.CreateCommand();
            checkCmd.CommandText = "SELECT count(*) FROM sqlite_master WHERE type='table' AND name='AppSettings';";
            var result = (long?)checkCmd.ExecuteScalar();

            if (result == null || result == 0)
            {
                System.Diagnostics.Debug.WriteLine($"[DatabaseMigrationHelper] 'AppSettings' table not found in {dbPath}");
                return 0;
            }

            var xpCmd = connection.CreateCommand();
            // Assuming there's only one row or we take the max/latest. 
            // Model usually has one row with ID '9999...'.
            // Let's take MAX(TotalXp) to be safe.
            xpCmd.CommandText = "SELECT MAX(TotalXp) FROM AppSettings;";
            var xpResult = xpCmd.ExecuteScalar();

            if (xpResult != null && xpResult != DBNull.Value)
            {
                return Convert.ToInt64(xpResult);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DatabaseMigrationHelper] Error reading XP from {dbPath}: {ex.Message}");
        }
        
        return 0;
    }
}
