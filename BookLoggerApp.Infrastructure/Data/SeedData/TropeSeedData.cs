using BookLoggerApp.Core.Models;

namespace BookLoggerApp.Infrastructure.Data.SeedData;

public static class TropeSeedData
{
    public static List<Trope> GetTropes()
    {
        var tropes = new List<Trope>();

        // Fantasy (00000000-0000-0000-0000-000000000003)
        var fantasyId = Guid.Parse("00000000-0000-0000-0000-000000000003");
        AddTrope(tropes, "The Chosen One", fantasyId);
        AddTrope(tropes, "Dark Lord", fantasyId);
        AddTrope(tropes, "Quest/Journey", fantasyId);
        AddTrope(tropes, "Magical Academy", fantasyId);
        AddTrope(tropes, "Found Family", fantasyId);
        AddTrope(tropes, "Prophecy", fantasyId);
        AddTrope(tropes, "Reluctant Hero", fantasyId);
        AddTrope(tropes, "Magic System", fantasyId);

        // SciFi (00000000-0000-0000-0000-000000000004)
        var sciFiId = Guid.Parse("00000000-0000-0000-0000-000000000004");
        AddTrope(tropes, "Space Opera", sciFiId);
        AddTrope(tropes, "First Contact", sciFiId);
        AddTrope(tropes, "Artificial Intelligence", sciFiId);
        AddTrope(tropes, "Dystopia", sciFiId);
        AddTrope(tropes, "Time Travel", sciFiId);
        AddTrope(tropes, "Cyberpunk", sciFiId);
        AddTrope(tropes, "Post-Apocalyptic", sciFiId);

        // Romance (00000000-0000-0000-0000-000000000006)
        var romanceId = Guid.Parse("00000000-0000-0000-0000-000000000006");
        AddTrope(tropes, "Enemies to Lovers", romanceId);
        AddTrope(tropes, "Friends to Lovers", romanceId);
        AddTrope(tropes, "Fake Dating", romanceId);
        AddTrope(tropes, "Second Chance", romanceId);
        AddTrope(tropes, "Slow Burn", romanceId);
        AddTrope(tropes, "Grumpy x Sunshine", romanceId);
        AddTrope(tropes, "One Bed", romanceId);
        AddTrope(tropes, "Love Triangle", romanceId);
        AddTrope(tropes, "Forbidden Love", romanceId);
        AddTrope(tropes, "Sport Romance", romanceId);
        AddTrope(tropes, "Age Gap", romanceId);

        // Mystery (00000000-0000-0000-0000-000000000005)
        var mysteryId = Guid.Parse("00000000-0000-0000-0000-000000000005");
        AddTrope(tropes, "Whodunit", mysteryId);
        AddTrope(tropes, "Locked Room", mysteryId);
        AddTrope(tropes, "Unreliable Narrator", mysteryId);
        AddTrope(tropes, "Red Herring", mysteryId);
        AddTrope(tropes, "Noir", mysteryId);
        AddTrope(tropes, "Cozy Mystery", mysteryId);

        // Dark Romance (00000000-0000-0000-0000-000000000009)
        var darkRomanceId = Guid.Parse("00000000-0000-0000-0000-000000000009");
        AddTrope(tropes, "Stalker", darkRomanceId);
        AddTrope(tropes, "Captive/Captor", darkRomanceId);
        AddTrope(tropes, "Morally Grey MC", darkRomanceId);
        AddTrope(tropes, "Bully Romance", darkRomanceId);
        AddTrope(tropes, "Mafia", darkRomanceId);
        AddTrope(tropes, "Obsessive Love", darkRomanceId);
        AddTrope(tropes, "Enemies to Lovers", darkRomanceId);

        // Thriller (00000000-0000-0000-0000-000000000012)
        var thrillerId = Guid.Parse("00000000-0000-0000-0000-000000000012");
        AddTrope(tropes, "Plot Twist", thrillerId);
        AddTrope(tropes, "Psychological", thrillerId);
        AddTrope(tropes, "Serial Killer", thrillerId);
        AddTrope(tropes, "Revenge", thrillerId);
        AddTrope(tropes, "Spy/Espionage", thrillerId);

        return tropes;
    }

    private static void AddTrope(List<Trope> tropes, string name, Guid genreId)
    {
        // Generate a deterministic GUID based on name and genreId
        // This ensures that the same trope will always have the same ID across migrations/databases
        var stringToHash = $"{name}:{genreId}";
        var hash = System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes(stringToHash));
        var id = new Guid(hash);

        tropes.Add(new Trope
        {
            Id = id,
            Name = name,
            GenreId = genreId
        });
    }
}
