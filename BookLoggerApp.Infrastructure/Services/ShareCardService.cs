using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;
using SkiaSharp;

namespace BookLoggerApp.Infrastructure.Services;

/// <summary>
/// Generates shareable PNG cards using SkiaSharp.
/// Stats card: 1080x1920 (Instagram Story format).
/// Book card: 1080x1680.
/// </summary>
public class ShareCardService : IShareCardService
{
    // === Palette (matches app.css CSS variables) ===
    private static readonly SKColor BgPrimary     = SKColor.Parse("#1A1410");
    private static readonly SKColor BgSecondary   = SKColor.Parse("#2D2419");
    private static readonly SKColor BgTertiary    = SKColor.Parse("#3D3126");
    private static readonly SKColor Primary       = SKColor.Parse("#D4A574");
    private static readonly SKColor TextPrimary   = SKColor.Parse("#F5E6D3");
    private static readonly SKColor TextSecondary = SKColor.Parse("#C9B5A0");
    private static readonly SKColor BorderColor   = SKColor.Parse("#4A3F32");
    private static readonly SKColor StatusCompleted = SKColor.Parse("#88A67E");

    public Task<byte[]> GenerateStatsCardAsync(StatsShareData data, CancellationToken ct = default)
    {
        const int Width = 1080;
        const int Height = 1920;

        using var bitmap = new SKBitmap(Width, Height);
        using var canvas = new SKCanvas(bitmap);

        canvas.Clear(BgPrimary);
        DrawStatsCard(canvas, data, Width, Height);

        return Task.FromResult(EncodePng(bitmap));
    }

