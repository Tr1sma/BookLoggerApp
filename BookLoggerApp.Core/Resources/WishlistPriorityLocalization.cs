using BookLoggerApp.Core.Enums;
using Microsoft.Extensions.Localization;

namespace BookLoggerApp.Core.Resources;

public static class WishlistPriorityLocalization
{
    public static string LocalizedLabel(this WishlistPriority priority, IStringLocalizer<AppResources> l)
    {
        LocalizedString value = l[$"Priority_{priority}"];
        return value.ResourceNotFound ? priority.ToString() : value.Value;
    }
}
