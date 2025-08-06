// Copyright 2022-2025 Niantic.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;

namespace Niantic.Lightship.AR.Subsystems.Semantics
{
    internal interface IApi
    {
        public IntPtr Construct(IntPtr unityContext);

        public void Start(IntPtr nativeProviderHandle);

        public void Stop(IntPtr nativeProviderHandle);

        public void Configure(IntPtr nativeProviderHandle, UInt32 framesPerSecond, UInt32 numThresholds, IntPtr thresholds, HashSet<string>suppressionMaskChannelNames);

        public void Destruct(IntPtr nativeProviderHandle);

        public bool TryGetSemanticChannel
        (
            IntPtr nativeProviderHandle,
            string channelName,
            XRCameraParams? cameraParams,
            Matrix4x4? targetPose,
            out XRTextureDescriptor semanticsChannelDescriptor,
            out Matrix4x4 samplerMatrix
        );

        public bool TryAcquireSemanticChannelCpuImage
        (
            IntPtr nativeProviderHandle,
            string channelName,
            XRCameraParams? cameraParams,
            Matrix4x4? targetPose,
            out XRCpuImage cpuImage,
            out Matrix4x4 samplerMatrix
        );

        public bool TryGetPackedSemanticChannels
        (
            IntPtr nativeProviderHandle,
            XRCameraParams? cameraParams,
            Matrix4x4? targetPose,
            out XRTextureDescriptor packedSemanticsDescriptor,
            out Matrix4x4 samplerMatrix
        );

        public bool TryAcquirePackedSemanticChannelsCpuImage
        (
            IntPtr nativeProviderHandle,
            XRCameraParams? cameraParams,
            Matrix4x4? targetPose,
            out XRCpuImage cpuImage,
            out Matrix4x4 samplerMatrix
        );

		public bool TryGetSuppressionMaskTexture
		(
			IntPtr nativeProviderHandle,
			XRCameraParams? cameraParams,
            Matrix4x4? targetPose,
			out XRTextureDescriptor suppressionMaskDescriptor,
			out Matrix4x4 samplerMatrix
		);

		public bool TryAcquireSuppressionMaskCpuImage
		(
			IntPtr nativeProviderHandle,
			XRCameraParams? cameraParams,
            Matrix4x4? targetPose,
			out XRCpuImage cpuImage,
			out Matrix4x4 samplerMatrix
		);

        public bool TryGetChannelNames(IntPtr nativeProviderHandle, out List<string> semanticChannelNames);

        public bool TryGetLatestFrameId(IntPtr nativeProviderHandle, out uint frameId);

        public bool TryGetLatestIntrinsicsMatrix(IntPtr nativeProviderHandle, out Matrix4x4 intrinsicsMatrix);

        public bool HasMetadata(IntPtr nativeProviderHandle);

        public UInt32 GetFlags(IEnumerable<string> channels);
    }
}
