using System.Drawing;

public partial class CrossfadeOverlayForm
{
    public sealed class CompactInstantCutStyle : ICarouselStyle
    {
        public string DisplayName => "Instant Cut";
        public CarouselCategory Category => CarouselCategory.Compact;
        public int SortOrder => 70;
        public bool IsInstantCut => true;

        public void Render(CrossfadeOverlayForm host, Graphics g, RectangleF heroRect, int baseH, float scale) =>
            host.DrawStandardHeroCard(g, heroRect, scale);
    }
}