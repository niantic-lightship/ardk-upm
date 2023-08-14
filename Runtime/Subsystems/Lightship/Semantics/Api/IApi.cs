using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;

namespace Niantic.Lightship.AR.SemanticsSubsystem
{
    internal interface IApi
    {
        public IntPtr Construct(IntPtr unityContext);

        public void Start(IntPtr nativeProviderHandle);

        public void Stop(IntPtr nativeProviderHandle);

        public bool TryPrepareSubsystem(IntPtr nativeProviderHandle);

        public void Configure(IntPtr nativeProviderHandle, UInt32 framesPerSecond, UInt32 numThresholds, IntPtr thresholds);

        public void Destruct(IntPtr nativeProviderHandle);

        public bool TryGetSemanticChannel
        (
            IntPtr nativeProviderHandle,
            string channelName,
            XRCameraParams? cameraParams,
            out XRTextureDescriptor semanticsChannelDescriptor,
            out Matrix4x4 samplerMatrix
        );

        public bool TryAcquireSemanticChannelCPUImage(IntPtr nativeProviderHandle, string channelName, out LightshipCpuBuffer cpuBuffer);

        public void DisposeCPUImage(IntPtr nativeProviderHandle,  IntPtr resHandle);

        public bool TryGetPackedSemanticChannels
        (
            IntPtr nativeProviderHandle,
            XRCameraParams? cameraParams,
            out XRTextureDescriptor packedSemanticsDescriptor,
            out Matrix4x4 samplerMatrix
        );

        public bool TryAcquirePackedSemanticChannelsCPUImage(IntPtr nativeProviderHandle, out LightshipCpuBuffer cpuBuffer);

        public bool TryCalculateSamplerMatrix
        (
            IntPtr nativeProviderHandle,
            IntPtr resourceHandle,
            XRCameraParams cameraParams,
            Matrix4x4 pose,
            float backProjectionPlane,
            out Matrix4x4 result
        );

        public bool TryGetChannelNames(IntPtr nativeProviderHandle, out List<string> semanticChannelNames);
    }
}
