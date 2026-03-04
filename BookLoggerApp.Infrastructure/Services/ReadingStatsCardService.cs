using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;
using SkiaSharp;

namespace BookLoggerApp.Infrastructure.Services;

/// <summary>
/// Generates shareable monthly reading stats card images using SkiaSharp.
/// Renders a visually appealing card matching the BookHeart cozy brown theme.
/// </summary>
public class ReadingStatsCardService : IReadingStatsCardService
{
    private const int CardWidth = 1080;
    private const int CardHeight = 1920;

    // BookHeart theme colors
    private static readonly SKColor BgPrimary = SKColor.Parse("#1A1410");
    private static readonly SKColor BgSecondary = SKColor.Parse("#2D2419");
    private static readonly SKColor BgCard = SKColor.Parse("#352C21");
    private static readonly SKColor AccentGold = SKColor.Parse("#D4A574");
    private static readonly SKColor TextPrimary = SKColor.Parse("#F5E6D3");
    private static readonly SKColor TextSecondary = SKColor.Parse("#C9B5A0");
    private static readonly SKColor XpGold = SKColor.Parse("#FFC107");
    private static readonly SKColor StreakOrange = SKColor.Parse("#FF9800");
    private static readonly SKColor GenreGreen = SKColor.Parse("#4CAF50");

    public Task<string> GenerateMonthlyCardAsync(MonthlyReadingStats stats, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        string filePath = Path.Combine(Path.GetTempPath(), $"reading_stats_{stats.Year}_{stats.Month:D2}.png");

        using var surface = SKSurface.Create(new SKImageInfo(CardWidth, CardHeight));
        var canvas = surface.Canvas;

        DrawBackground(canvas);
        DrawHeader(canvas, stats);
        DrawStatsGrid(canvas, stats);
        DrawBottomBranding(canvas);

        // Encode to PNG and save
        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.OpenWrite(filePath);
        data.SaveTo(stream);

        return Task.FromResult(filePath);
    }

    private static void DrawBackground(SKCanvas canvas)
    {
        // Gradient background
        using var bgPaint = new SKPaint();
        bgPaint.Shader = SKShader.CreateLinearGradient(
            new SKPoint(0, 0),
            new SKPoint(CardWidth, CardHeight),
            new[] { BgPrimary, BgSecondary, BgPrimary },
            new float[] { 0f, 0.5f, 1f },
            SKShaderTileMode.Clamp);
        canvas.DrawRect(0, 0, CardWidth, CardHeight, bgPaint);

        // Subtle decorative circles
        using var circlePaint = new SKPaint
        {
            Color = AccentGold.WithAlpha(15),
            IsAntialias = true
        };
        canvas.DrawCircle(-100, 200, 400, circlePaint);
        canvas.DrawCircle(CardWidth + 80, 600, 350, circlePaint);
        canvas.DrawCircle(200, CardHeight - 200, 300, circlePaint);
    }

    private static void DrawHeader(SKCanvas canvas, MonthlyReadingStats stats)
    {
        float y = 120;

        // App branding
        using var brandPaint = CreateTextPaint(TextSecondary, 36, SKFontStyleWeight.Normal);
        DrawCenteredText(canvas, "BookHeart", CardWidth / 2f, y, brandPaint);
        y += 70;

        // Decorative line
        using var linePaint = new SKPaint
        {
            Color = AccentGold.WithAlpha(80),
            StrokeWidth = 2,
            IsAntialias = true
        };
        canvas.DrawLine(CardWidth * 0.2f, y, CardWidth * 0.8f, y, linePaint);
        y += 60;

        // Main title: "Mein Lese-Monat"
        using var titlePaint = CreateTextPaint(AccentGold, 64, SKFontStyleWeight.Bold);
        DrawCenteredText(canvas, "Mein Lese-Monat", CardWidth / 2f, y, titlePaint);
        y += 80;

        // Month name (large)
        using var monthPaint = CreateTextPaint(TextPrimary, 80, SKFontStyleWeight.Bold);
        DrawCenteredText(canvas, stats.MonthNameGerman, CardWidth / 2f, y, monthPaint);
        y += 50;

        // Year
        using var yearPaint = CreateTextPaint(TextSecondary, 40, SKFontStyleWeight.Normal);
        DrawCenteredText(canvas, stats.Year.ToString(), CardWidth / 2f, y, yearPaint);
        y += 60;

        // Decorative line
        canvas.DrawLine(CardWidth * 0.2f, y, CardWidth * 0.8f, y, linePaint);
    }

