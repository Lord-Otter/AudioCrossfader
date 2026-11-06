using System.Drawing;
using System.Drawing.Drawing2D;

public partial class CrossfadeOverlayForm
{
    public sealed class StackedRolodexStyle : ICarouselStyle
    {
        public string DisplayName => "Stacked Rolodex";
        public CarouselCategory Category => CarouselCategory.Full;
        public int SortOrder => 40;

        public void Render(CrossfadeOverlayForm host, Graphics g, RectangleF heroRect, int baseH, float scale) =>
            host.RenderWideDeckOrder(g, heroRect, baseH, scale, host.DrawStackedRolodexCard);
    }

    private void DrawStackedRolodexCard(
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

        float cardH = heroCardRect.Height * (0.72f + 0.28f * activeBlend);
        float widthRatio = Math.Clamp(1.0f - dist * 0.09f, 0.76f, 1.0f);
        float cardW = heroCardRect.Width * widthRatio;
        float cardX = heroCardRect.X + (heroCardRect.Width - cardW) / 2f;

        float heroCenterY = heroCardRect.Y + heroCardRect.Height / 2f;
        float peekStep = Math.Clamp(baseH * 0.42f, 22f, 52f);
        float stackedOffset = signedVisualOffset * peekStep * _wheelExpansion;

        float cardCenterY = heroCenterY + stackedOffset;
        RectangleF fullCardRect = new RectangleF(cardX, cardCenterY - cardH / 2f, cardW, cardH);
        RectangleF cardRect = isActiveSlot
            ? fullCardRect
            : ApplyAlwaysPreviewBlendIfActive(fullCardRect, signedVisualOffset, heroCardRect, scale);

        if (cardRect.Bottom < 0 || cardRect.Top > this.Height) return;

        bool alwaysPreviews = string.Equals(_settings.NeighborPreviewVisibility, "Always", StringComparison.OrdinalIgnoreCase);
        float baseVis = (alwaysPreviews && dist <= 1.25f) ? Math.Max(_wheelExpansion, 0.92f) : _wheelExpansion;
        float visibility = isActiveSlot
            ? 1.0f
            : Math.Clamp((1.0f - Math.Max(0f, dist - 1.0f) * 0.50f) * baseVis, 0f, 1f);
        int cardAlpha = (int)Math.Clamp(visibility * (230f + 25f * activeBlend), 0f, 255f);
        if (cardAlpha < 10) return;

        Color itemRawC1 = ParseOverlayColor(item.Color1Hex, Color.FromArgb(40, 80, 120));
        Color itemRawC2 = ParseOverlayColor(item.Color2Hex, Color.FromArgb(95, 45, 85));
        Color darkDeckBase = Color.FromArgb(20, 21, 25);

        float deckStrength = Math.Clamp(0.38f - (dist * 0.08f), 0.18f, 0.45f);
        Color deckLeft = _settings.UseSourceBackgroundColors
            ? EnsureReadableBackdrop(ColorLerp(darkDeckBase, itemRawC1, deckStrength))
            : darkDeckBase;
        Color deckRight = _settings.UseSourceBackgroundColors
            ? EnsureReadableBackdrop(ColorLerp(darkDeckBase, itemRawC2, deckStrength))
            : darkDeckBase;

        var (activeHeroLeft, activeHeroRight) = ComputeItemHeroColors(item.Color1Hex, item.Color2Hex, item.IsSingleSource);
        Color bgLeft = ColorLerp(deckLeft, activeHeroLeft, activeBlend);
        Color bgRight = ColorLerp(deckRight, activeHeroRight, activeBlend);

        if (item.IsSingleSource) bgRight = bgLeft;

        float radius = GetWin11CornerRadius(scale);
        using GraphicsPath cardPath = Win11CardPanel.CreateRoundRectF(cardRect, radius);

        if (activeBlend > 0.1f && _wheelExpansion > 0.05f)
        {
            int shadowAlpha = (int)Math.Clamp(110 * activeBlend * _wheelExpansion, 0, 120);
            for (int s = 2; s >= 1; s--)
            {
                RectangleF shadowRect = RectangleF.Inflate(cardRect, s * 1.5f, s * 2.5f);
                using GraphicsPath shadowPath = Win11CardPanel.CreateRoundRectF(shadowRect, radius + s);
                using SolidBrush shadowBrush = new SolidBrush(Color.FromArgb(shadowAlpha / s, 0, 0, 0));
                g.FillPath(shadowBrush, shadowPath);
            }
        }

        FillRoundedCardGradient(g, cardPath, cardRect, Color.FromArgb(cardAlpha, bgLeft), Color.FromArgb(cardAlpha, bgRight));

        if (dist > 0.35f)
        {
            float lipH = Math.Min(cardRect.Height * 0.45f, peekStep * 0.92f);
            float lipY = signedVisualOffset < 0 ? cardRect.Top + 2f : cardRect.Bottom - lipH - 2f;
            RectangleF lipRect = new RectangleF(cardRect.X, lipY, cardRect.Width, lipH);

            int textAlpha = (int)Math.Clamp(visibility * 235f, 0f, 235f);
            bool showName = _settings.OverlayIdentityMode == "Name" || _settings.OverlayIdentityMode == "Both";
            bool showIcon = _settings.OverlayIdentityMode == "Icon" || _settings.OverlayIdentityMode == "Both";

            if (item.IsSingleSource)
            {
                RectangleF singleZone = new RectangleF(lipRect.X + 14f * scale, lipRect.Y, lipRect.Width - 28f * scale, lipRect.Height);
                DrawCenteredPillIdentity(g, singleZone, item.Label1, item.RawSource1, textAlpha, scale * 0.82f, showName, showIcon);
            }
            else
            {
                float cx = lipRect.X + lipRect.Width / 2f;
                RectangleF leftZone = new RectangleF(lipRect.X + 12f * scale, lipRect.Y, (lipRect.Width / 2f) - 20f * scale, lipRect.Height);
                RectangleF rightZone = new RectangleF(cx + 8f * scale, lipRect.Y, (lipRect.Width / 2f) - 20f * scale, lipRect.Height);
                DrawPillSideIdentity(g, leftZone, item.Label1, item.RawSource1, textAlpha, scale * 0.82f, showName, showIcon, isRightSide: false);
                DrawPillSideIdentity(g, rightZone, item.Label2, item.RawSource2, textAlpha, scale * 0.82f, showName, showIcon, isRightSide: true);
            }
        }
        else
        {
            DrawActiveHUDContent(
                g, cardRect,
                item.Label1, item.Label2,
                item.RawSource1, item.RawSource2,
                _app1Vol, _app2Vol,
                scale, activeBlend, cardAlpha);
        }

        int rimAlpha = (int)Math.Clamp(cardAlpha * (0.38f + 0.50f * activeBlend), 0, 225);
        using Pen rimPen = new Pen(Color.FromArgb(rimAlpha, 255, 255, 255), 1.0f + 0.4f * activeBlend);
        g.DrawPath(rimPen, cardPath);
    }
}