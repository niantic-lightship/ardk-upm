// Copyright 2022-2024 Niantic.
using System;
using System.Runtime.InteropServices;
using Niantic.Lightship.AR.Core;

namespace Niantic.Lightship.AR.Subsystems.Meshing
{
    internal class LightshipMeshingProvider
    {
        public LightshipMeshingProvider(IntPtr unityContext)
        {
            Lightship_ARDK_Unity_Meshing_Provider_Construct(unityContext);
        }

        public static bool Configure
        (
            int frameRate,
            float maximumIntegrationDistance,
            float voxelSize,
            bool enableDistanceBasedVolumetricCleanup,
            float meshBlockSize,
            float meshCullingDistance,
            bool enableMeshDecimation,
            bool enableMeshFiltering,
            bool enableFilteringAllowList,
            int packedAllowList,
            bool enableFilteringBlockList,
            int packedBlockList
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
                enableMeshDecimation,
                enableMeshFiltering,
                enableFilteringAllowList,
                packedAllowList,
                enableFilteringBlockList,
                packedBlockList
            );
        }

        public static ulong GetLastMeshUpdateTime()
        {
            return Lightship_ARDK_Unity_Meshing_Provider_LatestTimestamp();
        }

        [DllImport(LightshipPlugin.Name)]
        private static extern IntPtr Lightship_ARDK_Unity_Meshing_Provider_Construct(IntPtr unityContext);

        [DllImport(LightshipPlugin.Name)]
        private static extern ulong Lightship_ARDK_Unity_Meshing_Provider_LatestTimestamp();

        [DllImport(LightshipPlugin.Name)]
        private static extern bool Lightship_ARDK_Unity_Meshing_Provider_Configure
        (
            int frameRate,
            float maximumIntegrationDistance,
            float voxelSize,
            bool enableDistanceBasedVolumetricCleanup,
            float meshBlockSize,
            float meshCullingDistance,
            bool enableMeshDecimation,
            bool enableMeshFiltering,
            bool enableFilteringAllowList,
            int packedAllowList,
            bool enableFilteringBlockList,
            int packedBlockList
        );
    }
}
