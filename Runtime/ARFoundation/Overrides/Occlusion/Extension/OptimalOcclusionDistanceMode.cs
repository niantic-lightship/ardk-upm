// Copyright 2022-2024 Niantic.

namespace Niantic.Lightship.AR.Occlusion
{
    public partial class LightshipOcclusionExtension
    {
        /// <summary>
        /// The sampling mode for determining the distance to the occluder.
        /// This distance is used to transform the depth buffer to provide
        /// accurate occlusions.
        /// </summary>
        public enum OptimalOcclusionDistanceMode
        {
            /// Take a few samples of the full depth buffer to
            /// determine the closest occluder on the screen.
            /// This will provide the best available occlusions
            /// if there are many occluded virtual objects of similar
            /// size and importance.
            ClosestOccluder,

            /// Sample the sub-region of the buffer that is directly over
            /// the main CG object, to determine the distance of its occluder
            /// in the world. This will provide the best quality occlusions
            /// if there is only one occluded virtual object, or if one is more
            /// visually prominent than the others
            SpecifiedGameObject,

            /// Stabilize the depth buffer relative to a pre-determined,
            /// unchanging depth. Not recommended if there are occluded virtual objects
            /// in the scene, but is more performant and thus optimal when
            /// occlusions are not needed.
            Static,
        }
    }
}
