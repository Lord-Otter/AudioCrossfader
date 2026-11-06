using System.Drawing;
using System.Drawing.Drawing2D;

public partial class CrossfadeOverlayForm
{
    public sealed class FloatingPillsStyle : ICarouselStyle
    {
        public string DisplayName => "3D Floating Pills";
        public CarouselCategory Category => CarouselCategory.Full;
        public int SortOrder => 10;

        public void Render(CrossfadeOverlayForm host, Graphics g, RectangleF heroRect, int baseH, float scale) =>
            host.RenderFloatingPillsStyle(g, heroRect, baseH, scale);
    }

    private void RenderFloatingPillsStyle(Graphics g, RectangleF heroRect, int baseH, float scale)
    {
        int[] drawOrder = new[] { -2, 2, -1, 1 };
        foreach (int offset in drawOrder)
        {
            int itemIdx = GetCarouselItemIndexForSlot(offset);
            float visualOffset = offset - _wheelScrollOffset;
            DrawFloatingCarouselPill(g, _cycleItems[itemIdx], visualOffset, heroRect, baseH, scale);
        }

        if (_wheelExpansion > 0.02f)
        {
            int shadowAlpha = (int)Math.Clamp(115 * _wheelExpansion, 0, 125);
            for (int s = 3; s >= 1; s--)
            {
                RectangleF shadowRect = RectangleF.Inflate(heroRect, s * 1.5f, s * 2.2f);
                using GraphicsPath shadowPath = Win11CardPanel.CreateRoundRectF(shadowRect, 11f * scale);
                using SolidBrush shadowBrush = new SolidBrush(Color.FromArgb(shadowAlpha / s, 0, 0, 0));
                g.FillPath(shadowBrush, shadowPath);
            }
        }

        float heroCornerRadius = GetWin11CornerRadius(scale);
        using (GraphicsPath heroPath = Win11CardPanel.CreateRoundRectF(heroRect, heroCornerRadius))
        {
            FillRoundedCardGradient(g, heroPath, heroRect, _renderedBgLeft, _renderedBgRight);
            DrawActiveHUDContent(g, heroRect, _app1Label, _app2Label, _rawSource1, _rawSource2, _app1Vol, _app2Vol, scale, 1.0f);
            int rimAlpha = 105 + (int)Math.Round(90 * _wheelExpansion);
            using Pen heroBorderPen = new Pen(Color.FromArgb(Math.Clamp(rimAlpha, 0, 220), 255, 255, 255), 1.3f);
            g.DrawPath(heroBorderPen, heroPath);
        }
    }

