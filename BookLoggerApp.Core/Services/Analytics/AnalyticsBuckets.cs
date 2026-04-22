namespace BookLoggerApp.Core.Services.Analytics;

public static class AnalyticsBuckets
{
    public static string BookCount(int n) => n switch
    {
        <= 0 => "0",
        <= 5 => "1-5",
        <= 20 => "6-20",
        <= 50 => "21-50",
        _ => "51+"
    };

    public static string Level(int n) => n switch
    {
        <= 5 => "1-5",
        <= 10 => "6-10",
        <= 20 => "11-20",
        <= 35 => "21-35",
        <= 50 => "36-50",
        _ => "51+"
    };

    public static string Pages(int n) => n switch
    {
        <= 0 => "0",
        <= 10 => "1-10",
        <= 50 => "11-50",
        <= 200 => "51-200",
        <= 500 => "201-500",
        _ => "501+"
    };

    public static string Minutes(int n) => n switch
    {
        <= 5 => "0-5",
        <= 15 => "6-15",
        <= 30 => "16-30",
        <= 60 => "31-60",
        <= 120 => "61-120",
        _ => "120+"
    };

    public static string XpDelta(int n) => n switch
    {
        <= 0 => "0",
        <= 10 => "1-10",
        <= 50 => "11-50",
        <= 200 => "51-200",
        <= 1000 => "201-1000",
        _ => "1000+"
    };

    public static string Coins(int n) => n switch
    {
        <= 50 => "0-50",
        <= 200 => "51-200",
        <= 1000 => "201-1000",
        <= 5000 => "1001-5000",
        _ => "5000+"
    };

    public static string Sessions(int n) => n switch
    {
        <= 0 => "0",
        <= 10 => "1-10",
        <= 50 => "11-50",
        <= 200 => "51-200",
        _ => "201+"
    };

    public static string Plants(int n) => n switch
    {
        <= 0 => "0",
        1 => "1",
        <= 3 => "2-3",
        <= 6 => "4-6",
        _ => "7+"
    };

    public static string Shelves(int n) => n switch
    {
        <= 0 => "0",
        1 => "1",
        <= 3 => "2-3",
        <= 6 => "4-6",
        _ => "7+"
    };

    public static string RatingInt(int? n) => n.HasValue ? n.Value.ToString() : "none";

    public static string SizeBytes(long n) => n switch
    {
        < 1_000_000 => "<1MB",
        < 5_000_000 => "1-5MB",
        < 25_000_000 => "5-25MB",
        _ => "25MB+"
    };

    public static string Progress(double percent) => percent switch
    {
        <= 0 => "0",
        < 25 => "1-24",
        < 50 => "25-49",
        < 75 => "50-74",
        < 100 => "75-99",
        _ => "100"
    };

    public static string InstallAge(DateTime createdUtc, DateTime? nowUtc = null)
    {
        var now = nowUtc ?? DateTime.UtcNow;
        var days = (now - createdUtc).TotalDays;
        return days switch
        {
            < 1 => "<1d",
            <= 7 => "1-7d",
            <= 30 => "8-30d",
            <= 90 => "31-90d",
            _ => "90d+"
        };
    }

    public static string DaysSince(DateTime utc, DateTime? nowUtc = null)
    {
        var now = nowUtc ?? DateTime.UtcNow;
        var days = (now - utc).TotalDays;
        return days switch
        {
            < 1 => "<1d",
            <= 3 => "1-3d",
            <= 7 => "4-7d",
            <= 14 => "8-14d",
            <= 30 => "15-30d",
            _ => "30d+"
        };
    }

    public static string Count(int n) => n switch
    {
        <= 0 => "0",
        <= 10 => "1-10",
        <= 100 => "11-100",
        <= 1000 => "101-1000",
        _ => "1000+"
    };
}
