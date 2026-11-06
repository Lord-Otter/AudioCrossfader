using System;
using System.Drawing;
using System.Drawing.Drawing2D;

public partial class CrossfadeOverlayForm
{
    private void RenderWideDeckOrder(
        Graphics g,
        RectangleF heroRect,
        int baseH,
        float scale,
        WideCardDrawer drawer,
        bool drawHeroDropShadow = false)
    {
        int[] deckOrder = new[] { -3, 3, -2, 2, -1, 1, 0 };
        foreach (int offset in deckOrder)
        {
            if (drawHeroDropShadow && offset == 0 && _wheelExpansion > 0.02f)
            {
                float heroSlideOffset = Math.Clamp(1.0f - Math.Abs(_wheelScrollOffset), 0f, 1f);
                int shadowAlpha = (int)Math.Clamp(115 * _wheelExpansion * heroSlideOffset, 0, 125);
                if (shadowAlpha > 5)
                {
                    for (int s = 3; s >= 1; s--)
                    {
                        RectangleF shadowRect = RectangleF.Inflate(heroRect, s * 1.5f, s * 2.2f);
                        using GraphicsPath shadowPath = Win11CardPanel.CreateRoundRectF(shadowRect, 11f * scale);
                        using SolidBrush shadowBrush = new SolidBrush(Color.FromArgb(shadowAlpha / s, 0, 0, 0));
                        g.FillPath(shadowBrush, shadowPath);
                    }
                }
            }

            int itemIdx = GetCarouselItemIndexForSlot(offset);
            float visualOffset = offset - _wheelScrollOffset;
            drawer(g, _cycleItems[itemIdx], offset == 0, visualOffset, heroRect, baseH, scale);
        }
    }

    private void DrawMinimalSidePreviewBoxes(Graphics g, RectangleF heroRect, float scale, float visibility)
    {
        int count = _cycleItems.Count;
        if (count < 2 || visibility <= 0.01f) return;

        int leftIdx = GetCarouselItemIndexForSlot(-1);
        int rightIdx = GetCarouselItemIndexForSlot(1);

        float bump = MathF.Sin((1f - _compactTransitionProgress) * MathF.PI) * 2.5f * scale * (_compactTransitionProgress > 0f ? 1f : 0f);
        var (leftBox, rightBox) = GetMinimalSidePreviewBoxRects(heroRect, scale, bump);

        int alpha = (int)Math.Clamp(230f * visibility, 0f, 230f);
        DrawSingleNeighborPreviewBox(g, leftBox, _cycleItems[leftIdx], scale, isLeftSide: true, alpha: alpha);
        DrawSingleNeighborPreviewBox(g, rightBox, _cycleItems[rightIdx], scale, isLeftSide: false, alpha: alpha);
    }

    private void DrawCompactTopBottomPreviewBoxes(Graphics g, RectangleF heroRect, float scale, float visibility)
    {
        int count = _cycleItems.Count;
        if (count < 2 || visibility <= 0.01f) return;

        int topIdx = GetCarouselItemIndexForSlot(-1);
        int botIdx = GetCarouselItemIndexForSlot(1);

        float bump = MathF.Sin((1f - _compactTransitionProgress) * MathF.PI) * 2.5f * scale * (_compactTransitionProgress > 0f ? 1f : 0f);
        var (topBox, bottomBox) = GetCompactTopBottomPreviewBoxRects(heroRect, scale, bump);

        int alpha = (int)Math.Clamp(235f * visibility, 0f, 235f);
        DrawSingleTopBottomPreviewBar(g, topBox, _cycleItems[topIdx], scale, isTopSide: true, alpha: alpha);
        DrawSingleTopBottomPreviewBar(g, bottomBox, _cycleItems[botIdx], scale, isTopSide: false, alpha: alpha);
    }

