namespace Niantic.Lightship.AR.ARFoundation.Occlusion
{
    public struct OcclusionContext
    {
        /// Linear eye-depth from the camera to the occludee
        public float OccludeeEyeDepth;

        /// The aspect ratio of the camera (background) image
        internal float? CameraImageAspectRatio;

        /// Global setting
        public static OcclusionContext Shared;

        static OcclusionContext()
        {
            // Defaults
            Shared.CameraImageAspectRatio = null;
            ResetOccludee();
        }

        internal static void ResetOccludee()
        {
            Shared.OccludeeEyeDepth = 5.0f;
        }
    }
}
