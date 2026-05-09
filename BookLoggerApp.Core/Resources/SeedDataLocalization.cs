using BookLoggerApp.Core.Models;
using Microsoft.Extensions.Localization;

namespace BookLoggerApp.Core.Resources;

/// <summary>
/// Display-layer translation for seed-data-backed entities (plant species, shop
/// decorations). The DB keeps the canonical English names; the UI runs them through
/// this helper to get the translated name/description for the active culture.
///
/// Unknown values (e.g. future plants without a resx key yet, or a user-renamed
/// <see cref="UserPlant"/>) fall back to the original string unchanged.
/// </summary>
public static class SeedDataLocalization
{
    public static string LocalizedName(this PlantSpecies species, IStringLocalizer<AppResources> l)
        => TryLookup(l, $"PlantSpecies_{ToPascalCase(species.Name)}_Name", species.Name);

    public static string LocalizedDescription(this PlantSpecies species, IStringLocalizer<AppResources> l)
        => TryLookup(l, $"PlantSpecies_{ToPascalCase(species.Name)}_Description", species.Description ?? string.Empty);

    public static string LocalizedName(this ShopItem item, IStringLocalizer<AppResources> l)
        => TryLookup(l, $"ShopItem_{ToPascalCase(item.Name)}_Name", item.Name);

    public static string LocalizedDescription(this ShopItem item, IStringLocalizer<AppResources> l)
        => TryLookup(l, $"ShopItem_{ToPascalCase(item.Name)}_Description", item.Description ?? string.Empty);

    /// <summary>
    /// Translates a <see cref="UserPlant"/>'s display name. If the stored name matches
    /// the species default (= user never renamed it), return the localized species name;
    /// otherwise honour the user's custom name verbatim.
    /// </summary>
    public static string LocalizedDisplayName(this UserPlant plant, IStringLocalizer<AppResources> l)
    {
        if (plant.Species is not null && string.Equals(plant.Name, plant.Species.Name, StringComparison.Ordinal))
        {
            return plant.Species.LocalizedName(l);
        }
        return plant.Name;
    }

    /// <summary>
    /// Same pattern for decorations — user-renamed names stay, default names localize.
    /// </summary>
    public static string LocalizedDisplayName(this UserDecoration decoration, IStringLocalizer<AppResources> l)
    {
        if (decoration.ShopItem is not null && string.Equals(decoration.Name, decoration.ShopItem.Name, StringComparison.Ordinal))
        {
            return decoration.ShopItem.LocalizedName(l);
        }
        return decoration.Name;
    }

    private static string TryLookup(IStringLocalizer<AppResources> l, string key, string fallback)
    {
        LocalizedString value = l[key];
        return value.ResourceNotFound ? fallback : value.Value;
    }

    /// <summary>
    /// Converts a human-readable name like "Ancient Knowledge Bonsai" or
    /// "Scholar's Spectacles" into a stable resource-key component like
    /// "AncientKnowledgeBonsai" / "ScholarsSpectacles". Non-alphanumerics are stripped.
    /// </summary>
    private static string ToPascalCase(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        Span<char> buffer = stackalloc char[value.Length];
        int length = 0;
        bool capitalizeNext = true;

        foreach (char c in value)
        {
            if (char.IsLetterOrDigit(c))
            {
                buffer[length++] = capitalizeNext ? char.ToUpperInvariant(c) : c;
                capitalizeNext = false;
            }
            else
            {
                capitalizeNext = true;
            }
        }

        return new string(buffer[..length]);
    }
}