    private void DrawSingleTopBottomPreviewBar(Graphics g, RectangleF barRect, CycleDisplayItem item, float scale, bool isTopSide, int alpha = 235)
    {
        if (barRect.Width <= 8f || barRect.Height <= 4f || alpha <= 8) return;

        float radius = Math.Clamp(7f * scale, 5f, 12f);
        Color rawC1 = ParseOverlayColor(item.Color1Hex, Color.FromArgb(40, 80, 120));
        Color rawC2 = ParseOverlayColor(item.Color2Hex, Color.FromArgb(95, 45, 85));

        Color surfaceBase = Color.FromArgb(26, 27, 32);
        Color col1 = _settings.UseSourceBackgroundColors
            ? EnsureReadableBackdrop(ColorLerp(surfaceBase, rawC1, 0.68f))
            : surfaceBase;
        Color col2 = item.IsSingleSource
            ? col1
            : (_settings.UseSourceBackgroundColors
                ? EnsureReadableBackdrop(ColorLerp(surfaceBase, rawC2, 0.68f))
                : surfaceBase);

        using GraphicsPath barPath = Win11CardPanel.CreateRoundRectF(barRect, radius);
        FillRoundedCardGradient(g, barPath, barRect, Color.FromArgb(alpha, col1), Color.FromArgb(alpha, col2));

        float exposedH = barRect.Height * 0.56f;
        RectangleF visibleLip = isTopSide
            ? new RectangleF(barRect.X, barRect.Y + 1f, barRect.Width, exposedH)
            : new RectangleF(barRect.X, barRect.Bottom - exposedH - 1f, barRect.Width, exposedH);

        int textAlpha = (int)Math.Clamp(alpha * 0.96f, 0f, 245f);
        bool showName = _settings.OverlayIdentityMode == "Name" || _settings.OverlayIdentityMode == "Both";
        bool showIcon = _settings.OverlayIdentityMode == "Icon" || _settings.OverlayIdentityMode == "Both";

        if (item.IsSingleSource)
        {
            RectangleF centerZone = new RectangleF(visibleLip.X + 12f * scale, visibleLip.Y, visibleLip.Width - 24f * scale, visibleLip.Height);
            DrawCenteredPillIdentity(g, centerZone, item.Label1, item.RawSource1, textAlpha, scale * 0.72f, showName, showIcon);
        }
        else
        {
            float cx = visibleLip.X + visibleLip.Width / 2f;
            RectangleF leftZone = new RectangleF(visibleLip.X + 10f * scale, visibleLip.Y, (visibleLip.Width / 2f) - 18f * scale, visibleLip.Height);
            RectangleF rightZone = new RectangleF(cx + 8f * scale, visibleLip.Y, (visibleLip.Width / 2f) - 18f * scale, visibleLip.Height);
            DrawPillSideIdentity(g, leftZone, item.Label1, item.RawSource1, textAlpha, scale * 0.72f, showName, showIcon, isRightSide: false);
            DrawPillSideIdentity(g, rightZone, item.Label2, item.RawSource2, textAlpha, scale * 0.72f, showName, showIcon, isRightSide: true);
        }

        using Pen borderPen = new Pen(Color.FromArgb((int)(alpha * 0.60f), 255, 255, 255), 1.1f);
        g.DrawPath(borderPen, barPath);
    }

