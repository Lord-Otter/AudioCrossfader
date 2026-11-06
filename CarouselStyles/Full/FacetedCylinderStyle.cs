using System.Drawing;
using System.Drawing.Drawing2D;

public partial class CrossfadeOverlayForm
{
    public sealed class FacetedCylinderStyle : ICarouselStyle
    {
        public string DisplayName => "3D Faceted Cylinder";
        public CarouselCategory Category => CarouselCategory.Full;
        public int SortOrder => 50;

        public void Render(CrossfadeOverlayForm host, Graphics g, RectangleF heroRect, int baseH, float scale) =>
            host.RenderWideDeckOrder(g, heroRect, baseH, scale, host.DrawCylinderFacetCard);
    }

    private void DrawCylinderFacetCard(
        Graphics g,
        CycleDisplayItem item,
        bool isActiveSlot,
        float signedVisualOffset,
        RectangleF heroCardRect,
        int baseH,
        float scale)
    {
        float dist = Math.Abs(signedVisualOffset);
        if (dist > 2.55f) return;

        bool alwaysPreviews = string.Equals(_settings.NeighborPreviewVisibility, "Always", StringComparison.OrdinalIgnoreCase);
        float activeBlend = Math.Clamp(1.0f - dist, 0f, 1f);

        const float stepAngleRad = 27f * (MathF.PI / 180f);
        const float maxHorizonRad = 55f * (MathF.PI / 180f);
        const float maxGeomRad = 86f * (MathF.PI / 180f);
        float halfStep = stepAngleRad * 0.5f;

        float rawThetaTop = (signedVisualOffset - 0.5f) * stepAngleRad;
        float rawThetaBot = (signedVisualOffset + 0.5f) * stepAngleRad;

        if (rawThetaTop >= maxHorizonRad - 0.005f || rawThetaBot <= -maxHorizonRad + 0.005f)
            return;

        float geomThetaTop = Math.Clamp(rawThetaTop, -maxGeomRad, maxGeomRad);
        float geomThetaBot = Math.Clamp(rawThetaBot, -maxGeomRad, maxGeomRad);
        if (geomThetaBot - geomThetaTop <= 0.015f) return;

        float visThetaTop = Math.Clamp(rawThetaTop, -maxHorizonRad, maxHorizonRad);
        float visThetaBot = Math.Clamp(rawThetaBot, -maxHorizonRad, maxHorizonRad);
        float thetaCenter = (visThetaTop + visThetaBot) * 0.5f;
        float cosCenter = MathF.Cos(thetaCenter);

        float cosTop = MathF.Cos(geomThetaTop);
        float cosBot = MathF.Cos(geomThetaBot);

        float pillH = Math.Clamp(baseH * 0.50f, 28f, 64f);
        float maxOrbitDist = (baseH / 2f) + (pillH / 2f) + (6f * scale);
        float targetMaxWing = (maxOrbitDist - baseH / 2f) + (pillH * 0.74f);

        float heroCenterY = heroCardRect.Y + heroCardRect.Height / 2f;
        float sinHalf0 = MathF.Sin(halfStep);
        float sinHorizon = MathF.Sin(maxHorizonRad);

        float MapCylinderY(float theta)
        {
            float s = MathF.Sin(theta);
            float absS = Math.Abs(s);
            if (absS <= sinHalf0)
            {
                return heroCenterY + (s / sinHalf0) * (heroCardRect.Height * 0.5f);
            }
            float tWing = (absS - sinHalf0) / Math.Max(0.05f, sinHorizon - sinHalf0);
            return heroCenterY + Math.Sign(s) * ((heroCardRect.Height * 0.5f) + tWing * targetMaxWing);
        }

        float yTopFull = MapCylinderY(geomThetaTop);
        float yBotFull = MapCylinderY(geomThetaBot);

        float yHorizonTop = MapCylinderY(-maxHorizonRad);
        float yHorizonBot = MapCylinderY(maxHorizonRad);

        float baseW = heroCardRect.Width;
        float centerX = heroCardRect.X + baseW / 2f;

        float cosHorizon = MathF.Cos(maxHorizonRad);
        float normTop = Math.Clamp((cosTop - cosHorizon * 0.65f) / (1f - cosHorizon * 0.65f), 0f, 1f);
        float normBot = Math.Clamp((cosBot - cosHorizon * 0.65f) / (1f - cosHorizon * 0.65f), 0f, 1f);

        float wTopFull = baseW * (0.78f + 0.22f * MathF.Pow(normTop, 0.70f));
        float wBotFull = baseW * (0.78f + 0.22f * MathF.Pow(normBot, 0.70f));

        float exp = Math.Clamp(_wheelExpansion, 0f, 1f);
        if (!isActiveSlot)
        {
            RectangleF startRect = heroCardRect;
            if (alwaysPreviews)
            {
                var (topBox, bottomBox) = GetCompactTopBottomPreviewBoxRects(heroCardRect, scale);
                startRect = signedVisualOffset < 0 ? topBox : bottomBox;
            }
            yTopFull = startRect.Top + (yTopFull - startRect.Top) * exp;
            yBotFull = startRect.Bottom + (yBotFull - startRect.Bottom) * exp;
            wTopFull = startRect.Width + (wTopFull - startRect.Width) * exp;
            wBotFull = startRect.Width + (wBotFull - startRect.Width) * exp;

            yHorizonTop = startRect.Top + (yHorizonTop - startRect.Top) * exp;
            yHorizonBot = startRect.Bottom + (yHorizonBot - startRect.Bottom) * exp;
        }

        float fullFacetH = Math.Max(1f, yBotFull - yTopFull);
        float visTopY = Math.Clamp(yTopFull, yHorizonTop, yHorizonBot);
        float visBotY = Math.Clamp(yBotFull, yHorizonTop, yHorizonBot);
        float visFacetH = visBotY - visTopY;
        if (visFacetH <= 1.5f || visBotY < 0 || visTopY > this.Height) return;

        float tTopSlice = Math.Clamp((visTopY - yTopFull) / fullFacetH, 0f, 1f);
        float tBotSlice = Math.Clamp((visBotY - yTopFull) / fullFacetH, 0f, 1f);
        float wVisTop = wTopFull + (wBotFull - wTopFull) * tTopSlice;
        float wVisBot = wTopFull + (wBotFull - wTopFull) * tBotSlice;

        float horizonVisibility = Math.Clamp((2.52f - dist) / 0.48f, 0f, 1f);
        float baseVis = (alwaysPreviews && dist <= 1.25f) ? Math.Max(exp, 0.92f) : exp;
        float visibility = isActiveSlot ? 1.0f : Math.Clamp(horizonVisibility * baseVis, 0f, 1f);
        int cardAlpha = (int)Math.Clamp(visibility * (240f + 15f * activeBlend), 0f, 255f);
        if (cardAlpha < 6) return;

        float realCardMorph = _cycleItems.Count <= 2
            ? Math.Clamp(2.0f - dist, 0f, 1f)
            : Math.Clamp((2.35f - dist) / 0.65f, 0f, 1f);

        PointF[] trapPts = new PointF[]
        {
            new PointF(centerX - wVisTop / 2f, visTopY),
            new PointF(centerX + wVisTop / 2f, visTopY),
            new PointF(centerX + wVisBot / 2f, visBotY),
            new PointF(centerX - wVisBot / 2f, visBotY)
        };

        float maxHeroRadius = GetWin11CornerRadius(scale);
        float closeProgress = MathF.Pow(1.0f - exp, 1.35f);
        float dynamicCornerRadius = maxHeroRadius * closeProgress * (0.35f + 0.65f * activeBlend);

        using GraphicsPath facetPath = CreateRoundedTrapezoidPath(trapPts, dynamicCornerRadius);

        Color itemRawC1 = ParseOverlayColor(item.Color1Hex, Color.FromArgb(40, 80, 120));
        Color itemRawC2 = ParseOverlayColor(item.Color2Hex, Color.FromArgb(95, 45, 85));
        Color cylinderBase = Color.FromArgb(20, 22, 28);
        Color genericEmptyGray = Color.FromArgb(42, 45, 54);

        float lighting = Math.Clamp(MathF.Pow(cosCenter, 0.85f), 0.35f, 1.0f);
        Color emptyLitGray = ColorLerp(cylinderBase, genericEmptyGray, lighting);

        Color sourceFacetLeft = _settings.UseSourceBackgroundColors
            ? EnsureReadableBackdrop(ColorLerp(cylinderBase, itemRawC1, 0.78f * lighting))
            : ColorLerp(cylinderBase, DefaultDarkGrey, lighting);
        Color sourceFacetRight = _settings.UseSourceBackgroundColors
            ? EnsureReadableBackdrop(ColorLerp(cylinderBase, itemRawC2, 0.78f * lighting))
            : ColorLerp(cylinderBase, DefaultDarkGrey, lighting);

        if (item.IsSingleSource) sourceFacetRight = sourceFacetLeft;

        Color facetLeft = ColorLerp(emptyLitGray, sourceFacetLeft, realCardMorph);
        Color facetRight = ColorLerp(emptyLitGray, sourceFacetRight, realCardMorph);

        var (activeHeroLeft, activeHeroRight) = ComputeItemHeroColors(item.Color1Hex, item.Color2Hex, item.IsSingleSource);
        Color bgLeft = ColorLerp(facetLeft, activeHeroLeft, activeBlend);
        Color bgRight = ColorLerp(facetRight, activeHeroRight, activeBlend);
        if (item.IsSingleSource) bgRight = bgLeft;

        float maxW = Math.Max(wVisTop, wVisBot);
        RectangleF boundsRect = new RectangleF(centerX - maxW / 2f, visTopY, maxW, visFacetH);
        FillRoundedCardGradient(g, facetPath, boundsRect, Color.FromArgb(cardAlpha, bgLeft), Color.FromArgb(cardAlpha, bgRight));

        int horizonDarkAlpha = (int)Math.Clamp((1.0f - cosCenter) * 210f * exp, 0f, 155f);
        if (horizonDarkAlpha > 4)
        {
            Color nearEdgeShade = Color.FromArgb((int)(horizonDarkAlpha * 0.15f), 0, 0, 0);
            Color farEdgeShade = Color.FromArgb(horizonDarkAlpha, 0, 0, 0);
            Color topShade = signedVisualOffset < 0 ? farEdgeShade : nearEdgeShade;
            Color botShade = signedVisualOffset < 0 ? nearEdgeShade : farEdgeShade;

            using LinearGradientBrush depthBrush = new LinearGradientBrush(
                new PointF(centerX, visTopY - 1f),
                new PointF(centerX, visBotY + 1f),
                topShade,
                botShade);
            g.FillPath(depthBrush, facetPath);
        }

        if (visFacetH >= 5f && realCardMorph > 0.04f)
        {
            GraphicsState contentState = g.Save();
            g.SetClip(facetPath, CombineMode.Intersect);

            float unclampedMidY = (yTopFull + yBotFull) / 2f;
            float scaleY = Math.Clamp(fullFacetH / Math.Max(1f, heroCardRect.Height), 0.25f, 1.0f);
            float avgW = (wTopFull + wBotFull) * 0.5f;
            float scaleX = Math.Clamp(avgW / Math.Max(1f, heroCardRect.Width), 0.78f, 1.0f);

            g.TranslateTransform(centerX, unclampedMidY);
            g.ScaleTransform(scaleX, scaleY);
            g.TranslateTransform(-centerX, -(heroCardRect.Y + heroCardRect.Height / 2f));

            int contentAlpha = (int)Math.Clamp(cardAlpha * realCardMorph * Math.Clamp(cosCenter * 1.15f, 0.45f, 1.0f), 0f, 255f);
            if (contentAlpha > 6)
            {
                DrawActiveHUDContent(
                    g,
                    heroCardRect,
                    item.Label1,
                    item.Label2,
                    item.RawSource1,
                    item.RawSource2,
                    _app1Vol,
                    _app2Vol,
                    scale,
                    activeBlend,
                    contentAlpha);
            }

            g.Restore(contentState);
        }

        int sidePenAlpha = (int)Math.Clamp(cardAlpha * (0.38f + 0.52f * activeBlend), 0, 225);
        using (Pen facetBorderPen = new Pen(Color.FromArgb(sidePenAlpha, 255, 255, 255), 1.0f + 0.5f * activeBlend))
        {
            g.DrawPath(facetBorderPen, facetPath);
        }

        int ridgeAlpha = (int)Math.Clamp(cardAlpha * exp * (0.55f + 0.40f * activeBlend), 0, 240);
        if (ridgeAlpha > 8 && dynamicCornerRadius < 2.5f * scale)
        {
            using Pen ridgePen = new Pen(Color.FromArgb(ridgeAlpha, 255, 255, 255), 1.4f * scale);
            if (signedVisualOffset <= 0.1f)
                g.DrawLine(ridgePen, trapPts[3], trapPts[2]);
            if (signedVisualOffset >= -0.1f)
                g.DrawLine(ridgePen, trapPts[0], trapPts[1]);
        }
    }
}