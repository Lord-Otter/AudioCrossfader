using System.Drawing;
using System.Drawing.Drawing2D;

public partial class CrossfadeOverlayForm
{
    public sealed class SidePreviewMorphStyle : ICarouselStyle
    {
        public string DisplayName => "Side Preview Morph";
        public CarouselCategory Category => CarouselCategory.Minimal;
        public int SortOrder => 10;
        public bool DrawsOwnNeighborPreviews => true;

        public void Render(CrossfadeOverlayForm host, Graphics g, RectangleF heroRect, int baseH, float scale) =>
            host.DrawMinimalSidePreviewMorphHero(g, heroRect, baseH, scale);
    }

    private void DrawMinimalSidePreviewMorphHero(Graphics g, RectangleF heroRect, int baseH, float scale)
    {
        float t = 1.0f - Math.Clamp(_compactTransitionProgress, 0f, 1f);
        float ease = 0.5f - 0.5f * MathF.Cos(t * MathF.PI);

        var (leftPreviewRect, rightPreviewRect) = GetMinimalSidePreviewBoxRects(heroRect, scale);

        RectangleF incomingStartRect = _compactTransitionDir >= 0 ? leftPreviewRect : rightPreviewRect;
        RectangleF outgoingEndRect = _compactTransitionDir >= 0 ? rightPreviewRect : leftPreviewRect;

        if (!_suppressCarousel && _neighborPreviewVisibility > 0.01f && _cycleItems.Count > 1)
        {
            int leftIdx = GetCarouselItemIndexForSlot(-1);
            int rightIdx = GetCarouselItemIndexForSlot(1);

            int inPreviewAlpha = (int)(230 * ease * _neighborPreviewVisibility);
            if (_compactTransitionDir >= 0)
                DrawSingleNeighborPreviewBox(g, leftPreviewRect, _cycleItems[leftIdx], scale, isLeftSide: true, alpha: inPreviewAlpha);
            else
                DrawSingleNeighborPreviewBox(g, rightPreviewRect, _cycleItems[rightIdx], scale, isLeftSide: false, alpha: inPreviewAlpha);

            float recedeFade = Math.Clamp(1.0f - ease * 1.15f, 0f, 1f);
            int outPreviewAlpha = (int)(230 * recedeFade * _neighborPreviewVisibility);
            if (outPreviewAlpha > 8)
            {
                int oldReplacedIdx = _compactTransitionDir >= 0
                    ? GetCarouselItemIndexForSlot(2)
                    : GetCarouselItemIndexForSlot(-2);
                float shiftOutX = (_compactTransitionDir >= 0 ? 1f : -1f) * (14f * scale * ease);
                float shrinkH = outgoingEndRect.Height * (1.0f - 0.25f * ease);
                float shrinkW = outgoingEndRect.Width * (1.0f - 0.20f * ease);
                RectangleF recedingRect = new RectangleF(
                    outgoingEndRect.X + shiftOutX + (outgoingEndRect.Width - shrinkW) / 2f,
                    outgoingEndRect.Y + (outgoingEndRect.Height - shrinkH) / 2f,
                    shrinkW,
                    shrinkH);
                DrawSingleNeighborPreviewBox(g, recedingRect, _cycleItems[oldReplacedIdx], scale * (1f - 0.15f * ease), isLeftSide: _compactTransitionDir < 0, alpha: outPreviewAlpha);
            }
        }

        RectangleF outRect = LerpRectF(heroRect, outgoingEndRect, ease);
        float outEmphasis = Math.Clamp(1.0f - ease * 1.25f, 0f, 1f);
        var (oldLeftCol, oldRightCol) = ComputeItemHeroColors(
            _outgoingCompactItem.Color1Hex,
            _outgoingCompactItem.Color2Hex,
            _outgoingCompactItem.IsSingleSource);

        float outRadius = Math.Clamp(6f * scale + (GetWin11CornerRadius(scale) - 6f * scale) * (1f - ease), 4f, 16f);
        using (GraphicsPath outPath = Win11CardPanel.CreateRoundRectF(outRect, outRadius))
        {
            FillRoundedCardGradient(g, outPath, outRect, oldLeftCol, oldRightCol);
            if (outEmphasis > 0.08f)
            {
                DrawActiveHUDContent(
                    g, outRect,
                    _outgoingCompactItem.Label1, _outgoingCompactItem.Label2,
                    _outgoingCompactItem.RawSource1, _outgoingCompactItem.RawSource2,
                    _app1Vol, _app2Vol, scale * (0.65f + 0.35f * (1f - ease)), outEmphasis, 240);
            }
            using Pen outBorderPen = new Pen(Color.FromArgb(170, 255, 255, 255), 1.2f);
            g.DrawPath(outBorderPen, outPath);
        }

        RectangleF inRect = LerpRectF(incomingStartRect, heroRect, ease);
        float inRadius = Math.Clamp(6f * scale + (GetWin11CornerRadius(scale) - 6f * scale) * ease, 4f, 16f);

        for (int s = 2; s >= 1; s--)
        {
            RectangleF shadowRect = RectangleF.Inflate(inRect, s * 1.8f, s * 1.8f);
            using GraphicsPath shadowPath = Win11CardPanel.CreateRoundRectF(shadowRect, inRadius + s);
            using SolidBrush shadowBrush = new SolidBrush(Color.FromArgb((int)(80 * ease) / s, 0, 0, 0));
            g.FillPath(shadowBrush, shadowPath);
        }

        using (GraphicsPath inPath = Win11CardPanel.CreateRoundRectF(inRect, inRadius))
        {
            FillRoundedCardGradient(g, inPath, inRect, _renderedBgLeft, _renderedBgRight);
            DrawActiveHUDContent(
                g, inRect,
                _app1Label, _app2Label,
                _rawSource1, _rawSource2,
                _app1Vol, _app2Vol,
                scale * (0.65f + 0.35f * ease), ease, 255);
            using Pen inBorderPen = new Pen(Color.FromArgb(220, 255, 255, 255), 1.4f);
            g.DrawPath(inBorderPen, inPath);
        }
    }
}