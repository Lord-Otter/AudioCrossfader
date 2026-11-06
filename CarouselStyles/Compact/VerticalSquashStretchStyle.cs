using System.Drawing;
using System.Drawing.Drawing2D;

public partial class CrossfadeOverlayForm
{
    public sealed class VerticalSquashStretchStyle : ICarouselStyle
    {
        public string DisplayName => "Vertical Squash && Stretch";
        public CarouselCategory Category => CarouselCategory.Compact;
        public int SortOrder => 60;

        public void Render(CrossfadeOverlayForm host, Graphics g, RectangleF heroRect, int baseH, float scale) =>
            host.DrawCompactVerticalSquashStretchHero(g, heroRect, baseH, scale);
    }

    private void DrawCompactVerticalSquashStretchHero(Graphics g, RectangleF heroRect, int baseH, float scale)
    {
        float t = 1.0f - Math.Clamp(_compactTransitionProgress, 0f, 1f);
        float ease = 0.5f - 0.5f * MathF.Cos(t * MathF.PI);
        float radius = GetWin11CornerRadius(scale);

        var (oldLeftCol, oldRightCol) = ComputeItemHeroColors(
            _outgoingCompactItem.Color1Hex,
            _outgoingCompactItem.Color2Hex,
            _outgoingCompactItem.IsSingleSource);

        float totalH = heroRect.Height;
        float inRatio = Math.Clamp(ease, 0.001f, 1.0f);
        float outRatio = Math.Clamp(1.0f - inRatio, 0.001f, 1.0f);

        float splitY = _compactTransitionDir < 0
            ? heroRect.Top + totalH * outRatio
            : heroRect.Top + totalH * inRatio;

        RectangleF topSlice = new RectangleF(heroRect.Left, heroRect.Top, heroRect.Width, splitY - heroRect.Top);
        RectangleF botSlice = new RectangleF(heroRect.Left, splitY, heroRect.Width, heroRect.Bottom - splitY);

        RectangleF inRect = _compactTransitionDir < 0 ? botSlice : topSlice;
        RectangleF outRect = _compactTransitionDir < 0 ? topSlice : botSlice;

        using GraphicsPath outerClipPath = Win11CardPanel.CreateRoundRectF(heroRect, radius);
        using (SolidBrush backdropBrush = new SolidBrush(BackdropCharcoal))
        {
            g.FillPath(backdropBrush, outerClipPath);
        }

        GraphicsState outerClipState = g.Save();
        g.SetClip(outerClipPath, CombineMode.Intersect);

        void DrawVertSquashedCard(RectangleF targetBox, float scaleY, bool isIncoming, int alpha)
        {
            if (targetBox.Height < 0.5f || scaleY < 0.005f || alpha < 4) return;

            GraphicsState st = g.Save();
            g.SetClip(targetBox, CombineMode.Intersect);

            float centerX = heroRect.X + heroRect.Width / 2f;
            float pivotY = targetBox.Y;

            g.TranslateTransform(centerX, pivotY);
            g.ScaleTransform(1.0f, scaleY);
            g.TranslateTransform(-centerX, -heroRect.Y);

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
            DrawVertSquashedCard(outRect, outRatio, isIncoming: false, outAlpha);
        }
        DrawVertSquashedCard(inRect, inRatio, isIncoming: true, 255);

        if (ease > 0.02f && ease < 0.98f)
        {
            int seamAlpha = (int)(MathF.Sin(ease * MathF.PI) * 210f);
            using Pen seamPen = new Pen(Color.FromArgb(seamAlpha, 255, 255, 255), 1.5f * scale);
            g.DrawLine(seamPen, heroRect.Left + 1f, splitY, heroRect.Right - 1f, splitY);
        }

        g.Restore(outerClipState);

        using Pen borderPen = new Pen(Color.FromArgb(220, 255, 255, 255), 1.5f);
        g.DrawPath(borderPen, outerClipPath);
    }
}