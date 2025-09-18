// Copyright 2022-2025 Niantic.

using System;
using Niantic.Lightship.AR.API;

namespace Niantic.Lightship.AR.Subsystems
{
    internal interface IMeshDownloaderApi
    {
        public ArdkStatus ARDK_MeshDownloader_Create(IntPtr ardkHandle);

        public ArdkStatus ARDK_MeshDownloader_Destroy(IntPtr ardkHandle);

        public ArdkStatus ARDK_MeshDownloader_RequestLocationMesh(
            IntPtr ardkHandle,
            ArdkString payload,
            bool getTexture,
            out ulong requestIdOut,
            UInt32 maxSizeKb);

        public ArdkStatus ARDK_MeshDownloader_GetLocationMeshResults(
            IntPtr ardkHandle,
            ulong requestId,
            out MeshDownloadClient.ArkdMeshDownloaderResults resultsOut);

        public void ARDK_Release_Resource(IntPtr resource);
    }
}
