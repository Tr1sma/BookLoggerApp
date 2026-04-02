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
    private static readonly SKColor BgPrimary      = SKColor.Parse("#1A1410");
    private static readonly SKColor BgSecondary    = SKColor.Parse("#2D2419");
    private static readonly SKColor BgTertiary     = SKColor.Parse("#3D3126");
    private static readonly SKColor Primary        = SKColor.Parse("#D4A574");
    private static readonly SKColor TextPrimary    = SKColor.Parse("#F5E6D3");
    private static readonly SKColor TextSecondary  = SKColor.Parse("#C9B5A0");
    private static readonly SKColor BorderColor    = SKColor.Parse("#4A3F32");

    // Tile accent colors — one per stat so cards have visual variety
    private static readonly SKColor AccentGreen  = SKColor.Parse("#88A67E");
    private static readonly SKColor AccentAmber  = SKColor.Parse("#D4A574");
    private static readonly SKColor AccentBlue   = SKColor.Parse("#7B9CB5");
    private static readonly SKColor AccentGold   = SKColor.Parse("#E8C97A");
    private static readonly SKColor AccentOrange = SKColor.Parse("#E07B54");
    private static readonly SKColor AccentRose   = SKColor.Parse("#B5907B");

    public Task<byte[]> GenerateStatsCardAsync(StatsShareData data, CancellationToken ct = default)
    {
        const int Width  = 1080;
        const int Height = 1920;

        using var bitmap = new SKBitmap(Width, Height);
        using var canvas = new SKCanvas(bitmap);

        canvas.Clear(BgPrimary);
        DrawStatsCard(canvas, data, Width, Height);

        return Task.FromResult(EncodePng(bitmap));
    }

    public Task<byte[]> GenerateBookCardAsync(BookShareData data, CancellationToken ct = default)
    {
        const int Width  = 1080;
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

        using var boldTypeface    = SKTypeface.FromFamilyName(null, SKFontStyle.Bold);
        using var regularTypeface = SKTypeface.Default;

        float y = 110;

        // ── App name: drawn heart icon + "BookHeart" text ─────────────────────
        DrawHeartAndText(canvas, "BookHeart", w / 2f, y, boldTypeface, 72, Primary);
        y += 80;
        DrawText(canvas, "Reading Recap", w / 2f, y, regularTypeface, 52, TextSecondary, SKTextAlign.Center);
        y += 70;

        // Period chip shows exactly which month/year is being shared
        DrawChip(canvas, data.PeriodLabel, w / 2f, y, BgTertiary, Primary, boldTypeface, 44);
        y += 80;

        DrawHorizontalLine(canvas, Pad, w - Pad, y, BorderColor);
        y += 50;

        // ── 3×2 stat tile grid ────────────────────────────────────────────────
        const float TileW   = 300;
        const float TileH   = 230;
        const float TileGap = 30;

        float col0X = Pad;
        float col1X = Pad + TileW + TileGap;
        float col2X = Pad + (TileW + TileGap) * 2;
        float row0Y = y;
        float row1Y = y + TileH + TileGap;

        string hoursText  = data.MinutesRead < 60
            ? $"{data.MinutesRead}m"
            : $"{(data.MinutesRead / 60.0):F1}h";
        string ratingText = data.AverageRating.HasValue ? $"{data.AverageRating.Value:F1}" : "–";
        string totalBooksText = data.TotalBooks > 0 ? data.TotalBooks.ToString() : "–";
        string genreText  = TruncateText(data.FavoriteGenre ?? "–", 12);

        // Row 0: Books / Pages / Hours
        DrawStatTile(canvas, data.BooksCompleted.ToString(), "Books Read",
            col0X, row0Y, TileW, TileH, boldTypeface, regularTypeface, AccentGreen);
        DrawStatTile(canvas, data.PagesRead.ToString("N0"), "Pages Read",
            col1X, row0Y, TileW, TileH, boldTypeface, regularTypeface, AccentAmber);
        DrawStatTile(canvas, hoursText, "Hours Read",
            col2X, row0Y, TileW, TileH, boldTypeface, regularTypeface, AccentBlue);

        // Row 1: Avg Rating / Day Streak / Fav. Genre
        DrawStatTile(canvas, ratingText, "Avg Rating",
            col0X, row1Y, TileW, TileH, boldTypeface, regularTypeface, AccentGold, valueFontSize: 80);
        DrawStatTile(canvas, totalBooksText, "Total Books",
            col1X, row1Y, TileW, TileH, boldTypeface, regularTypeface, AccentOrange);
        DrawStatTile(canvas, genreText, "Fav. Genre",
            col2X, row1Y, TileW, TileH, boldTypeface, regularTypeface, AccentRose, valueFontSize: 42);

        y = row1Y + TileH + 50;

        // ── Top Books ─────────────────────────────────────────────────────────
        DrawHorizontalLine(canvas, Pad, w - Pad, y, BorderColor);
        y += 50;
        DrawText(canvas, "Top Books", Pad, y, boldTypeface, 52, Primary, SKTextAlign.Left);
        y += 75;

        for (int i = 0; i < data.TopBooks.Count && i < 3; i++)
        {
            var (title, author, rating) = data.TopBooks[i];
            DrawBookRow(canvas, i + 1, TruncateText(title, 26), author, rating,
                Pad, y, w - Pad * 2, regularTypeface, boldTypeface);
            y += 180;
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

        using var boldTypeface    = SKTypeface.FromFamilyName(null, SKFontStyle.Bold);
        using var regularTypeface = SKTypeface.Default;

        float y = 80;

        // ── Header ────────────────────────────────────────────────────────────
        DrawText(canvas, "Just finished reading!", w / 2f, y, regularTypeface, 44, TextSecondary, SKTextAlign.Center);
        y += 60;
        DrawHeartAndText(canvas, "BookHeart", w / 2f, y, boldTypeface, 52, Primary);
        y += 80;

        // ── Cover image ───────────────────────────────────────────────────────
        const float CoverW = 320;
        const float CoverH = 480;
        float coverX = (w - CoverW) / 2f;
        float coverY = y;

        if (data.CoverImageBytes != null && data.CoverImageBytes.Length > 0)
        {
            using var coverBitmap = SKBitmap.Decode(data.CoverImageBytes);
            if (coverBitmap != null)
                DrawRoundedBitmap(canvas, coverBitmap, coverX, coverY, CoverW, CoverH, 16);
            else
                DrawCoverPlaceholder(canvas, data.Title, coverX, coverY, CoverW, CoverH, boldTypeface);
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
        string pagesText = data.PageCount.HasValue ? $"{data.PageCount:N0} pages" : "–";
        string timeText  = data.TotalMinutesRead > 0 ? FormatReadingTime(data.TotalMinutesRead) : "–";

        float chipY = y;
        DrawInfoChip(canvas, pagesText, Pad, chipY, (w / 2f) - Pad - 20, 70, regularTypeface, 38);
        DrawInfoChip(canvas, timeText,  w / 2f + 20, chipY, (w / 2f) - Pad - 20, 70, regularTypeface, 38);
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

    /// <summary>
    /// Draws a path-based heart icon followed by text, all centered at (cx, y).
    /// Avoids Unicode glyph rendering failures on Android default typefaces.
    /// </summary>
    private static void DrawHeartAndText(SKCanvas canvas, string text, float cx, float y,
        SKTypeface typeface, float fontSize, SKColor color)
    {
        using var font = new SKFont(typeface, fontSize);
        using var measurePaint = new SKPaint { IsAntialias = true };
        float textW = font.MeasureText(text, measurePaint);

        float heartSize = fontSize * 0.32f;   // cubic bezier "radius"
        float heartIconW = heartSize * 2.6f;  // total bounding width of the heart path
        float gap        = fontSize * 0.28f;
        float totalW     = heartIconW + gap + textW;

        float heartCx = cx - totalW / 2f + heartIconW / 2f;
        float textX   = cx - totalW / 2f + heartIconW + gap;

        // Align heart vertically with the cap-height of the text
        float heartCy = y - fontSize * 0.38f;

        DrawHeartIcon(canvas, heartCx, heartCy, heartSize, color);

        using var textPaint = new SKPaint { Color = color, IsAntialias = true };
        canvas.DrawText(text, textX, y, SKTextAlign.Left, font, textPaint);
    }

    /// <summary>
    /// Draws a filled heart at (cx, cy) using cubic bezier curves — no glyph required.
    /// </summary>
    private static void DrawHeartIcon(SKCanvas canvas, float cx, float cy, float size, SKColor color)
    {
        using var path = new SKPath();

        // Start at the top-center dip (the notch between the two lobes)
        path.MoveTo(cx, cy - size * 0.2f);

        // Right lobe: sweep right and down to the bottom tip
        path.CubicTo(
            cx + size * 0.1f, cy - size * 1.1f,
            cx + size * 1.3f, cy - size * 1.1f,
            cx + size * 1.3f, cy - size * 0.2f);
        path.CubicTo(
            cx + size * 1.3f, cy + size * 0.5f,
            cx + size * 0.4f, cy + size * 0.9f,
            cx,               cy + size * 1.4f);

        // Left lobe: sweep left back up to the top-center dip
        path.CubicTo(
            cx - size * 0.4f, cy + size * 0.9f,
            cx - size * 1.3f, cy + size * 0.5f,
            cx - size * 1.3f, cy - size * 0.2f);
        path.CubicTo(
            cx - size * 1.3f, cy - size * 1.1f,
            cx - size * 0.1f, cy - size * 1.1f,
            cx,               cy - size * 0.2f);

        path.Close();

        using var paint = new SKPaint { Color = color, IsAntialias = true, Style = SKPaintStyle.Fill };
        canvas.DrawPath(path, paint);
    }

    /// <summary>
    /// Draws a 5-pointed star at (cx, cy) using path geometry — no Unicode glyph required.
    /// Avoids glyph rendering failures on Android default typefaces.
    /// </summary>
    private static void DrawStarIcon(SKCanvas canvas, float cx, float cy, float outerRadius,
        SKColor color, bool filled)
    {
        using var path = new SKPath();
        float innerRadius = outerRadius * 0.4f;

        for (int i = 0; i < 10; i++)
        {
            float radius = i % 2 == 0 ? outerRadius : innerRadius;
            double angle = -Math.PI / 2 + i * Math.PI / 5;
            float px = cx + radius * (float)Math.Cos(angle);
            float py = cy + radius * (float)Math.Sin(angle);

            if (i == 0)
                path.MoveTo(px, py);
            else
                path.LineTo(px, py);
        }
        path.Close();

        using var paint = new SKPaint
        {
            Color = color,
            IsAntialias = true,
            Style = filled ? SKPaintStyle.Fill : SKPaintStyle.Stroke,
            StrokeWidth = filled ? 0 : 2f
        };
        canvas.DrawPath(path, paint);
    }

    private static void DrawText(SKCanvas canvas, string text, float x, float y, SKTypeface typeface,
        float fontSize, SKColor color, SKTextAlign align)
    {
        using var font  = new SKFont(typeface, fontSize);
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
        using var font         = new SKFont(typeface, fontSize);
        using var measurePaint = new SKPaint { IsAntialias = true };
        float textW  = font.MeasureText(text, measurePaint);
        float chipW  = textW + 60;
        float chipH  = fontSize + 30;

        var chipRect = new SKRect(
            centerX - chipW / 2f, y - chipH / 2f + 10,
            centerX + chipW / 2f, y + chipH / 2f + 10);

        using var bgPaint   = new SKPaint { Color = bgColor,   IsAntialias = true };
        using var textPaint = new SKPaint { Color = textColor, IsAntialias = true };
        canvas.DrawRoundRect(chipRect, 12, 12, bgPaint);
        canvas.DrawText(text, centerX, y + fontSize / 3f + 10, SKTextAlign.Center, font, textPaint);
    }

    /// <summary>
    /// Draws a stat tile with a colored accent bar at the top clipped to the tile's rounded corners.
    /// </summary>
    private static void DrawStatTile(SKCanvas canvas, string value, string label, float x, float y,
        float w, float h, SKTypeface boldTypeface, SKTypeface regularTypeface,
        SKColor accentColor, float valueFontSize = 80)
    {
        var tileRect = new SKRect(x, y, x + w, y + h);

        // Clip all drawing to the rounded tile shape so the accent bar gets rounded top corners
        canvas.Save();
        using var rrect = new SKRoundRect(tileRect, 16);
        canvas.ClipRoundRect(rrect, SKClipOperation.Intersect, true);

        using var bgPaint     = new SKPaint { Color = BgSecondary, IsAntialias = true };
        using var accentPaint = new SKPaint { Color = accentColor,  IsAntialias = true };
        canvas.DrawRect(tileRect, bgPaint);
        canvas.DrawRect(new SKRect(x, y, x + w, y + 5), accentPaint);

        canvas.Restore();

        float midX = x + w / 2f;

        using var valueFont  = new SKFont(boldTypeface,    valueFontSize);
        using var valuePaint = new SKPaint { Color = Primary,       IsAntialias = true };
        canvas.DrawText(value, midX, y + h * 0.56f, SKTextAlign.Center, valueFont, valuePaint);

        using var labelFont  = new SKFont(regularTypeface, 30);
        using var labelPaint = new SKPaint { Color = TextSecondary, IsAntialias = true };
        canvas.DrawText(label, midX, y + h * 0.84f, SKTextAlign.Center, labelFont, labelPaint);
    }

    private static void DrawBookRow(SKCanvas canvas, int rank, string title, string author,
        double? rating, float x, float y, float width,
        SKTypeface regularTypeface, SKTypeface boldTypeface)
    {
        const float CircleR = 32;
        float circleX = x + CircleR + 8;
        float circleY = y + 55;

        // Rank circle
        using var circleBg = new SKPaint { Color = BgTertiary, IsAntialias = true };
        canvas.DrawCircle(circleX, circleY, CircleR, circleBg);

        using var rankFont  = new SKFont(boldTypeface, 36);
        using var rankPaint = new SKPaint { Color = Primary, IsAntialias = true };
        canvas.DrawText(rank.ToString(), circleX, circleY + 13, SKTextAlign.Center, rankFont, rankPaint);

        // Title and author
        float textX = circleX + CircleR + 20;

        using var titleFont  = new SKFont(boldTypeface,    40);
        using var titlePaint = new SKPaint { Color = TextPrimary,   IsAntialias = true };
        canvas.DrawText(title,  textX, y + 42, SKTextAlign.Left, titleFont,  titlePaint);

        using var authorFont  = new SKFont(regularTypeface, 30);
        using var authorPaint = new SKPaint { Color = TextSecondary, IsAntialias = true };
        canvas.DrawText(author, textX, y + 85, SKTextAlign.Left, authorFont, authorPaint);

        // Mini star rating — right-aligned (path-based to avoid glyph failures on Android)
        if (rating.HasValue)
        {
            int filled = Math.Clamp((int)Math.Round(rating.Value), 0, 5);
            const float StarRadius  = 14f;
            const float StarSpacing = 34f;
            float starsStartX = x + width - 5 * StarSpacing - 10;

            for (int i = 0; i < 5; i++)
            {
                SKColor starColor = i < filled ? AccentGold : BgTertiary.WithAlpha(180);
                DrawStarIcon(canvas, starsStartX + i * StarSpacing + StarRadius, y + 130,
                    StarRadius, starColor, filled: i < filled);
            }
        }

        // Row separator
        DrawHorizontalLine(canvas, x, x + width, y + 165, BorderColor);
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
        using var font  = new SKFont(boldTypeface, 120);
        using var paint = new SKPaint { Color = Primary, IsAntialias = true };
        canvas.DrawText(letter, x + w / 2f, y + h / 2f + 40, SKTextAlign.Center, font, paint);
    }

    private static void DrawInfoChip(SKCanvas canvas, string text, float x, float y,
        float maxW, float chipH, SKTypeface typeface, float fontSize)
    {
        var rect = new SKRect(x, y, x + maxW, y + chipH);
        using var bgPaint = new SKPaint { Color = BgSecondary, IsAntialias = true };
        canvas.DrawRoundRect(rect, 14, 14, bgPaint);

        using var font  = new SKFont(typeface, fontSize);
        using var paint = new SKPaint { Color = TextSecondary, IsAntialias = true };
        canvas.DrawText(text, x + maxW / 2f, y + chipH / 2f + fontSize / 3f,
            SKTextAlign.Center, font, paint);
    }

    private static void DrawStarRating(SKCanvas canvas, double rating, float centerX, float y, float fontSize)
    {
        int filled = Math.Clamp((int)Math.Round(rating), 0, 5);
        float starRadius = fontSize * 0.5f;

        const float StarSpacing = 70;
        float startX = centerX - (5 * StarSpacing) / 2f + StarSpacing / 2f;

        for (int i = 0; i < 5; i++)
        {
            SKColor starColor = i < filled ? Primary : BgTertiary.WithAlpha(180);
            DrawStarIcon(canvas, startX + i * StarSpacing, y + fontSize / 3f,
                starRadius, starColor, filled: i < filled);
        }
    }

    private static void DrawCategoryRatings(SKCanvas canvas,
        List<KeyValuePair<RatingCategory, int?>> categories,
        float x, float y, float totalW, SKTypeface regularTypeface, SKTypeface boldTypeface)
    {
        const int   Cols  = 3;
        const float CellH = 90;
        const float Gap   = 20;
        float cellW = (totalW - Gap * (Cols - 1)) / Cols;

        for (int i = 0; i < categories.Count; i++)
        {
            int   col   = i % Cols;
            int   row   = i / Cols;
            float cellX = x + col * (cellW + Gap);
            float cellY = y + row * (CellH + Gap);

            var (category, ratingVal) = (categories[i].Key, categories[i].Value);

            var rect = new SKRect(cellX, cellY, cellX + cellW, cellY + CellH);
            using var bgPaint = new SKPaint { Color = BgTertiary, IsAntialias = true };
            canvas.DrawRoundRect(rect, 12, 12, bgPaint);

            string label = CategoryLabel(category);
            string value = ratingVal.HasValue ? $"{ratingVal}/5" : "–";

            using var labelFont  = new SKFont(regularTypeface, 30);
            using var labelPaint = new SKPaint { Color = TextSecondary, IsAntialias = true };
            canvas.DrawText(label, cellX + cellW / 2f, cellY + 38, SKTextAlign.Center, labelFont, labelPaint);

            using var valFont  = new SKFont(boldTypeface, 36);
            using var valPaint = new SKPaint { Color = Primary, IsAntialias = true };
            canvas.DrawText(value, cellX + cellW / 2f, cellY + 74, SKTextAlign.Center, valFont, valPaint);
        }
    }

    private static void DrawWatermark(SKCanvas canvas, int w, int h,
        SKTypeface boldTypeface, SKTypeface regularTypeface)
    {
        float y = h - 120;

        DrawHeartAndText(canvas, "BookHeart", w / 2f, y, boldTypeface, 36, Primary.WithAlpha(128));

        using var tagFont  = new SKFont(regularTypeface, 26);
        using var tagPaint = new SKPaint { Color = TextSecondary.WithAlpha(100), IsAntialias = true };
        canvas.DrawText("Track your reading journey", w / 2f, y + 42,
            SKTextAlign.Center, tagFont, tagPaint);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Utility helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static byte[] EncodePng(SKBitmap bitmap)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var data  = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static string TruncateText(string text, int maxChars)
    {
        if (text.Length <= maxChars) return text;
        return text[..(maxChars - 1)] + "…";
    }

    private static List<string> WrapText(string text, float maxWidth, SKTypeface typeface, float fontSize)
    {
        using var font  = new SKFont(typeface, fontSize);
        using var paint = new SKPaint { IsAntialias = true };

        var words   = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var lines   = new List<string>();
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
        if (minutes <= 0) return "0m";
        if (minutes < 60) return $"{minutes}m";
        int h = minutes / 60;
        int m = minutes % 60;
        return m > 0 ? $"{h}h {m}m" : $"{h}h";
    }

    private static string CategoryLabel(RatingCategory category) => category switch
    {
        RatingCategory.Characters    => "Characters",
        RatingCategory.Plot          => "Plot",
        RatingCategory.WritingStyle  => "Writing",
        RatingCategory.SpiceLevel    => "Spice",
        RatingCategory.Pacing        => "Pacing",
        RatingCategory.WorldBuilding => "World",
        _ => category.ToString()
    };
}