    private void DrawSingleNeighborPreviewBox(Graphics g, RectangleF boxRect, CycleDisplayItem item, float scale, bool? isLeftSide = null, int alpha = 230)
    {
        if (boxRect.Width <= 4f || boxRect.Height <= 4f || alpha <= 8) return;

        float radius = Math.Clamp(6f * scale, 4f, 10f);
        Color rawC1 = ParseOverlayColor(item.Color1Hex, Color.FromArgb(40, 80, 120));
        Color rawC2 = ParseOverlayColor(item.Color2Hex, Color.FromArgb(95, 45, 85));

        Color surfaceBase = Color.FromArgb(26, 27, 32);
        Color col1 = _settings.UseSourceBackgroundColors
            ? EnsureReadableBackdrop(ColorLerp(surfaceBase, rawC1, 0.75f))
            : surfaceBase;
        Color col2 = item.IsSingleSource
            ? col1
            : (_settings.UseSourceBackgroundColors
                ? EnsureReadableBackdrop(ColorLerp(surfaceBase, rawC2, 0.75f))
                : surfaceBase);

        using (SolidBrush shadowBrush = new SolidBrush(Color.FromArgb((int)(alpha * 0.35f), 0, 0, 0)))
        {
            RectangleF sRect = new RectangleF(boxRect.X + 1f, boxRect.Y + 2f, boxRect.Width, boxRect.Height);
            using GraphicsPath sPath = Win11CardPanel.CreateRoundRectF(sRect, radius);
            g.FillPath(shadowBrush, sPath);
        }

        using GraphicsPath boxPath = Win11CardPanel.CreateRoundRectF(boxRect, radius);
        GraphicsState clipState = g.Save();
        g.SetClip(boxPath, CombineMode.Intersect);

        // Because the preview box is 50% overlapped by the main card, we place the color split & icons
        // in the exposed outer half (left half for the left box, right half for the right box) so they are clearly visible!
        RectangleF visibleZone = isLeftSide == true
            ? new RectangleF(boxRect.X, boxRect.Y, boxRect.Width * 0.54f, boxRect.Height)
            : (isLeftSide == false
                ? new RectangleF(boxRect.X + boxRect.Width * 0.46f, boxRect.Y, boxRect.Width * 0.54f, boxRect.Height)
                : boxRect);

        if (item.IsSingleSource)
        {
            using SolidBrush b1 = new SolidBrush(Color.FromArgb(alpha, col1));
            g.FillRectangle(b1, boxRect);
        }
        else if (isLeftSide.HasValue)
        {
            // Top/bottom split inside the exposed wing so both source colors and both icons are visible!
            float halfH = boxRect.Height / 2f;
            using SolidBrush b1 = new SolidBrush(Color.FromArgb(alpha, col1));
            using SolidBrush b2 = new SolidBrush(Color.FromArgb(alpha, col2));
            g.FillRectangle(b1, boxRect.X, boxRect.Y, boxRect.Width, halfH + 0.5f);
            g.FillRectangle(b2, boxRect.X, boxRect.Y + halfH, boxRect.Width, halfH);

            using Pen splitPen = new Pen(Color.FromArgb((int)(alpha * 0.40f), 255, 255, 255), 1f);
            g.DrawLine(splitPen, visibleZone.Left + 2f, boxRect.Y + halfH, visibleZone.Right - 2f, boxRect.Y + halfH);
        }
        else
        {
            float halfW = boxRect.Width / 2f;
            using SolidBrush b1 = new SolidBrush(Color.FromArgb(alpha, col1));
            using SolidBrush b2 = new SolidBrush(Color.FromArgb(alpha, col2));
            g.FillRectangle(b1, boxRect.X, boxRect.Y, halfW + 0.5f, boxRect.Height);
            g.FillRectangle(b2, boxRect.X + halfW, boxRect.Y, halfW, boxRect.Height);
        }

        bool showIcon = _settings.OverlayIdentityMode == "Icon" || _settings.OverlayIdentityMode == "Both";
        if (showIcon)
        {
            Bitmap? icon1 = AppIconCache.GetIconForSource(item.RawSource1, _settings);
            Bitmap? icon2 = item.IsSingleSource ? null : AppIconCache.GetIconForSource(item.RawSource2, _settings);
            float icoOpacity = (alpha / 255f) * 0.92f;

            if (item.IsSingleSource && icon1 != null)
            {
                float icoSz = Math.Clamp(13f * scale, 10f, Math.Min(visibleZone.Width - 3f, visibleZone.Height - 4f));
                RectangleF icoRect = new RectangleF(
                    visibleZone.X + (visibleZone.Width - icoSz) / 2f,
                    visibleZone.Y + (visibleZone.Height - icoSz) / 2f,
                    icoSz, icoSz);
                DrawImageWithAlpha(g, icon1, icoRect, icoOpacity);
            }
            else if (!item.IsSingleSource)
            {
                float icoSz = Math.Clamp(11f * scale, 9f, Math.Min(visibleZone.Width - 3f, (visibleZone.Height / 2f) - 3f));
                float cx = visibleZone.X + (visibleZone.Width - icoSz) / 2f;
                if (icon1 != null)
                {
                    float y1 = visibleZone.Y + (visibleZone.Height * 0.25f) - (icoSz / 2f);
                    DrawImageWithAlpha(g, icon1, new RectangleF(cx, y1, icoSz, icoSz), icoOpacity);
                }
                if (icon2 != null)
                {
                    float y2 = visibleZone.Y + (visibleZone.Height * 0.75f) - (icoSz / 2f);
                    DrawImageWithAlpha(g, icon2, new RectangleF(cx, y2, icoSz, icoSz), icoOpacity);
                }
            }
        }

        g.Restore(clipState);

        using Pen borderPen = new Pen(Color.FromArgb((int)(alpha * 0.65f), 255, 255, 255), 1.1f);
        g.DrawPath(borderPen, boxPath);
    }

