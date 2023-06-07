namespace Niantic.Lightship.AR.ARFoundation
{
    internal struct OcclusionContext
    {
        /// Linear eye-depth from the camera to the occludee
        public float OccludeeEyeDepth;

        /// Global setting
        public static OcclusionContext Shared;

        static OcclusionContext()
        {
            // Defaults
            Shared.OccludeeEyeDepth = 1.0f;
        }
    }
}
