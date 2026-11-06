using System.Drawing;
using System.Drawing.Drawing2D;

public partial class CrossfadeOverlayForm
{
    public sealed class VerticalSwooshStyle : ICarouselStyle
    {
        public string DisplayName => "Vertical Swoosh";
        public CarouselCategory Category => CarouselCategory.Compact;
        public int SortOrder => 40;

        public void Render(CrossfadeOverlayForm host, Graphics g, RectangleF heroRect, int baseH, float scale) =>
            host.DrawCompactVerticalSwooshHero(g, heroRect, baseH, scale);
    }

    private void DrawCompactVerticalSwooshHero(Graphics g, RectangleF heroRect, int baseH, float scale)
    {
        float t = 1.0f - Math.Clamp(_compactTransitionProgress, 0f, 1f);
        float ease = 1.0f - MathF.Pow(1.0f - t, 3f);
        float radius = GetWin11CornerRadius(scale);

        var (oldLeftCol, oldRightCol) = ComputeItemHeroColors(
            _outgoingCompactItem.Color1Hex,
            _outgoingCompactItem.Color2Hex,
            _outgoingCompactItem.IsSingleSource);

        using GraphicsPath clipPath = Win11CardPanel.CreateRoundRectF(heroRect, radius);
        using SolidBrush baseFill = new SolidBrush(BackdropCharcoal);
        g.FillPath(baseFill, clipPath);

        GraphicsState st = g.Save();
        g.SetClip(clipPath, CombineMode.Intersect);

        float travel = heroRect.Height * 0.95f;
        float outShiftY = _compactTransitionDir * travel * ease;
        float inShiftY = -_compactTransitionDir * travel * (1.0f - ease);

        int outAlpha = (int)Math.Clamp((1.0f - ease) * 255f, 0f, 255f);
        if (outAlpha > 8)
        {
            RectangleF outRect = new RectangleF(heroRect.X, heroRect.Y + outShiftY, heroRect.Width, heroRect.Height);
            using GraphicsPath outPath = Win11CardPanel.CreateRoundRectF(outRect, radius);
            FillRoundedCardGradient(g, outPath, outRect, Color.FromArgb(outAlpha, oldLeftCol), Color.FromArgb(outAlpha, oldRightCol));
            DrawActiveHUDContent(
                g, outRect,
                _outgoingCompactItem.Label1, _outgoingCompactItem.Label2,
                _outgoingCompactItem.RawSource1, _outgoingCompactItem.RawSource2,
                _app1Vol, _app2Vol, scale, 1.0f, outAlpha);
        }

        int inAlpha = (int)Math.Clamp(ease * 255f, 0f, 255f);
        if (inAlpha > 8)
        {
            RectangleF inRect = new RectangleF(heroRect.X, heroRect.Y + inShiftY, heroRect.Width, heroRect.Height);
            using GraphicsPath inPath = Win11CardPanel.CreateRoundRectF(inRect, radius);
            FillRoundedCardGradient(g, inPath, inRect, Color.FromArgb(inAlpha, _renderedBgLeft), Color.FromArgb(inAlpha, _renderedBgRight));
            DrawActiveHUDContent(g, inRect, _app1Label, _app2Label, _rawSource1, _rawSource2, _app1Vol, _app2Vol, scale, 1.0f, inAlpha);
        }

        g.Restore(st);

        using Pen borderPen = new Pen(Color.FromArgb(220, 255, 255, 255), 1.5f);
        g.DrawPath(borderPen, clipPath);
    }
}