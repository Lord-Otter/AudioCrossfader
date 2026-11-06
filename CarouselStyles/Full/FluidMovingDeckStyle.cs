using System.Drawing;
using System.Drawing.Drawing2D;

public partial class CrossfadeOverlayForm
{
    public sealed class FluidMovingDeckStyle : ICarouselStyle
    {
        public string DisplayName => "Fluid Moving Deck";
        public CarouselCategory Category => CarouselCategory.Full;
        public int SortOrder => 20;

        public void Render(CrossfadeOverlayForm host, Graphics g, RectangleF heroRect, int baseH, float scale) =>
            host.RenderWideDeckOrder(g, heroRect, baseH, scale, host.DrawFluidDeckCard, drawHeroDropShadow: true);
    }

    private void DrawFluidDeckCard(
        Graphics g,
        CycleDisplayItem item,
        bool isActiveSlot,
        float signedVisualOffset,
        RectangleF heroCardRect,
        int baseH,
        float scale)
    {
        float dist = Math.Abs(signedVisualOffset);
        if (dist > 2.35f) return;

        float activeBlend = Math.Clamp(1.0f - dist, 0f, 1f);

        float pillH = Math.Clamp(baseH * 0.50f, 28f, 64f);
        float cardH = pillH + (heroCardRect.Height - pillH) * activeBlend;

        float pillWidthRatio = dist <= 1.0f
            ? (1.0f - 0.11f * dist)
            : Math.Clamp(0.89f - (dist - 1.0f) * 0.10f, 0.74f, 0.92f);

        float cardW = heroCardRect.Width * pillWidthRatio;
        float cardX = heroCardRect.X + (heroCardRect.Width - cardW) / 2f;

        float heroCenterY = heroCardRect.Y + heroCardRect.Height / 2f;
        float minOrbitDist = (heroCardRect.Height / 2f) + (pillH / 2f) + (6f * scale);

        float orbitSpacing = dist <= 1.0f
            ? dist * minOrbitDist
            : minOrbitDist + (dist - 1.0f) * (pillH + 5f * scale);

        bool alwaysPreviews = string.Equals(_settings.NeighborPreviewVisibility, "Always", StringComparison.OrdinalIgnoreCase);
        float orbitExpansion = alwaysPreviews ? 1.0f : _wheelExpansion;
        float cardCenterY = heroCenterY + Math.Sign(signedVisualOffset) * (orbitSpacing * orbitExpansion);

        RectangleF fullCardRect = new RectangleF(cardX, cardCenterY - cardH / 2f, cardW, cardH);
        RectangleF cardRect = isActiveSlot
            ? fullCardRect
            : ApplyAlwaysPreviewBlendIfActive(fullCardRect, signedVisualOffset, heroCardRect, scale);

        if (cardRect.Bottom < 0 || cardRect.Top > this.Height) return;

        float baseVis = (alwaysPreviews && dist <= 1.25f) ? Math.Max(_wheelExpansion, 0.92f) : _wheelExpansion;
        float visibility = isActiveSlot
            ? 1.0f
            : Math.Clamp((1.0f - Math.Max(0f, dist - 1.0f) * 0.55f) * baseVis, 0f, 1f);
        int cardAlpha = (int)Math.Clamp(visibility * (235f + 20f * activeBlend), 0f, 255f);
        if (cardAlpha < 10) return;

        Color itemRawC1 = ParseOverlayColor(item.Color1Hex, Color.FromArgb(40, 80, 120));
        Color itemRawC2 = ParseOverlayColor(item.Color2Hex, Color.FromArgb(95, 45, 85));

        Color surfaceBase = Color.FromArgb(30, 31, 36);
        Color pillLeft = _settings.UseSourceBackgroundColors
            ? EnsureReadableBackdrop(ColorLerp(surfaceBase, itemRawC1, 0.48f))
            : surfaceBase;
        Color pillRight = _settings.UseSourceBackgroundColors
            ? EnsureReadableBackdrop(ColorLerp(surfaceBase, itemRawC2, 0.48f))
            : surfaceBase;

        var (activeHeroLeft, activeHeroRight) = ComputeItemHeroColors(item.Color1Hex, item.Color2Hex, item.IsSingleSource);
        Color bgLeft = ColorLerp(pillLeft, activeHeroLeft, activeBlend);
        Color bgRight = ColorLerp(pillRight, activeHeroRight, activeBlend);

        if (item.IsSingleSource) { bgRight = bgLeft; }

        Color finalLeft = Color.FromArgb(cardAlpha, bgLeft);
        Color finalRight = Color.FromArgb(cardAlpha, bgRight);

        float pillRadius = Math.Min(cardRect.Height / 2f, 12f * scale);
        float heroRadius = GetWin11CornerRadius(scale);
        float radius = pillRadius + (heroRadius - pillRadius) * activeBlend;

        using GraphicsPath cardPath = Win11CardPanel.CreateRoundRectF(cardRect, radius);
        FillRoundedCardGradient(g, cardPath, cardRect, finalLeft, finalRight);

        DrawActiveHUDContent(
            g,
            cardRect,
            item.Label1,
            item.Label2,
            item.RawSource1,
            item.RawSource2,
            _app1Vol,
            _app2Vol,
            scale,
            activeBlend,
            cardAlpha);

        int pillRimAlpha = (int)(cardAlpha * 0.45f);
        int heroRimAlpha = 105 + (int)Math.Round(90 * _wheelExpansion);
        int rimAlpha = (int)Math.Clamp(pillRimAlpha + (heroRimAlpha - pillRimAlpha) * activeBlend, 0, 220);

        using Pen rimPen = new Pen(Color.FromArgb(rimAlpha, 255, 255, 255), 1.0f + 0.3f * activeBlend);
        g.DrawPath(rimPen, cardPath);
    }
}