    private void DrawPillSideIdentity(
        Graphics g,
        RectangleF zone,
        string label,
        string rawSource,
        int alpha,
        float scale,
        bool showName,
        bool showIcon,
        bool isRightSide)
    {
        if (zone.Width <= 10 || alpha <= 5) return;

        Bitmap? icon = showIcon ? AppIconCache.GetIconForSource(rawSource, _settings) : null;
        int iconPx = icon != null ? (int)Math.Clamp(16 * scale, 12, 24) : 0;
        float gap = 6f * scale;
        float iconOpacity = alpha / 255f;

        using Font font = new Font("Segoe UI Semibold", Math.Clamp(9.5f * scale, 7f, 18f), FontStyle.Bold);
        string text = showName ? label : "";

        float cy = zone.Y + zone.Height / 2f;
        using SolidBrush textBrush = new SolidBrush(Color.FromArgb(alpha, 238, 240, 245));

        if (!isRightSide)
        {
            float curRight = zone.Right;

            if (icon != null)
            {
                DrawImageWithAlpha(g, icon, new RectangleF(curRight - iconPx, cy - iconPx / 2f, iconPx, iconPx), iconOpacity);
                curRight -= (iconPx + gap);
            }

            if (!string.IsNullOrEmpty(text) && curRight > zone.Left + 8)
            {
                RectangleF textRect = new RectangleF(zone.Left, zone.Y, curRight - zone.Left, zone.Height);
                using StringFormat sf = new StringFormat
                {
                    Alignment = StringAlignment.Far,
                    LineAlignment = StringAlignment.Center,
                    Trimming = StringTrimming.EllipsisCharacter,
                    FormatFlags = StringFormatFlags.NoWrap
                };
                g.DrawString(text, font, textBrush, textRect, sf);
            }
        }
        else
        {
            float curLeft = zone.Left;

            if (icon != null)
            {
                DrawImageWithAlpha(g, icon, new RectangleF(curLeft, cy - iconPx / 2f, iconPx, iconPx), iconOpacity);
                curLeft += (iconPx + gap);
            }

            if (!string.IsNullOrEmpty(text) && zone.Right > curLeft + 8)
            {
                RectangleF textRect = new RectangleF(curLeft, zone.Y, zone.Right - curLeft, zone.Height);
                using StringFormat sf = new StringFormat
                {
                    Alignment = StringAlignment.Near,
                    LineAlignment = StringAlignment.Center,
                    Trimming = StringTrimming.EllipsisCharacter,
                    FormatFlags = StringFormatFlags.NoWrap
                };
                g.DrawString(text, font, textBrush, textRect, sf);
            }
        }
    }

