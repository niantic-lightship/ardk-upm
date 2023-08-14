using System;
using System.Runtime.InteropServices;

namespace Niantic.Lightship.AR
{
    internal class LightshipMeshingProvider
    {
        private static IntPtr _nativeProviderHandle;

        public LightshipMeshingProvider(IntPtr unityContext)
        {
            _nativeProviderHandle = Lightship_ARDK_Unity_Meshing_Provider_Construct(unityContext);
        }

        public static bool Configure
        (
            int frameRate,
            float maximumIntegrationDistance,
            float voxelSize,
            bool enableDistanceBasedVolumetricCleanup,
            float meshBlockSize,
            float meshCullingDistance,
            bool enableMeshDecimation
        )
        {
            return Lightship_ARDK_Unity_Meshing_Provider_Configure
            (
                frameRate,
                maximumIntegrationDistance,
                voxelSize,
                enableDistanceBasedVolumetricCleanup,
                meshBlockSize,
                meshCullingDistance,
                enableMeshDecimation
            );
        }

        [DllImport(LightshipPlugin.Name)]
        private static extern IntPtr Lightship_ARDK_Unity_Meshing_Provider_Construct(IntPtr unityContext);

        [DllImport(LightshipPlugin.Name)]
        private static extern bool Lightship_ARDK_Unity_Meshing_Provider_Configure
        (
            int frameRate,
            float maximumIntegrationDistance,
            float voxelSize,
            bool enableDistanceBasedVolumetricCleanup,
            float meshBlockSize,
            float meshCullingDistance,
            bool enableMeshDecimation
        );
    }
}
