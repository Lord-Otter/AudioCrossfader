using System.Drawing;
using System.Drawing.Drawing2D;

public partial class CrossfadeOverlayForm
{
    public sealed class TopBottomMorphStyle : ICarouselStyle
    {
        public string DisplayName => "Top/Bottom Preview Morph";
        public CarouselCategory Category => CarouselCategory.Compact;
        public int SortOrder => 10;
        public bool DrawsOwnNeighborPreviews => true;

        public void Render(CrossfadeOverlayForm host, Graphics g, RectangleF heroRect, int baseH, float scale) =>
            host.DrawCompactTopBottomMorphHero(g, heroRect, baseH, scale);
    }

    private void DrawCompactTopBottomMorphHero(Graphics g, RectangleF heroRect, int baseH, float scale)
    {
        float t = 1.0f - Math.Clamp(_compactTransitionProgress, 0f, 1f);
        float ease = 0.5f - 0.5f * MathF.Cos(t * MathF.PI);

        var (topPreviewRect, botPreviewRect) = GetCompactTopBottomPreviewBoxRects(heroRect, scale);

        RectangleF incomingStartRect = _compactTransitionDir >= 0 ? topPreviewRect : botPreviewRect;
        RectangleF outgoingEndRect = _compactTransitionDir >= 0 ? botPreviewRect : topPreviewRect;

        if (!_suppressCarousel && _neighborPreviewVisibility > 0.01f && _cycleItems.Count > 1)
        {
            int topIdx = GetCarouselItemIndexForSlot(-1);
            int botIdx = GetCarouselItemIndexForSlot(1);

            int inAlpha = (int)(235 * ease * _neighborPreviewVisibility);
            if (_compactTransitionDir >= 0)
                DrawSingleTopBottomPreviewBar(g, topPreviewRect, _cycleItems[topIdx], scale, isTopSide: true, alpha: inAlpha);
            else
                DrawSingleTopBottomPreviewBar(g, botPreviewRect, _cycleItems[botIdx], scale, isTopSide: false, alpha: inAlpha);

            float recedeFade = Math.Clamp(1.0f - ease * 1.15f, 0f, 1f);
            int outPreviewAlpha = (int)(235 * recedeFade * _neighborPreviewVisibility);
            if (outPreviewAlpha > 8)
            {
                int oldReplacedIdx = _compactTransitionDir >= 0
                    ? GetCarouselItemIndexForSlot(2)
                    : GetCarouselItemIndexForSlot(-2);
                float shiftOutY = (_compactTransitionDir >= 0 ? 1f : -1f) * (12f * scale * ease);
                float shrinkW = outgoingEndRect.Width * (1.0f - 0.14f * ease);
                RectangleF recedingRect = new RectangleF(
                    outgoingEndRect.X + (outgoingEndRect.Width - shrinkW) / 2f,
                    outgoingEndRect.Y + shiftOutY,
                    shrinkW,
                    outgoingEndRect.Height * (1.0f - 0.15f * ease));
                DrawSingleTopBottomPreviewBar(g, recedingRect, _cycleItems[oldReplacedIdx], scale * (1f - 0.12f * ease), isTopSide: _compactTransitionDir < 0, alpha: outPreviewAlpha);
            }
        }

        RectangleF outRect = LerpRectF(heroRect, outgoingEndRect, ease);
        float outEmphasis = Math.Clamp(1.0f - ease * 1.25f, 0f, 1f);
        var (oldLeftCol, oldRightCol) = ComputeItemHeroColors(
            _outgoingCompactItem.Color1Hex,
            _outgoingCompactItem.Color2Hex,
            _outgoingCompactItem.IsSingleSource);

        float outRadius = Math.Clamp(7f * scale + (GetWin11CornerRadius(scale) - 7f * scale) * (1f - ease), 5f, 16f);
        using (GraphicsPath outPath = Win11CardPanel.CreateRoundRectF(outRect, outRadius))
        {
            FillRoundedCardGradient(g, outPath, outRect, oldLeftCol, oldRightCol);
            if (outEmphasis > 0.08f)
            {
                DrawActiveHUDContent(
                    g, outRect,
                    _outgoingCompactItem.Label1, _outgoingCompactItem.Label2,
                    _outgoingCompactItem.RawSource1, _outgoingCompactItem.RawSource2,
                    _app1Vol, _app2Vol, scale * (0.72f + 0.28f * (1f - ease)), outEmphasis, 240);
            }
            using Pen outBorderPen = new Pen(Color.FromArgb(170, 255, 255, 255), 1.2f);
            g.DrawPath(outBorderPen, outPath);
        }

        RectangleF inRect = LerpRectF(incomingStartRect, heroRect, ease);
        float inRadius = Math.Clamp(7f * scale + (GetWin11CornerRadius(scale) - 7f * scale) * ease, 5f, 16f);

        for (int s = 2; s >= 1; s--)
        {
            RectangleF shadowRect = RectangleF.Inflate(inRect, s * 1.8f, s * 1.8f);
            using GraphicsPath shadowPath = Win11CardPanel.CreateRoundRectF(shadowRect, inRadius + s);
            using SolidBrush shadowBrush = new SolidBrush(Color.FromArgb((int)(85 * ease) / s, 0, 0, 0));
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
                scale * (0.72f + 0.28f * ease), ease, 255);
            using Pen inBorderPen = new Pen(Color.FromArgb(220, 255, 255, 255), 1.4f);
            g.DrawPath(inBorderPen, inPath);
        }
    }
}