    public Task<byte[]> GenerateBookCardAsync(BookShareData data, CancellationToken ct = default)
    {
        const int Width = 1080;
        const int Height = 1680;

        using var bitmap = new SKBitmap(Width, Height);
        using var canvas = new SKCanvas(bitmap);

        canvas.Clear(BgPrimary);
        DrawBookCard(canvas, data, Width, Height);

        return Task.FromResult(EncodePng(bitmap));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Stats Card
    // ─────────────────────────────────────────────────────────────────────────

    private static void DrawStatsCard(SKCanvas canvas, StatsShareData data, int w, int h)
    {
        const int Pad = 60;

        // Subtle radial gradient overlay for depth
        using (var gradPaint = new SKPaint())
        {
            var shader = SKShader.CreateRadialGradient(
                new SKPoint(w / 2f, 350),
                700,
                new[] { BgSecondary.WithAlpha(200), BgPrimary.WithAlpha(0) },
                null,
                SKShaderTileMode.Clamp);
            gradPaint.Shader = shader;
            canvas.DrawRect(SKRect.Create(0, 0, w, h), gradPaint);
        }

        // ── App name ──────────────────────────────────────────────────────────
        using var boldTypeface = SKTypeface.FromFamilyName(null, SKFontStyle.Bold);
        using var regularTypeface = SKTypeface.Default;

        float y = 120;

        DrawText(canvas, "♥ BookHeart", w / 2f, y, boldTypeface, 72, Primary, SKTextAlign.Center);
        y += 90;
        DrawText(canvas, "Reading Recap", w / 2f, y, regularTypeface, 52, TextSecondary, SKTextAlign.Center);
        y += 70;

        // Period chip
        string periodText = data.PeriodLabel;
        DrawChip(canvas, periodText, w / 2f, y, BgTertiary, Primary, boldTypeface, 44);
        y += 80;

        // Divider
        DrawHorizontalLine(canvas, Pad, w - Pad, y, BorderColor);
        y += 50;

        // ── 2x2 stat tiles ───────────────────────────────────────────────────
        const float TileW = 460;
        const float TileH = 180;
        const float TileGap = 40;

        float col1X = Pad;
        float col2X = Pad + TileW + TileGap;
        float row1Y = y;
        float row2Y = y + TileH + TileGap;

        // Hours display
        string hoursText;
        if (data.MinutesRead < 60)
            hoursText = $"{data.MinutesRead}m";
        else
            hoursText = $"{(data.MinutesRead / 60.0):F1}h";

        DrawStatTile(canvas, data.BooksCompleted.ToString(), "Books Read", col1X, row1Y, TileW, TileH, boldTypeface, regularTypeface);
        DrawStatTile(canvas, data.PagesRead.ToString("N0"), "Pages Read", col2X, row1Y, TileW, TileH, boldTypeface, regularTypeface);
        DrawStatTile(canvas, hoursText, "Hours Read", col1X, row2Y, TileW, TileH, boldTypeface, regularTypeface);
        DrawStatTile(canvas, data.FavoriteGenre ?? "–", "Fav. Genre", col2X, row2Y, TileW, TileH, boldTypeface, regularTypeface, valueFontSize: 44);

        y = row2Y + TileH + 60;

        // ── Top Books ─────────────────────────────────────────────────────────
        DrawHorizontalLine(canvas, Pad, w - Pad, y, BorderColor);
        y += 50;

        DrawText(canvas, "Top Books", Pad, y, boldTypeface, 52, Primary, SKTextAlign.Left);
        y += 70;

        for (int i = 0; i < data.TopBooks.Count && i < 3; i++)
        {
            var (title, author) = data.TopBooks[i];
            DrawBookRow(canvas, i + 1, TruncateText(title, 28), author, Pad, y, w - Pad * 2, regularTypeface, boldTypeface);
            y += 140;
        }

        // ── Watermark ─────────────────────────────────────────────────────────
        DrawWatermark(canvas, w, h, boldTypeface, regularTypeface);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Book Card
    // ─────────────────────────────────────────────────────────────────────────

    private static void DrawBookCard(SKCanvas canvas, BookShareData data, int w, int h)
    {
        const int Pad = 60;

        using var boldTypeface = SKTypeface.FromFamilyName(null, SKFontStyle.Bold);
        using var regularTypeface = SKTypeface.Default;

        float y = 80;

        // ── Header ────────────────────────────────────────────────────────────
        DrawText(canvas, "Just finished reading!", w / 2f, y, regularTypeface, 44, TextSecondary, SKTextAlign.Center);
        y += 60;
        DrawText(canvas, "♥ BookHeart", w / 2f, y, boldTypeface, 52, Primary, SKTextAlign.Center);
        y += 80;

        // ── Cover image ──────────────────────────────────────────────────────
        const float CoverW = 320;
        const float CoverH = 480;
        float coverX = (w - CoverW) / 2f;
        float coverY = y;

        if (data.CoverImageBytes != null && data.CoverImageBytes.Length > 0)
        {
            using var coverBitmap = SKBitmap.Decode(data.CoverImageBytes);
            if (coverBitmap != null)
            {
                DrawRoundedBitmap(canvas, coverBitmap, coverX, coverY, CoverW, CoverH, 16);
            }
            else
            {
                DrawCoverPlaceholder(canvas, data.Title, coverX, coverY, CoverW, CoverH, boldTypeface);
            }
        }
        else
        {
            DrawCoverPlaceholder(canvas, data.Title, coverX, coverY, CoverW, CoverH, boldTypeface);
        }

        y = coverY + CoverH + 50;

        // ── Title & Author ────────────────────────────────────────────────────
        var titleLines = WrapText(data.Title, w - Pad * 2, boldTypeface, 68);
        foreach (var line in titleLines.Take(2))
        {
            DrawText(canvas, line, w / 2f, y, boldTypeface, 68, TextPrimary, SKTextAlign.Center);
            y += 80;
        }

        DrawText(canvas, $"by {data.Author}", w / 2f, y, regularTypeface, 42, TextSecondary, SKTextAlign.Center);
        y += 70;

        // ── Stats chips ───────────────────────────────────────────────────────
        string pagesText = data.PageCount.HasValue ? $"📄 {data.PageCount:N0} pages" : "📄 –";
        string timeText = FormatReadingTime(data.TotalMinutesRead);

        float chipY = y;
        DrawInfoChip(canvas, pagesText, Pad, chipY, (w / 2f) - Pad - 20, 70, regularTypeface, 38);
        DrawInfoChip(canvas, timeText, w / 2f + 20, chipY, (w / 2f) - Pad - 20, 70, regularTypeface, 38);
        y += 100;

        // ── Star rating ───────────────────────────────────────────────────────
        if (data.AverageRating.HasValue)
        {
            DrawStarRating(canvas, data.AverageRating.Value, w / 2f, y, 52);
            y += 80;
        }

        // ── Category ratings grid ─────────────────────────────────────────────
        var ratedCategories = data.CategoryRatings
            .Where(kvp => kvp.Value.HasValue)
            .ToList();

        if (ratedCategories.Count > 0)
        {
            y += 10;
            DrawCategoryRatings(canvas, ratedCategories, Pad, y, w - Pad * 2, regularTypeface, boldTypeface);
        }

        // ── Watermark ─────────────────────────────────────────────────────────
        DrawWatermark(canvas, w, h, boldTypeface, regularTypeface);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Drawing helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static void DrawText(SKCanvas canvas, string text, float x, float y, SKTypeface typeface,
        float fontSize, SKColor color, SKTextAlign align)
    {
        using var font = new SKFont(typeface, fontSize);
        using var paint = new SKPaint { Color = color, IsAntialias = true };
        canvas.DrawText(text, x, y, align, font, paint);
    }

    private static void DrawHorizontalLine(SKCanvas canvas, float x1, float x2, float y, SKColor color)
    {
        using var paint = new SKPaint { Color = color, StrokeWidth = 1.5f, IsAntialias = true };
        canvas.DrawLine(x1, y, x2, y, paint);
    }

    private static void DrawChip(SKCanvas canvas, string text, float centerX, float y,
        SKColor bgColor, SKColor textColor, SKTypeface typeface, float fontSize)
    {
        using var font = new SKFont(typeface, fontSize);
        using var measurePaint = new SKPaint { IsAntialias = true };
        float textW = font.MeasureText(text, measurePaint);
        float chipW = textW + 60;
        float chipH = fontSize + 30;

        var chipRect = new SKRect(centerX - chipW / 2f, y - chipH / 2f + 10, centerX + chipW / 2f, y + chipH / 2f + 10);

        using var bgPaint = new SKPaint { Color = bgColor, IsAntialias = true };
        canvas.DrawRoundRect(chipRect, 12, 12, bgPaint);

        using var textPaint = new SKPaint { Color = textColor, IsAntialias = true };
        canvas.DrawText(text, centerX, y + fontSize / 3f + 10, SKTextAlign.Center, font, textPaint);
    }

    private static void DrawStatTile(SKCanvas canvas, string value, string label, float x, float y,
        float w, float h, SKTypeface boldTypeface, SKTypeface regularTypeface, float valueFontSize = 80)
    {
        using var bgPaint = new SKPaint { Color = BgSecondary, IsAntialias = true };
        canvas.DrawRoundRect(new SKRect(x, y, x + w, y + h), 16, 16, bgPaint);

        float midX = x + w / 2f;

        // Value
        using var valueFont = new SKFont(boldTypeface, valueFontSize);
        using var valuePaint = new SKPaint { Color = Primary, IsAntialias = true };
        canvas.DrawText(value, midX, y + h * 0.52f, SKTextAlign.Center, valueFont, valuePaint);

        // Label
        using var labelFont = new SKFont(regularTypeface, 32);
        using var labelPaint = new SKPaint { Color = TextSecondary, IsAntialias = true };
        canvas.DrawText(label, midX, y + h * 0.82f, SKTextAlign.Center, labelFont, labelPaint);
    }

    private static void DrawBookRow(SKCanvas canvas, int rank, string title, string author,
        float x, float y, float width, SKTypeface regularTypeface, SKTypeface boldTypeface)
    {
        const float CircleR = 30;
        float circleX = x + CircleR + 10;
        float circleY = y + 50;

        // Rank circle
        using var circleBg = new SKPaint { Color = BgTertiary, IsAntialias = true };
        canvas.DrawCircle(circleX, circleY, CircleR, circleBg);

        using var rankFont = new SKFont(boldTypeface, 36);
        using var rankPaint = new SKPaint { Color = Primary, IsAntialias = true };
        canvas.DrawText(rank.ToString(), circleX, circleY + 13, SKTextAlign.Center, rankFont, rankPaint);

        // Title
        float textX = circleX + CircleR + 20;
        using var titleFont = new SKFont(boldTypeface, 42);
        using var titlePaint = new SKPaint { Color = TextPrimary, IsAntialias = true };
        canvas.DrawText(title, textX, y + 40, SKTextAlign.Left, titleFont, titlePaint);

        // Author
        using var authorFont = new SKFont(regularTypeface, 32);
        using var authorPaint = new SKPaint { Color = TextSecondary, IsAntialias = true };
        canvas.DrawText(author, textX, y + 90, SKTextAlign.Left, authorFont, authorPaint);
    }

    private static void DrawRoundedBitmap(SKCanvas canvas, SKBitmap bitmap,
        float x, float y, float w, float h, float cornerRadius)
    {
        var destRect = new SKRect(x, y, x + w, y + h);

        canvas.Save();
        using var rrect = new SKRoundRect(destRect, cornerRadius);
        canvas.ClipRoundRect(rrect, SKClipOperation.Intersect, true);
        canvas.DrawBitmap(bitmap, destRect);
        canvas.Restore();
    }

    private static void DrawCoverPlaceholder(SKCanvas canvas, string title,
        float x, float y, float w, float h, SKTypeface boldTypeface)
    {
        var rect = new SKRect(x, y, x + w, y + h);
        using var bgPaint = new SKPaint { Color = BgTertiary, IsAntialias = true };
        canvas.DrawRoundRect(rect, 16, 16, bgPaint);

        string letter = string.IsNullOrEmpty(title) ? "?" : title[0].ToString().ToUpperInvariant();
        using var font = new SKFont(boldTypeface, 120);
        using var paint = new SKPaint { Color = Primary, IsAntialias = true };
        canvas.DrawText(letter, x + w / 2f, y + h / 2f + 40, SKTextAlign.Center, font, paint);
    }

    private static void DrawInfoChip(SKCanvas canvas, string text, float x, float y,
        float maxW, float chipH, SKTypeface typeface, float fontSize)
    {
        var rect = new SKRect(x, y, x + maxW, y + chipH);
        using var bgPaint = new SKPaint { Color = BgSecondary, IsAntialias = true };
        canvas.DrawRoundRect(rect, 14, 14, bgPaint);

        using var font = new SKFont(typeface, fontSize);
        using var paint = new SKPaint { Color = TextSecondary, IsAntialias = true };
        canvas.DrawText(text, x + maxW / 2f, y + chipH / 2f + fontSize / 3f, SKTextAlign.Center, font, paint);
    }

    private static void DrawStarRating(SKCanvas canvas, double rating, float centerX, float y, float fontSize)
    {
        int filled = (int)Math.Round(rating);
        filled = Math.Clamp(filled, 0, 5);

        using var starFont = new SKFont(SKTypeface.Default, fontSize);

        const float StarSpacing = 70;
        float totalW = 5 * StarSpacing;
        float startX = centerX - totalW / 2f + StarSpacing / 2f;

        for (int i = 0; i < 5; i++)
        {
            string glyph = i < filled ? "★" : "☆";
            var color = i < filled ? Primary : BgTertiary.WithAlpha(180);
            using var paint = new SKPaint { Color = color, IsAntialias = true };
            canvas.DrawText(glyph, startX + i * StarSpacing, y + fontSize / 3f, SKTextAlign.Center, starFont, paint);
        }
    }

    private static void DrawCategoryRatings(SKCanvas canvas,
        List<KeyValuePair<RatingCategory, int?>> categories,
        float x, float y, float totalW, SKTypeface regularTypeface, SKTypeface boldTypeface)
    {
        const int Cols = 3;
        float cellW = (totalW - 20f * (Cols - 1)) / Cols;
        const float CellH = 90;
        const float Gap = 20;

        for (int i = 0; i < categories.Count; i++)
        {
            int col = i % Cols;
            int row = i / Cols;
            float cellX = x + col * (cellW + Gap);
            float cellY = y + row * (CellH + Gap);

            var (category, rating) = (categories[i].Key, categories[i].Value);

            var rect = new SKRect(cellX, cellY, cellX + cellW, cellY + CellH);
            using var bgPaint = new SKPaint { Color = BgTertiary, IsAntialias = true };
            canvas.DrawRoundRect(rect, 12, 12, bgPaint);

            string label = CategoryLabel(category);
            string value = rating.HasValue ? $"{rating}/5" : "–";

            using var labelFont = new SKFont(regularTypeface, 30);
            using var labelPaint = new SKPaint { Color = TextSecondary, IsAntialias = true };
            canvas.DrawText(label, cellX + cellW / 2f, cellY + 38, SKTextAlign.Center, labelFont, labelPaint);

            using var valFont = new SKFont(boldTypeface, 36);
            using var valPaint = new SKPaint { Color = Primary, IsAntialias = true };
            canvas.DrawText(value, cellX + cellW / 2f, cellY + 74, SKTextAlign.Center, valFont, valPaint);
        }
    }

    private static void DrawWatermark(SKCanvas canvas, int w, int h,
        SKTypeface boldTypeface, SKTypeface regularTypeface)
    {
        float y = h - 120;

        using var nameFont = new SKFont(boldTypeface, 40);
        using var namePaint = new SKPaint { Color = Primary.WithAlpha(128), IsAntialias = true };
        canvas.DrawText("♥ BookHeart", w / 2f, y, SKTextAlign.Center, nameFont, namePaint);

        using var tagFont = new SKFont(regularTypeface, 28);
        using var tagPaint = new SKPaint { Color = TextSecondary.WithAlpha(100), IsAntialias = true };
        canvas.DrawText("Track your reading journey", w / 2f, y + 40, SKTextAlign.Center, tagFont, tagPaint);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Utility helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static byte[] EncodePng(SKBitmap bitmap)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static string TruncateText(string text, int maxChars)
    {
        if (text.Length <= maxChars) return text;
        return text[..(maxChars - 1)] + "…";
    }

    private static List<string> WrapText(string text, float maxWidth, SKTypeface typeface, float fontSize)
    {
        using var font = new SKFont(typeface, fontSize);
        using var paint = new SKPaint { IsAntialias = true };

        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var lines = new List<string>();
        var current = string.Empty;

        foreach (var word in words)
        {
            var candidate = string.IsNullOrEmpty(current) ? word : $"{current} {word}";
            if (font.MeasureText(candidate, paint) <= maxWidth)
            {
                current = candidate;
            }
            else
            {
                if (!string.IsNullOrEmpty(current))
                    lines.Add(current);
                current = word;
            }
        }

        if (!string.IsNullOrEmpty(current))
            lines.Add(current);

        return lines;
    }

    private static string FormatReadingTime(int minutes)
    {
        if (minutes <= 0) return "⏱ –";
        if (minutes < 60) return $"⏱ {minutes}m";
        int h = minutes / 60;
        int m = minutes % 60;
        return m > 0 ? $"⏱ {h}h {m}m" : $"⏱ {h}h";
    }

    private static string CategoryLabel(RatingCategory category) => category switch
    {
        RatingCategory.Characters   => "Characters",
        RatingCategory.Plot         => "Plot",
        RatingCategory.WritingStyle => "Writing",
        RatingCategory.SpiceLevel   => "Spice",
        RatingCategory.Pacing       => "Pacing",
        RatingCategory.WorldBuilding => "World",
        _ => category.ToString()
    };
}
