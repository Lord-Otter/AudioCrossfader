using System.Drawing;
using System.Drawing.Drawing2D;

public partial class CrossfadeOverlayForm
{
    public sealed class SplitFlapClockFlipStyle : ICarouselStyle
    {
        public string DisplayName => "Split-Flap Clock Flip";
        public CarouselCategory Category => CarouselCategory.Compact;
        public int SortOrder => 30;

        public void Render(CrossfadeOverlayForm host, Graphics g, RectangleF heroRect, int baseH, float scale) =>
            host.DrawCompactClockFlipHero(g, heroRect, baseH, scale);
    }

    private void DrawCompactClockFlipHero(Graphics g, RectangleF heroRect, int baseH, float scale)
    {
        float t = 1.0f - Math.Clamp(_compactTransitionProgress, 0f, 1f);
        float ease = 1.0f - MathF.Pow(1.0f - t, 3f);
        float radius = GetWin11CornerRadius(scale);
        float midY = heroRect.Y + heroRect.Height / 2f;
        float halfH = heroRect.Height / 2f;

        var (oldLeftCol, oldRightCol) = ComputeItemHeroColors(
            _outgoingCompactItem.Color1Hex,
            _outgoingCompactItem.Color2Hex,
            _outgoingCompactItem.IsSingleSource);

        void DrawCardHalf(RectangleF clipHalf, bool isIncoming, int shadeDelta)
        {
            GraphicsState st = g.Save();
            g.SetClip(clipHalf, CombineMode.Intersect);

            using GraphicsPath path = Win11CardPanel.CreateRoundRectF(heroRect, radius);
            if (isIncoming)
            {
                FillRoundedCardGradient(g, path, heroRect, _renderedBgLeft, _renderedBgRight);
                DrawActiveHUDContent(g, heroRect, _app1Label, _app2Label, _rawSource1, _rawSource2, _app1Vol, _app2Vol, scale, 1.0f);
            }
            else
            {
                FillRoundedCardGradient(g, path, heroRect, oldLeftCol, oldRightCol);
                DrawActiveHUDContent(
                    g, heroRect,
                    _outgoingCompactItem.Label1, _outgoingCompactItem.Label2,
                    _outgoingCompactItem.RawSource1, _outgoingCompactItem.RawSource2,
                    _app1Vol, _app2Vol, scale, 1.0f);
            }

            if (shadeDelta != 0)
            {
                Color shadeCol = shadeDelta > 0
                    ? Color.FromArgb(Math.Clamp(shadeDelta, 0, 140), 255, 255, 255)
                    : Color.FromArgb(Math.Clamp(-shadeDelta, 0, 180), 0, 0, 0);
                using SolidBrush shadeBrush = new SolidBrush(shadeCol);
                g.FillPath(shadeBrush, path);
            }

            using Pen borderPen = new Pen(Color.FromArgb(200, 255, 255, 255), 1.4f);
            g.DrawPath(borderPen, path);
            g.Restore(st);
        }

        RectangleF topHalfRect = new RectangleF(heroRect.X - 2f, heroRect.Y - 2f, heroRect.Width + 4f, halfH + 2f);
        RectangleF botHalfRect = new RectangleF(heroRect.X - 2f, midY, heroRect.Width + 4f, halfH + 2f);

        if (_compactTransitionDir >= 0)
        {
            DrawCardHalf(topHalfRect, isIncoming: true, shadeDelta: 0);
            DrawCardHalf(botHalfRect, isIncoming: false, shadeDelta: -35);

            if (ease < 0.5f)
            {
                float flapScaleY = Math.Max(0.04f, 1.0f - (ease * 2.0f));
                GraphicsState flapState = g.Save();
                g.TranslateTransform(0f, midY);
                g.ScaleTransform(1.0f, flapScaleY);
                g.TranslateTransform(0f, -midY);

                int shadow = -(int)((1.0f - flapScaleY) * 130f);
                DrawCardHalf(topHalfRect, isIncoming: false, shadeDelta: shadow);
                g.Restore(flapState);
            }
            else
            {
                float flapScaleY = Math.Max(0.04f, (ease - 0.5f) * 2.0f);
                GraphicsState flapState = g.Save();
                g.TranslateTransform(0f, midY);
                g.ScaleTransform(1.0f, flapScaleY);
                g.TranslateTransform(0f, -midY);

                int highlight = (int)((1.0f - flapScaleY) * 90f);
                DrawCardHalf(botHalfRect, isIncoming: true, shadeDelta: highlight);
                g.Restore(flapState);
            }
        }
        else
        {
            DrawCardHalf(topHalfRect, isIncoming: false, shadeDelta: -35);
            DrawCardHalf(botHalfRect, isIncoming: true, shadeDelta: 0);

            if (ease < 0.5f)
            {
                float flapScaleY = Math.Max(0.04f, 1.0f - (ease * 2.0f));
                GraphicsState flapState = g.Save();
                g.TranslateTransform(0f, midY);
                g.ScaleTransform(1.0f, flapScaleY);
                g.TranslateTransform(0f, -midY);

                int highlight = (int)((1.0f - flapScaleY) * 90f);
                DrawCardHalf(botHalfRect, isIncoming: false, shadeDelta: highlight);
                g.Restore(flapState);
            }
            else
            {
                float flapScaleY = Math.Max(0.04f, (ease - 0.5f) * 2.0f);
                GraphicsState flapState = g.Save();
                g.TranslateTransform(0f, midY);
                g.ScaleTransform(1.0f, flapScaleY);
                g.TranslateTransform(0f, -midY);

                int shadow = -(int)((1.0f - flapScaleY) * 130f);
                DrawCardHalf(topHalfRect, isIncoming: true, shadeDelta: shadow);
                g.Restore(flapState);
            }
        }

        using Pen hingePen = new Pen(Color.FromArgb(190, 10, 10, 14), Math.Max(1.5f, 1.8f * scale));
        g.DrawLine(hingePen, heroRect.Left + 1f, midY, heroRect.Right - 1f, midY);
    }
}