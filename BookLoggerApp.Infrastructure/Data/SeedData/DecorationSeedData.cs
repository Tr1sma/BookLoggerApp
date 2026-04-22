using BookLoggerApp.Core.Enums;
using BookLoggerApp.Core.Models;

namespace BookLoggerApp.Infrastructure.Data.SeedData;

/// <summary>
/// Central source of truth for decoration shop item data.
/// Used by AppDbContext for seeding (migrations) and DbInitializer for runtime syncing.
/// ID block: 20000000-0000-0000-0000-00000000000X
/// </summary>
public static class DecorationSeedData
{
    // Level 1 (starter tier) — 100–150 coins
    private static readonly Guid _candleId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly Guid _mugId = Guid.Parse("20000000-0000-0000-0000-000000000002");
    private static readonly Guid _bookendWoodId = Guid.Parse("20000000-0000-0000-0000-000000000003");

    // Level 5 — 175–180 coins
    private static readonly Guid _hourglassId = Guid.Parse("20000000-0000-0000-0000-000000000004");
    private static readonly Guid _spectaclesId = Guid.Parse("20000000-0000-0000-0000-000000000005");

    // Level 10 — 200–220 coins
    private static readonly Guid _inkwellId = Guid.Parse("20000000-0000-0000-0000-000000000006");
    private static readonly Guid _owlFigurineId = Guid.Parse("20000000-0000-0000-0000-000000000007");

    // Level 15 — 260–280 coins
    private static readonly Guid _globeId = Guid.Parse("20000000-0000-0000-0000-000000000008");
    private static readonly Guid _bookendMarbleId = Guid.Parse("20000000-0000-0000-0000-000000000009");

    // Level 20 — 320–340 coins
    private static readonly Guid _telescopeId = Guid.Parse("20000000-0000-0000-0000-000000000010");
    private static readonly Guid _magicLampId = Guid.Parse("20000000-0000-0000-0000-000000000011");

    // Level 25 (prestige) — 360–400 coins
    private static readonly Guid _dragonFigurineId = Guid.Parse("20000000-0000-0000-0000-000000000012");
    private static readonly Guid _alchemyFlaskId = Guid.Parse("20000000-0000-0000-0000-000000000013");
    private static readonly Guid _ancientScrollId = Guid.Parse("20000000-0000-0000-0000-000000000014");

    // Level 70 (ultimate, singleton) — Herz der Geschichten
    private static readonly Guid _storyHeartId = Guid.Parse("20000000-0000-0000-0000-00000000000F");

