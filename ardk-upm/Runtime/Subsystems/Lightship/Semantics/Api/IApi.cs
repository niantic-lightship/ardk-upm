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

        public void Configure(IntPtr nativeProviderHandle, UInt32 framesPerSecond, UInt32 numThresholds, IntPtr thresholds);

        public void Destruct(IntPtr nativeProviderHandle);

        public bool TryGetSemanticChannel(IntPtr nativeProviderHandle, string channelName, out XRTextureDescriptor semanticsChannelDescriptor);

        public bool TryAcquireSemanticChannelCPUImage(IntPtr nativeProviderHandle, string channelName, out LightshipCpuBuffer cpuBuffer);

        public void DisposeCPUImage(IntPtr nativeProviderHandle,  IntPtr resHandle);

        public bool TryGetPackedSemanticChannels(IntPtr nativeProviderHandle, out XRTextureDescriptor packedSemanticsDescriptor);

        public bool TryAcquirePackedSemanticChannelsCPUImage(IntPtr nativeProviderHandle, out LightshipCpuBuffer cpuBuffer);

        public bool GetChannelNames(IntPtr nativeProviderHandle, out List<string> semanticChannelNames);
    }
}
