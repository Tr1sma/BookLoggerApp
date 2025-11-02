using Microsoft.EntityFrameworkCore;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Infrastructure.Data;

namespace BookLoggerApp.Infrastructure.Services;

/// <summary>
/// Provider implementation for app settings.
/// </summary>
public class AppSettingsProvider : IAppSettingsProvider
{
    private readonly AppDbContext _context;

    public AppSettingsProvider(AppDbContext context)
    {
        _context = context;
    }

    public async Task<AppSettings> GetSettingsAsync(CancellationToken ct = default)
    {
        var settings = await _context.AppSettings.FirstOrDefaultAsync(ct);

        if (settings == null)
        {
            // Create default settings if none exist
            settings = new AppSettings
            {
                Theme = "Light",
                Language = "en",
                UserLevel = 1,
                TotalXp = 0,
                Coins = 100 // Start with 100 coins
            };

            _context.AppSettings.Add(settings);
            await _context.SaveChangesAsync(ct);
        }

        return settings;
    }

    public async Task UpdateSettingsAsync(AppSettings settings, CancellationToken ct = default)
    {
        settings.UpdatedAt = DateTime.UtcNow;
        _context.AppSettings.Update(settings);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<int> GetUserCoinsAsync(CancellationToken ct = default)
    {
        var settings = await GetSettingsAsync(ct);
        return settings.Coins;
    }

    public async Task<int> GetUserLevelAsync(CancellationToken ct = default)
    {
        var settings = await GetSettingsAsync(ct);
        return settings.UserLevel;
    }

    public async Task SpendCoinsAsync(int amount, CancellationToken ct = default)
    {
        var settings = await GetSettingsAsync(ct);

        if (settings.Coins < amount)
            throw new InvalidOperationException($"Not enough coins. Have {settings.Coins}, need {amount}");

        settings.Coins -= amount;
        await UpdateSettingsAsync(settings, ct);
    }

    public async Task AddCoinsAsync(int amount, CancellationToken ct = default)
    {
        var settings = await GetSettingsAsync(ct);
        settings.Coins += amount;
        await UpdateSettingsAsync(settings, ct);
    }
}