    public static IEnumerable<ShopItem> GetDecorations()
    {
        yield return new ShopItem
        {
            Id = _candleId,
            ItemType = ShopItemType.Decoration,
            Name = "Reading Candle",
            Description = "A warm flickering candle — the perfect reading companion.",
            Cost = 100,
            ImagePath = "images/decorations/candle.svg",
            IsAvailable = true,
            UnlockLevel = 1,
            IsFreeTier = true
        };

        yield return new ShopItem
        {
            Id = _mugId,
            ItemType = ShopItemType.Decoration,
            Name = "Cosy Book Mug",
            Description = "A ceramic mug with a book-spine pattern. Always full.",
            Cost = 120,
            ImagePath = "images/decorations/mug.svg",
            IsAvailable = true,
            UnlockLevel = 1,
            IsFreeTier = true
        };

        yield return new ShopItem
        {
            Id = _bookendWoodId,
            ItemType = ShopItemType.Decoration,
            Name = "Wooden Bookend",
            Description = "A hand-carved oak bookend. Your books deserve a proper brace.",
            Cost = 150,
            ImagePath = "images/decorations/bookend_wood.svg",
            IsAvailable = true,
            UnlockLevel = 1
        };

        yield return new ShopItem
        {
            Id = _hourglassId,
            ItemType = ShopItemType.Decoration,
            Name = "Brass Hourglass",
            Description = "Measure your reading sessions in style.",
            Cost = 175,
            ImagePath = "images/decorations/hourglass.svg",
            IsAvailable = true,
            UnlockLevel = 5,
            SlotWidth = 2
        };

        yield return new ShopItem
        {
            Id = _spectaclesId,
            ItemType = ShopItemType.Decoration,
            Name = "Scholar's Spectacles",
            Description = "Round brass-rimmed glasses. Any shelf gains +10 distinguished.",
            Cost = 180,
            ImagePath = "images/decorations/spectacles.svg",
            IsAvailable = true,
            UnlockLevel = 5,
            SlotWidth = 2
        };

        yield return new ShopItem
        {
            Id = _inkwellId,
            ItemType = ShopItemType.Decoration,
            Name = "Inkwell & Quill",
            Description = "A glass inkwell with a raven feather quill. Timeless.",
            Cost = 200,
            ImagePath = "images/decorations/inkwell.svg",
            IsAvailable = true,
            UnlockLevel = 10,
            SlotWidth = 2
        };

        yield return new ShopItem
        {
            Id = _owlFigurineId,
            ItemType = ShopItemType.Decoration,
            Name = "Owl Figurine",
            Description = "A ceramic owl. Wise observer of your reading habits.",
            Cost = 220,
            ImagePath = "images/decorations/owl_figurine.svg",
            IsAvailable = true,
            UnlockLevel = 10,
            SlotWidth = 2,
            IsFreeTier = true
        };

        yield return new ShopItem
        {
            Id = _globeId,
            ItemType = ShopItemType.Decoration,
            Name = "Library Globe",
            Description = "A vintage brass globe. Explore worlds between the shelves.",
            Cost = 260,
            ImagePath = "images/decorations/globe.svg",
            IsAvailable = true,
            UnlockLevel = 15,
            SlotWidth = 2
        };

        yield return new ShopItem
        {
            Id = _bookendMarbleId,
            ItemType = ShopItemType.Decoration,
            Name = "Marble Bookend",
            Description = "Carved marble bookends — heavy, elegant, permanent.",
            Cost = 280,
            ImagePath = "images/decorations/bookend_marble.svg",
            IsAvailable = true,
            UnlockLevel = 15,
            SlotWidth = 2
        };

        yield return new ShopItem
        {
            Id = _telescopeId,
            ItemType = ShopItemType.Decoration,
            Name = "Brass Telescope",
            Description = "A miniature brass telescope. For reading between the stars.",
            Cost = 320,
            ImagePath = "images/decorations/telescope.svg",
            IsAvailable = true,
            UnlockLevel = 20,
            SlotWidth = 2
        };

        yield return new ShopItem
        {
            Id = _magicLampId,
            ItemType = ShopItemType.Decoration,
            Name = "Magic Reading Lamp",
            Description = "An enchanted desk lamp that never runs out of oil.",
            Cost = 340,
            ImagePath = "images/decorations/magic_lamp.svg",
            IsAvailable = true,
            UnlockLevel = 20,
            SlotWidth = 2
        };

        yield return new ShopItem
        {
            Id = _dragonFigurineId,
            ItemType = ShopItemType.Decoration,
            Name = "Dragon Figurine",
            Description = "A tiny dragon perched on your shelf. He's read everything.",
            Cost = 360,
            ImagePath = "images/decorations/dragon_figurine.svg",
            IsAvailable = true,
            UnlockLevel = 25,
            SlotWidth = 2
        };

        yield return new ShopItem
        {
            Id = _alchemyFlaskId,
            ItemType = ShopItemType.Decoration,
            Name = "Alchemy Flask",
            Description = "A glowing green flask of unknown purpose. Do not drink.",
            Cost = 380,
            ImagePath = "images/decorations/alchemy_flask.svg",
            IsAvailable = true,
            UnlockLevel = 25,
            SlotWidth = 2
        };

        yield return new ShopItem
        {
            Id = _ancientScrollId,
            ItemType = ShopItemType.Decoration,
            Name = "Ancient Scroll",
            Description = "A sealed scroll of immense wisdom. Or a grocery list. Who knows.",
            Cost = 400,
            ImagePath = "images/decorations/ancient_scroll.svg",
            IsAvailable = true,
            UnlockLevel = 25,
            SlotWidth = 2
        };

        yield return new ShopItem
        {
            Id = _storyHeartId,
            ItemType = ShopItemType.Decoration,
            Name = "Heart of Stories",
            Description = "The heart of the library — a pulsing relic in warm beige. The ultimate reward for every reader who never gives up. Its magic permeates every aspect of your journey.",
            Cost = 200000,
            ImagePath = "images/decorations/heart_of_stories.svg",
            IsAvailable = true,
            UnlockLevel = 70,
            SlotWidth = 2,
            SpecialAbilityKey = SpecialAbilityKeys.StoryHeart,
            IsSingleton = true,
            IsUltimateTier = true
        };
    }
}
