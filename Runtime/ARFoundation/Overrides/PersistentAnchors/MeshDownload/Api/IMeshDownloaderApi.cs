// Copyright 2022-2025 Niantic.

using System;

namespace Niantic.Lightship.AR.Subsystems
{
    internal interface IMeshDownloaderApi
    {
        public MeshDownloadClient.ARDK_Status ARDK_MeshDownloader_Create(IntPtr ardkHandle);

        public MeshDownloadClient.ARDK_Status ARDK_MeshDownloader_Destroy(IntPtr ardkHandle);

        public MeshDownloadClient.ARDK_Status ARDK_MeshDownloader_RequestLocationMesh(
            IntPtr ardkHandle,
            ARDK_String payload,
            bool getTexture,
            out ulong requestIdOut,
            UInt32 maxSizeKb);

        public MeshDownloadClient.ARDK_Status ARDK_MeshDownloader_GetLocationMeshResults(
            IntPtr ardkHandle,
            ulong requestId,
            out MeshDownloadClient.ARDK_MeshDownloader_Results resultsOut);

        public void ARDK_Release_Resource(IntPtr resource);
    }
}