    private static void DrawStatsGrid(SKCanvas canvas, MonthlyReadingStats stats)
    {
        float startY = 620;
        float cardMargin = 60;
        float cardWidth = (CardWidth - cardMargin * 3) / 2f;
        float cardHeight = 220;
        float gap = 30;

        // Row 1: Books + Pages
        DrawStatCard(canvas, cardMargin, startY, cardWidth, cardHeight,
            "📚", "Bücher", stats.BooksCompleted.ToString(), AccentGold);
        DrawStatCard(canvas, cardMargin * 2 + cardWidth, startY, cardWidth, cardHeight,
            "📄", "Seiten", FormatNumber(stats.PagesRead), TextPrimary);

        // Row 2: Time + Streak
        float row2Y = startY + cardHeight + gap;
        DrawStatCard(canvas, cardMargin, row2Y, cardWidth, cardHeight,
            "⏱️", "Lesezeit", FormatMinutesGerman(stats.MinutesRead), TextPrimary);
        DrawStatCard(canvas, cardMargin * 2 + cardWidth, row2Y, cardWidth, cardHeight,
            "🔥", "Streak", $"{stats.CurrentStreak} Tage", StreakOrange);

        // Row 3: Rating + Genre
        float row3Y = row2Y + cardHeight + gap;
        string ratingText = stats.AverageRating > 0 ? $"{stats.AverageRating:F1} ★" : "—";
        DrawStatCard(canvas, cardMargin, row3Y, cardWidth, cardHeight,
            "⭐", "Bewertung", ratingText, XpGold);
        string genreText = stats.FavoriteGenre ?? "—";
        DrawStatCard(canvas, cardMargin * 2 + cardWidth, row3Y, cardWidth, cardHeight,
            "🎭", "Top-Genre", genreText, GenreGreen);

        // Row 4: Full-width level/XP card
        float row4Y = row3Y + cardHeight + gap;
        float fullWidth = CardWidth - cardMargin * 2;
        DrawLevelCard(canvas, cardMargin, row4Y, fullWidth, 200, stats);
    }

    private static void DrawStatCard(SKCanvas canvas, float x, float y, float width, float height,
        string emoji, string label, string value, SKColor accentColor)
    {
        // Card background with rounded corners
        using var cardPaint = new SKPaint
        {
            Color = BgCard,
            IsAntialias = true
        };
        var cardRect = new SKRoundRect(new SKRect(x, y, x + width, y + height), 24);
        canvas.DrawRoundRect(cardRect, cardPaint);

        // Subtle border
        using var borderPaint = new SKPaint
        {
            Color = accentColor.WithAlpha(40),
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2
        };
        canvas.DrawRoundRect(cardRect, borderPaint);

        float centerX = x + width / 2f;
        float currentY = y + 45;

        // Emoji
        using var emojiPaint = CreateTextPaint(TextPrimary, 48, SKFontStyleWeight.Normal);
        DrawCenteredText(canvas, emoji, centerX, currentY, emojiPaint);
        currentY += 55;

        // Value (large, accented)
        using var valuePaint = CreateTextPaint(accentColor, 44, SKFontStyleWeight.Bold);
        // Truncate long values to fit the card
        string displayValue = value.Length > 12 ? value[..11] + "…" : value;
        DrawCenteredText(canvas, displayValue, centerX, currentY, valuePaint);
        currentY += 50;

        // Label
        using var labelPaint = CreateTextPaint(TextSecondary, 30, SKFontStyleWeight.Normal);
        DrawCenteredText(canvas, label, centerX, currentY, labelPaint);
    }