    private void DrawCenteredPillIdentity(
        Graphics g,
        RectangleF zone,
        string label,
        string rawSource,
        int alpha,
        float scale,
        bool showName,
        bool showIcon)
    {
        if (zone.Width <= 10 || alpha <= 5) return;

        Bitmap? icon = showIcon ? AppIconCache.GetIconForSource(rawSource, _settings) : null;
        int iconPx = icon != null ? (int)Math.Clamp(16 * scale, 12, 24) : 0;
        float gap = 6f * scale;
        float iconOpacity = alpha / 255f;

        using Font font = new Font("Segoe UI Semibold", Math.Clamp(9.5f * scale, 7f, 18f), FontStyle.Bold);
        string text = showName ? label : "";

        SizeF textSz = !string.IsNullOrEmpty(text) ? g.MeasureString(text, font) : SizeF.Empty;
        float textW = Math.Min(textSz.Width + 2f, Math.Max(0f, zone.Width - iconPx - (icon != null ? gap : 0f)));
        float totalW = iconPx + (icon != null && !string.IsNullOrEmpty(text) ? gap : 0f) + textW;

        float startX = zone.X + (zone.Width - totalW) / 2f;
        float cy = zone.Y + zone.Height / 2f;

        if (icon != null)
        {
            DrawImageWithAlpha(g, icon, new RectangleF(startX, cy - iconPx / 2f, iconPx, iconPx), iconOpacity);
            startX += iconPx + gap;
        }

        if (!string.IsNullOrEmpty(text) && textW > 4f)
        {
            RectangleF textRect = new RectangleF(startX, zone.Y, textW + 4f, zone.Height);
            using SolidBrush textBrush = new SolidBrush(Color.FromArgb(alpha, 238, 240, 245));
            using StringFormat sf = new StringFormat
            {
                Alignment = StringAlignment.Near,
                LineAlignment = StringAlignment.Center,
                Trimming = StringTrimming.EllipsisCharacter,
                FormatFlags = StringFormatFlags.NoWrap
            };
            g.DrawString(text, font, textBrush, textRect, sf);
        }
    }

    private RectangleF ApplyAlwaysPreviewBlendIfActive(RectangleF fullCarouselRect, float signedVisualOffset, RectangleF heroCardRect, float scale)
    {
        bool alwaysPreviews = string.Equals(_settings.NeighborPreviewVisibility, "Always", StringComparison.OrdinalIgnoreCase);
        float dist = Math.Abs(signedVisualOffset);
        if (!alwaysPreviews || dist < 0.05f)
            return fullCarouselRect;

        var (topBox, bottomBox) = GetCompactTopBottomPreviewBoxRects(heroCardRect, scale);
        RectangleF previewRect = signedVisualOffset < 0 ? topBox : bottomBox;

        if (dist > 1.35f)
        {
            float extraTuck = (dist - 1.0f) * 3f * scale;
            previewRect = new RectangleF(
                previewRect.X + 6f * scale,
                previewRect.Y + (signedVisualOffset < 0 ? extraTuck : -extraTuck),
                previewRect.Width - 12f * scale,
                previewRect.Height);
        }

        return LerpRectF(previewRect, fullCarouselRect, Math.Clamp(_wheelExpansion, 0f, 1f));
    }

    private static RectangleF LerpRectF(RectangleF a, RectangleF b, float t)
    {
        return new RectangleF(
            a.X + (b.X - a.X) * t,
            a.Y + (b.Y - a.Y) * t,
            a.Width + (b.Width - a.Width) * t,
            a.Height + (b.Height - a.Height) * t);
    }

    private static GraphicsPath CreateRoundedTrapezoidPath(PointF[] pts, float radius)
    {
        GraphicsPath path = new GraphicsPath();
        if (pts == null || pts.Length != 4 || radius <= 0.4f)
        {
            if (pts != null && pts.Length == 4) path.AddPolygon(pts);
            return path;
        }

        float topW = pts[1].X - pts[0].X;
        float botW = pts[2].X - pts[3].X;
        float h = pts[3].Y - pts[0].Y;
        float r = Math.Min(radius, Math.Min(Math.Min(topW, botW) * 0.45f, h * 0.45f));

        if (r <= 0.4f)
        {
            path.AddPolygon(pts);
            return path;
        }

        float d = r * 2f;
        path.AddArc(pts[0].X, pts[0].Y, d, d, 180f, 90f);
        path.AddArc(pts[1].X - d, pts[1].Y, d, d, 270f, 90f);
        path.AddArc(pts[2].X - d, pts[2].Y - d, d, d, 0f, 90f);
        path.AddArc(pts[3].X, pts[3].Y - d, d, d, 90f, 90f);
        path.CloseFigure();
        return path;
    }
}