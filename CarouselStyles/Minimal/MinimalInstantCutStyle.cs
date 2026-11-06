using System.Drawing;

public partial class CrossfadeOverlayForm
{
    public sealed class MinimalInstantCutStyle : ICarouselStyle
    {
        public string DisplayName => "Instant Cut";
        public CarouselCategory Category => CarouselCategory.Minimal;
        public int SortOrder => 60;
        public bool IsInstantCut => true;

        public void Render(CrossfadeOverlayForm host, Graphics g, RectangleF heroRect, int baseH, float scale) =>
            host.DrawStandardHeroCard(g, heroRect, scale);
    }
}