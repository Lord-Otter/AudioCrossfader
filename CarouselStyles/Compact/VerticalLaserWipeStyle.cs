using System.Drawing;
using System.Drawing.Drawing2D;

public partial class CrossfadeOverlayForm
{
    public sealed class VerticalLaserWipeStyle : ICarouselStyle
    {
        public string DisplayName => "Vertical Laser Wipe";
        public CarouselCategory Category => CarouselCategory.Compact;
        public int SortOrder => 50;

        public void Render(CrossfadeOverlayForm host, Graphics g, RectangleF heroRect, int baseH, float scale) =>
            host.DrawCompactVerticalLaserWipeHero(g, heroRect, baseH, scale);
    }

    private void DrawCompactVerticalLaserWipeHero(Graphics g, RectangleF heroRect, int baseH, float scale)
    {
        float t = 1.0f - Math.Clamp(_compactTransitionProgress, 0f, 1f);
        float ease = 0.5f - 0.5f * MathF.Cos(t * MathF.PI);
        float radius = GetWin11CornerRadius(scale);

        var (oldLeftCol, oldRightCol) = ComputeItemHeroColors(
            _outgoingCompactItem.Color1Hex,
            _outgoingCompactItem.Color2Hex,
            _outgoingCompactItem.IsSingleSource);

        using GraphicsPath cardPath = Win11CardPanel.CreateRoundRectF(heroRect, radius);

        float lineY = _compactTransitionDir >= 0
            ? heroRect.Top + heroRect.Height * ease
            : heroRect.Bottom - heroRect.Height * ease;

        RectangleF newRegion = _compactTransitionDir >= 0
            ? new RectangleF(heroRect.Left - 2f, heroRect.Top - 2f, heroRect.Width + 4f, (lineY - heroRect.Top) + 2f)
            : new RectangleF(heroRect.Left - 2f, lineY, heroRect.Width + 4f, (heroRect.Bottom - lineY) + 2f);

        RectangleF oldRegion = _compactTransitionDir >= 0
            ? new RectangleF(heroRect.Left - 2f, lineY, heroRect.Width + 4f, (heroRect.Bottom - lineY) + 2f)
            : new RectangleF(heroRect.Left - 2f, heroRect.Top - 2f, heroRect.Width + 4f, (lineY - heroRect.Top) + 2f);

        if (oldRegion.Height > 1f)
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

        if (newRegion.Height > 1f)
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
            using Pen glowPen = new Pen(Color.FromArgb(75 / w, accentGlow), w * 3.0f * scale);
            g.DrawLine(glowPen, heroRect.Left, lineY, heroRect.Right, lineY);
        }
        using (Pen corePen = new Pen(Color.FromArgb(245, 255, 255, 255), 1.8f * scale))
        {
            g.DrawLine(corePen, heroRect.Left, lineY, heroRect.Right, lineY);
        }
        g.Restore(stLaser);

        using Pen borderPen = new Pen(Color.FromArgb(220, 255, 255, 255), 1.5f);
        g.DrawPath(borderPen, cardPath);
    }
}