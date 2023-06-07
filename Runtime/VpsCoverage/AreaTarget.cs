namespace Niantic.Lightship.AR
{
    public struct AreaTarget
    {
        public CoverageArea Area { get; }
        public LocalizationTarget Target { get; }

        public AreaTarget(CoverageArea area, LocalizationTarget target)
        {
            Area = area;
            Target = target;
        }
    }
}