    private void DrawFloatingCarouselPill(
        Graphics g,
        CycleDisplayItem item,
        float signedVisualOffset,
        RectangleF heroCardRect,
        int baseH,
        float scale)
    {
        float dist = Math.Abs(signedVisualOffset);
        if (dist < 0.15f || dist > 2.35f) return;

        float pillWidthRatio = Math.Clamp(0.89f - (dist - 1.0f) * 0.10f, 0.74f, 0.92f);
        float pillW = heroCardRect.Width * pillWidthRatio;
        float pillH = Math.Clamp(baseH * 0.50f, 28f, 64f);
        float pillX = heroCardRect.X + (heroCardRect.Width - pillW) / 2f;

        float heroCenterY = heroCardRect.Y + heroCardRect.Height / 2f;
        float minOrbitDist = (heroCardRect.Height / 2f) + (pillH / 2f) + (6f * scale);
        float orbitSpacing = minOrbitDist + (dist - 1.0f) * (pillH + 5f * scale);

        bool alwaysPreviews = string.Equals(_settings.NeighborPreviewVisibility, "Always", StringComparison.OrdinalIgnoreCase);
        float pillCenterY = heroCenterY + Math.Sign(signedVisualOffset) * (orbitSpacing * _wheelExpansion);
        RectangleF fullPillRect = new RectangleF(pillX, pillCenterY - pillH / 2f, pillW, pillH);
        RectangleF pillRect = ApplyAlwaysPreviewBlendIfActive(fullPillRect, signedVisualOffset, heroCardRect, scale);

        if (pillRect.Bottom < 0 || pillRect.Top > this.Height) return;

        float baseVis = (alwaysPreviews && dist <= 1.25f) ? Math.Max(_wheelExpansion, 0.92f) : _wheelExpansion;
        float visibility = Math.Clamp((1.0f - Math.Max(0f, dist - 1.0f) * 0.55f) * baseVis, 0f, 1f);
        int cardAlpha = (int)Math.Clamp(visibility * 235f, 0f, 235f);
        if (cardAlpha < 12) return;

        Color rawC1 = ParseOverlayColor(item.Color1Hex, Color.FromArgb(40, 80, 120));
        Color rawC2 = ParseOverlayColor(item.Color2Hex, Color.FromArgb(95, 45, 85));

        Color surfaceBase = Color.FromArgb(30, 31, 36);
        Color tintLeft = _settings.UseSourceBackgroundColors ? ColorLerp(surfaceBase, rawC1, 0.48f) : surfaceBase;
        Color tintRight = _settings.UseSourceBackgroundColors ? ColorLerp(surfaceBase, rawC2, 0.48f) : surfaceBase;

        Color finalLeft = Color.FromArgb(cardAlpha, tintLeft);
        Color finalRight = item.IsSingleSource ? finalLeft : Color.FromArgb(cardAlpha, tintRight);

        float radius = Math.Min(pillRect.Height / 2f, 12f * scale);
        using GraphicsPath pillPath = Win11CardPanel.CreateRoundRectF(pillRect, radius);
        FillRoundedCardGradient(g, pillPath, pillRect, finalLeft, finalRight);

        using (Pen rimPen = new Pen(Color.FromArgb((int)(cardAlpha * 0.45f), 255, 255, 255), 1f))
        {
            g.DrawPath(rimPen, pillPath);
        }

        int textAlpha = (int)Math.Clamp(visibility * 240f, 0f, 240f);
        bool showName = _settings.OverlayIdentityMode == "Name" || _settings.OverlayIdentityMode == "Both";
        bool showIcon = _settings.OverlayIdentityMode == "Icon" || _settings.OverlayIdentityMode == "Both";

        if (item.IsSingleSource)
        {
            RectangleF centerZone = new RectangleF(pillRect.X + 14f * scale, pillRect.Y + 2f, pillRect.Width - 28f * scale, pillRect.Height - 4f);
            DrawCenteredPillIdentity(g, centerZone, item.Label1, item.RawSource1, textAlpha, scale * 0.85f, showName, showIcon);
            return;
        }

        float centerX = pillRect.X + pillRect.Width / 2f;
        float centerY = pillRect.Y + pillRect.Height / 2f;

        using Font arrowFont = new Font("Segoe UI Symbol", Math.Clamp(9.0f * scale, 7f, 16f), FontStyle.Bold);
        using SolidBrush arrowBrush = new SolidBrush(Color.FromArgb((int)(textAlpha * 0.65f), 200, 210, 225));
        using StringFormat centerSf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString("⇄", arrowFont, arrowBrush, new PointF(centerX, centerY), centerSf);

        RectangleF leftZone = new RectangleF(pillRect.X + 12f * scale, pillRect.Y + 2f, (pillRect.Width / 2f) - 24f * scale, pillRect.Height - 4f);
        RectangleF rightZone = new RectangleF(centerX + 12f * scale, pillRect.Y + 2f, (pillRect.Width / 2f) - 24f * scale, pillRect.Height - 4f);

        DrawPillSideIdentity(g, leftZone, item.Label1, item.RawSource1, textAlpha, scale * 0.85f, showName, showIcon, isRightSide: false);
        DrawPillSideIdentity(g, rightZone, item.Label2, item.RawSource2, textAlpha, scale * 0.85f, showName, showIcon, isRightSide: true);
    }
}