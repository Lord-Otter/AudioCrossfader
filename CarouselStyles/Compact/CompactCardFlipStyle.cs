using System.Drawing;
using System.Drawing.Drawing2D;

public partial class CrossfadeOverlayForm
{
    public sealed class CompactCardFlipStyle : ICarouselStyle
    {
        public string DisplayName => "Card Flip";
        public CarouselCategory Category => CarouselCategory.Compact;
        public int SortOrder => 20;

        public void Render(CrossfadeOverlayForm host, Graphics g, RectangleF heroRect, int baseH, float scale) =>
            host.DrawCompactCardFlipHero(g, heroRect, baseH, scale);
    }

    internal void DrawCompactCardFlipHero(Graphics g, RectangleF heroRect, int baseH, float scale)
    {
        float t = 1.0f - Math.Clamp(_compactTransitionProgress, 0f, 1f);
        float ease = 0.5f - 0.5f * MathF.Cos(t * MathF.PI);
        bool showNewHalf = (ease >= 0.5f);
        float flipScaleY = Math.Max(0.04f, Math.Abs(MathF.Cos(ease * MathF.PI)));

        int shadeDelta = (int)((1.0f - flipScaleY) * 145f) * (_compactTransitionDir >= 0 ? (showNewHalf ? 1 : -1) : (showNewHalf ? -1 : 1));
        float radius = GetWin11CornerRadius(scale);

        if (!_suppressCarousel && _neighborPreviewVisibility > 0.01f && _cycleItems.Count > 1)
        {
            for (int s = 2; s >= 1; s--)
            {
                RectangleF shadowRect = RectangleF.Inflate(heroRect, s * 1.8f, s * 1.8f * flipScaleY);
                using GraphicsPath shadowPath = Win11CardPanel.CreateRoundRectF(shadowRect, radius + s);
                using SolidBrush shadowBrush = new SolidBrush(Color.FromArgb((int)(75 * _neighborPreviewVisibility) / s, 0, 0, 0));
                g.FillPath(shadowBrush, shadowPath);
            }
        }

        GraphicsState stHero = g.Save();
        float heroCy = heroRect.Y + heroRect.Height / 2f;
        g.TranslateTransform(0f, heroCy);
        g.ScaleTransform(1f, flipScaleY);
        g.TranslateTransform(0f, -heroCy);

        using GraphicsPath heroPath = Win11CardPanel.CreateRoundRectF(heroRect, radius);
        if (showNewHalf)
        {
            FillRoundedCardGradient(g, heroPath, heroRect, _renderedBgLeft, _renderedBgRight);
            DrawActiveHUDContent(g, heroRect, _app1Label, _app2Label, _rawSource1, _rawSource2, _app1Vol, _app2Vol, scale, 1.0f);
        }
        else
        {
            var (oldLeftCol, oldRightCol) = ComputeItemHeroColors(
                _outgoingCompactItem.Color1Hex,
                _outgoingCompactItem.Color2Hex,
                _outgoingCompactItem.IsSingleSource);
            FillRoundedCardGradient(g, heroPath, heroRect, oldLeftCol, oldRightCol);
            DrawActiveHUDContent(
                g, heroRect,
                _outgoingCompactItem.Label1, _outgoingCompactItem.Label2,
                _outgoingCompactItem.RawSource1, _outgoingCompactItem.RawSource2,
                _app1Vol, _app2Vol, scale, 1.0f);
        }

        if (Math.Abs(shadeDelta) > 4)
        {
            Color shadeCol = shadeDelta > 0
                ? Color.FromArgb(Math.Clamp(shadeDelta, 0, 110), 255, 255, 255)
                : Color.FromArgb(Math.Clamp(-shadeDelta, 0, 165), 0, 0, 0);
            using SolidBrush shadeBrush = new SolidBrush(shadeCol);
            g.FillPath(shadeBrush, heroPath);
        }

        using Pen borderPen = new Pen(Color.FromArgb(220, 255, 255, 255), 1.5f);
        g.DrawPath(borderPen, heroPath);
        g.Restore(stHero);
    }
}