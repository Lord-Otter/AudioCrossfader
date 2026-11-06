using System.Drawing;

public partial class CrossfadeOverlayForm
{
    public sealed class MinimalCardFlipStyle : ICarouselStyle
    {
        public string DisplayName => "Card Flip";
        public CarouselCategory Category => CarouselCategory.Minimal;
        public int SortOrder => 20;

        public void Render(CrossfadeOverlayForm host, Graphics g, RectangleF heroRect, int baseH, float scale) =>
            host.DrawCompactCardFlipHero(g, heroRect, baseH, scale);
    }
}