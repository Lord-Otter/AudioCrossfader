using System.Drawing;
using System.Drawing.Drawing2D;

public partial class CrossfadeOverlayForm
{
    public sealed class SquashStretchPushStyle : ICarouselStyle
    {
        public string DisplayName => "Squash && Stretch Push";
        public CarouselCategory Category => CarouselCategory.Minimal;
        public int SortOrder => 50;

        public void Render(CrossfadeOverlayForm host, Graphics g, RectangleF heroRect, int baseH, float scale) =>
            host.DrawCompactSquashStretchHero(g, heroRect, baseH, scale);
    }

    private void DrawCompactSquashStretchHero(Graphics g, RectangleF heroRect, int baseH, float scale)
    {
        float t = 1.0f - Math.Clamp(_compactTransitionProgress, 0f, 1f);
        float ease = 0.5f - 0.5f * MathF.Cos(t * MathF.PI);
        float radius = GetWin11CornerRadius(scale);

        var (oldLeftCol, oldRightCol) = ComputeItemHeroColors(
            _outgoingCompactItem.Color1Hex,
            _outgoingCompactItem.Color2Hex,
            _outgoingCompactItem.IsSingleSource);

        float totalW = heroRect.Width;
        float inRatio = Math.Clamp(ease, 0.001f, 1.0f);
        float outRatio = Math.Clamp(1.0f - inRatio, 0.001f, 1.0f);

        float splitX = _compactTransitionDir < 0
            ? heroRect.Left + totalW * outRatio
            : heroRect.Left + totalW * inRatio;

        RectangleF leftSlice = new RectangleF(heroRect.Left, heroRect.Y, splitX - heroRect.Left, heroRect.Height);
        RectangleF rightSlice = new RectangleF(splitX, heroRect.Y, heroRect.Right - splitX, heroRect.Height);

        RectangleF inRect = _compactTransitionDir < 0 ? rightSlice : leftSlice;
        RectangleF outRect = _compactTransitionDir < 0 ? leftSlice : rightSlice;

        using GraphicsPath outerClipPath = Win11CardPanel.CreateRoundRectF(heroRect, radius);
        using (SolidBrush backdropBrush = new SolidBrush(BackdropCharcoal))
        {
            g.FillPath(backdropBrush, outerClipPath);
        }

        GraphicsState outerClipState = g.Save();
        g.SetClip(outerClipPath, CombineMode.Intersect);

        void DrawSquashedCard(RectangleF targetBox, float scaleX, bool isIncoming, int alpha)
        {
            if (targetBox.Width < 0.5f || scaleX < 0.005f || alpha < 4) return;

            GraphicsState st = g.Save();
            g.SetClip(targetBox, CombineMode.Intersect);

            float pivotX = targetBox.X;
            float centerY = heroRect.Y + heroRect.Height / 2f;

            g.TranslateTransform(pivotX, centerY);
            g.ScaleTransform(scaleX, 1.0f);
            g.TranslateTransform(-heroRect.X, -centerY);

            if (isIncoming)
            {
                FillRoundedCardGradient(g, outerClipPath, heroRect, Color.FromArgb(alpha, _renderedBgLeft), Color.FromArgb(alpha, _renderedBgRight));
                DrawActiveHUDContent(g, heroRect, _app1Label, _app2Label, _rawSource1, _rawSource2, _app1Vol, _app2Vol, scale, 1.0f, alpha);
            }
            else
            {
                FillRoundedCardGradient(g, outerClipPath, heroRect, Color.FromArgb(alpha, oldLeftCol), Color.FromArgb(alpha, oldRightCol));
                DrawActiveHUDContent(
                    g, heroRect,
                    _outgoingCompactItem.Label1, _outgoingCompactItem.Label2,
                    _outgoingCompactItem.RawSource1, _outgoingCompactItem.RawSource2,
                    _app1Vol, _app2Vol, scale, 1.0f, alpha);
            }

            g.Restore(st);
        }

        int outAlpha = (int)Math.Clamp((1.0f - ease * 0.65f) * 255f, 0f, 255f);
        if (outRatio > 0.005f)
        {
            DrawSquashedCard(outRect, outRatio, isIncoming: false, outAlpha);
        }
        DrawSquashedCard(inRect, inRatio, isIncoming: true, 255);

        if (ease > 0.02f && ease < 0.98f)
        {
            int seamAlpha = (int)(MathF.Sin(ease * MathF.PI) * 210f);
            using Pen seamPen = new Pen(Color.FromArgb(seamAlpha, 255, 255, 255), 1.6f * scale);
            g.DrawLine(seamPen, splitX, heroRect.Top + 1f, splitX, heroRect.Bottom - 1f);
        }

        g.Restore(outerClipState);

        using Pen borderPen = new Pen(Color.FromArgb(220, 255, 255, 255), 1.5f);
        g.DrawPath(borderPen, outerClipPath);
    }
}