using BookLoggerApp.Core.Resources;
using Microsoft.Extensions.Localization;

namespace BookLoggerApp.Core.Helpers;

/// <summary>
/// Shared localized minute formatting so Dashboard, Stats and BookDetail render
/// reading durations identically through the <c>Common_Time_*</c> resource keys.
/// </summary>
public static class TimeFormatHelper
{
    public static string FormatMinutes(IStringLocalizer<AppResources> localizer, int minutes)
    {
        if (minutes < 60)
        {
            return localizer["Common_Time_Minutes", minutes];
        }

        int hours = minutes / 60;
        int remainingMinutes = minutes % 60;
        return localizer["Common_Time_HoursMinutes", hours, remainingMinutes];
    }
}