    private static void DrawLevelCard(SKCanvas canvas, float x, float y, float width, float height,
        MonthlyReadingStats stats)
    {
        // Card background
        using var cardPaint = new SKPaint
        {
            Color = BgCard,
            IsAntialias = true
        };
        var cardRect = new SKRoundRect(new SKRect(x, y, x + width, y + height), 24);
        canvas.DrawRoundRect(cardRect, cardPaint);

        // Gold accent border for level card
        using var borderPaint = new SKPaint
        {
            Color = XpGold.WithAlpha(60),
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2
        };
        canvas.DrawRoundRect(cardRect, borderPaint);

        // Level badge circle
        float badgeX = x + 100;
        float badgeY = y + height / 2f;
        float badgeRadius = 50;

        using var badgePaint = new SKPaint
        {
            IsAntialias = true
        };
        badgePaint.Shader = SKShader.CreateLinearGradient(
            new SKPoint(badgeX - badgeRadius, badgeY - badgeRadius),
            new SKPoint(badgeX + badgeRadius, badgeY + badgeRadius),
            new[] { XpGold, AccentGold },
            null,
            SKShaderTileMode.Clamp);
        canvas.DrawCircle(badgeX, badgeY, badgeRadius, badgePaint);

        // Level number inside badge
        using var levelNumPaint = CreateTextPaint(BgPrimary, 40, SKFontStyleWeight.Bold);
        DrawCenteredText(canvas, stats.CurrentLevel.ToString(), badgeX, badgeY + 14, levelNumPaint);

        // "LEVEL" text below number
        using var levelLabelPaint = CreateTextPaint(BgPrimary, 16, SKFontStyleWeight.Bold);
        DrawCenteredText(canvas, "LEVEL", badgeX, badgeY + 34, levelLabelPaint);

        // XP info to the right
        float textX = x + 200;
        float textY = y + 70;

        using var xpTitlePaint = CreateTextPaint(XpGold, 36, SKFontStyleWeight.Bold);
        canvas.DrawText($"Level {stats.CurrentLevel}", textX, textY, xpTitlePaint);
        textY += 50;

        using var xpValuePaint = CreateTextPaint(TextSecondary, 30, SKFontStyleWeight.Normal);
        canvas.DrawText($"{FormatNumber(stats.TotalXp)} XP gesammelt", textX, textY, xpValuePaint);
    }

    private static void DrawBottomBranding(SKCanvas canvas)
    {
        float y = CardHeight - 100;

        // Decorative line
        using var linePaint = new SKPaint
        {
            Color = AccentGold.WithAlpha(50),
            StrokeWidth = 1,
            IsAntialias = true
        };
        canvas.DrawLine(CardWidth * 0.3f, y, CardWidth * 0.7f, y, linePaint);
        y += 50;

        using var footerPaint = CreateTextPaint(TextSecondary.WithAlpha(120), 28, SKFontStyleWeight.Normal);
        DrawCenteredText(canvas, "Erstellt mit BookHeart 📖", CardWidth / 2f, y, footerPaint);
    }

    // --- Helpers ---

    private static SKPaint CreateTextPaint(SKColor color, float textSize, SKFontStyleWeight weight)
    {
        return new SKPaint
        {
            Color = color,
            TextSize = textSize,
            IsAntialias = true,
            Typeface = SKTypeface.FromFamilyName("sans-serif", weight, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright),
            TextAlign = SKTextAlign.Left
        };
    }

    private static void DrawCenteredText(SKCanvas canvas, string text, float centerX, float y, SKPaint paint)
    {
        float textWidth = paint.MeasureText(text);
        canvas.DrawText(text, centerX - textWidth / 2f, y, paint);
    }

    private static string FormatMinutesGerman(int minutes)
    {
        if (minutes < 60) return $"{minutes} Min";
        int hours = minutes / 60;
        int remaining = minutes % 60;
        if (remaining == 0) return $"{hours} Std";
        return $"{hours}h {remaining}m";
    }

    private static string FormatNumber(int number)
    {
        return number >= 10000 ? $"{number / 1000}k" : number.ToString("N0");
    }
}
