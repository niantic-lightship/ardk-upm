// Copyright 2023 Niantic, Inc. All Rights Reserved.
using System;
using System.Collections.Generic;
using Niantic.Lightship.AR.Subsystems.Common;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;

namespace Niantic.Lightship.AR.Subsystems.Semantics
{
    internal interface IApi
    {
        public IntPtr Construct(IntPtr unityContext);

        public void Start(IntPtr nativeProviderHandle);

        public void Stop(IntPtr nativeProviderHandle);

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

        public bool TryAcquireSemanticChannelCpuImage
        (
            IntPtr nativeProviderHandle,
            string channelName,
            XRCameraParams? cameraParams,
            out XRCpuImage cpuImage,
            out Matrix4x4 samplerMatrix
        );

        public bool TryGetPackedSemanticChannels
        (
            IntPtr nativeProviderHandle,
            XRCameraParams? cameraParams,
            out XRTextureDescriptor packedSemanticsDescriptor,
            out Matrix4x4 samplerMatrix
        );

        public bool TryAcquirePackedSemanticChannelsCpuImage
        (
            IntPtr nativeProviderHandle,
            XRCameraParams? cameraParams,
            out XRCpuImage cpuImage,
            out Matrix4x4 samplerMatrix
        );

        public bool TryGetChannelNames(IntPtr nativeProviderHandle, out List<string> semanticChannelNames);

        public bool TryGetLatestFrameId(IntPtr nativeProviderHandle, out uint frameId);

        public bool HasMetadata(IntPtr nativeProviderHandle);
    }
}
