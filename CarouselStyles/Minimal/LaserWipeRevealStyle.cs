using System.Drawing;
using System.Drawing.Drawing2D;

public partial class CrossfadeOverlayForm
{
    public sealed class LaserWipeRevealStyle : ICarouselStyle
    {
        public string DisplayName => "Laser Wipe Reveal";
        public CarouselCategory Category => CarouselCategory.Minimal;
        public int SortOrder => 40;

        public void Render(CrossfadeOverlayForm host, Graphics g, RectangleF heroRect, int baseH, float scale) =>
            host.DrawCompactLaserWipeHero(g, heroRect, baseH, scale);
    }

    private void DrawCompactLaserWipeHero(Graphics g, RectangleF heroRect, int baseH, float scale)
    {
        float t = 1.0f - Math.Clamp(_compactTransitionProgress, 0f, 1f);
        float ease = 0.5f - 0.5f * MathF.Cos(t * MathF.PI);
        float radius = GetWin11CornerRadius(scale);

        var (oldLeftCol, oldRightCol) = ComputeItemHeroColors(
            _outgoingCompactItem.Color1Hex,
            _outgoingCompactItem.Color2Hex,
            _outgoingCompactItem.IsSingleSource);

        using GraphicsPath cardPath = Win11CardPanel.CreateRoundRectF(heroRect, radius);

        float lineX = _compactTransitionDir >= 0
            ? heroRect.Left + heroRect.Width * ease
            : heroRect.Right - heroRect.Width * ease;

        RectangleF newRegion = _compactTransitionDir >= 0
            ? new RectangleF(heroRect.Left - 2f, heroRect.Top - 2f, (lineX - heroRect.Left) + 2f, heroRect.Height + 4f)
            : new RectangleF(lineX, heroRect.Top - 2f, (heroRect.Right - lineX) + 2f, heroRect.Height + 4f);

        RectangleF oldRegion = _compactTransitionDir >= 0
            ? new RectangleF(lineX, heroRect.Top - 2f, (heroRect.Right - lineX) + 2f, heroRect.Height + 4f)
            : new RectangleF(heroRect.Left - 2f, heroRect.Top - 2f, (lineX - heroRect.Left) + 2f, heroRect.Height + 4f);

        if (oldRegion.Width > 1f)
        {
            GraphicsState stOld = g.Save();
            g.SetClip(cardPath, CombineMode.Intersect);
            g.SetClip(oldRegion, CombineMode.Intersect);
            FillRoundedCardGradient(g, cardPath, heroRect, oldLeftCol, oldRightCol);
            DrawActiveHUDContent(
                g, heroRect,
                _outgoingCompactItem.Label1, _outgoingCompactItem.Label2,
                _outgoingCompactItem.RawSource1, _outgoingCompactItem.RawSource2,
                _app1Vol, _app2Vol, scale, 1.0f);
            g.Restore(stOld);
        }

        if (newRegion.Width > 1f)
        {
            GraphicsState stNew = g.Save();
            g.SetClip(cardPath, CombineMode.Intersect);
            g.SetClip(newRegion, CombineMode.Intersect);
            FillRoundedCardGradient(g, cardPath, heroRect, _renderedBgLeft, _renderedBgRight);
            DrawActiveHUDContent(g, heroRect, _app1Label, _app2Label, _rawSource1, _rawSource2, _app1Vol, _app2Vol, scale, 1.0f);
            g.Restore(stNew);
        }

        GraphicsState stLaser = g.Save();
        g.SetClip(cardPath, CombineMode.Intersect);
        Color accentGlow = ParseOverlayColor(_settings.UnmutedColor, Color.FromArgb(0, 195, 255));
        for (int w = 3; w >= 1; w--)
        {
            using Pen glowPen = new Pen(Color.FromArgb(75 / w, accentGlow), w * 3.2f * scale);
            g.DrawLine(glowPen, lineX, heroRect.Top, lineX, heroRect.Bottom);
        }
        using (Pen corePen = new Pen(Color.FromArgb(245, 255, 255, 255), 2.0f * scale))
        {
            g.DrawLine(corePen, lineX, heroRect.Top, lineX, heroRect.Bottom);
        }
        g.Restore(stLaser);

        using Pen borderPen = new Pen(Color.FromArgb(220, 255, 255, 255), 1.5f);
        g.DrawPath(borderPen, cardPath);
    }
}