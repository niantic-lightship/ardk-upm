// Copyright 2022-2024 Niantic.

using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Niantic.Lightship.AR.Core;

namespace Niantic.Lightship.AR.Utilities.Preloading
{
    // This feature enumeration corresponds to ARDK_Feature in the native implementation
    internal enum Feature : byte
    {
        [Description("None")] Unspecified = 0,
        [Description("Lightship Depth")] Depth,
        [Description("Lightship Semantic Segmentation")] Semantics,
        [Description("Lightship Scanning Framework")] Scanning,
        [Description("Lightship Object Detection")] ObjectDetection
    }

    internal sealed class NativeModelPreloader: IModelPreloader
    {
        private IntPtr _nativeHandle;

        internal NativeModelPreloader(IntPtr unityContext)
        {
            _nativeHandle = Native.Create(unityContext);
        }

        ~NativeModelPreloader()
        {
            Dispose();
        }

        public override void Dispose()
        {
            if (_nativeHandle.IsValidHandle())
            {
                Native.Release(_nativeHandle);
                _nativeHandle = IntPtr.Zero;
            }
        }

        public override PreloaderStatusCode DownloadModel(DepthMode depthMode)
        {
            return DownloadModel(Feature.Depth, (byte) depthMode);
        }

        public override PreloaderStatusCode DownloadModel(SemanticsMode semanticsMode)
        {
            return DownloadModel(Feature.Semantics, (byte) semanticsMode);
        }

        public override PreloaderStatusCode DownloadModel(ObjectDetectionMode objectDetectionMode)
        {
            return DownloadModel(Feature.ObjectDetection, (byte)objectDetectionMode);
        }

        private PreloaderStatusCode DownloadModel(Feature feature, byte mode)
        {
            if (!_nativeHandle.IsValidHandle())
            {
                return PreloaderStatusCode.Failure;
            }

            return Native.DownloadModel(_nativeHandle, (byte) feature, mode);
        }

        public override PreloaderStatusCode RegisterModel(DepthMode depthMode, string filepath)
        {
            return RegisterModel(Feature.Depth, (byte) depthMode, filepath);
        }

        public override PreloaderStatusCode RegisterModel(SemanticsMode semanticsMode, string filepath)
        {
            return RegisterModel(Feature.Semantics, (byte) semanticsMode, filepath);
        }

        public override PreloaderStatusCode RegisterModel(ObjectDetectionMode objectDetectionMode, string filepath)
        {
            return RegisterModel(Feature.ObjectDetection, (byte)objectDetectionMode, filepath);
        }

        private PreloaderStatusCode RegisterModel(Feature feature, byte mode, string filepath)
        {
            if (!_nativeHandle.IsValidHandle())
            {
                return PreloaderStatusCode.Failure;
            }

            return Native.RegisterModel(_nativeHandle, (byte) feature, mode, filepath);
        }

        public override PreloaderStatusCode CurrentProgress(DepthMode depthMode, out float progress)
        {
            return CurrentProgress(Feature.Depth, (byte) depthMode, out progress);
        }

        public override PreloaderStatusCode CurrentProgress(SemanticsMode semanticsMode, out float progress)
        {
            return CurrentProgress(Feature.Semantics, (byte) semanticsMode, out progress);
        }

        public override PreloaderStatusCode CurrentProgress(ObjectDetectionMode objectDetectionMode, out float progress)
        {
            return CurrentProgress(Feature.ObjectDetection, (byte)objectDetectionMode, out progress);
        }

        private PreloaderStatusCode CurrentProgress(Feature feature, byte mode, out float progress)
        {
            if (!_nativeHandle.IsValidHandle())
            {
                progress = 0;
                return PreloaderStatusCode.Failure;
            }

            return Native.CurrentProgress(_nativeHandle, (byte) feature, mode, out progress);
        }

        public override bool ExistsInCache(DepthMode depthMode)
        {
            return ExistsInCache(Feature.Depth, (byte) depthMode);
        }

        public override bool ExistsInCache(SemanticsMode semanticsMode)
        {
            return ExistsInCache(Feature.Semantics, (byte) semanticsMode);
        }

        public override bool ExistsInCache(ObjectDetectionMode objectDetectionMode)
        {
            return ExistsInCache(Feature.ObjectDetection, (byte)objectDetectionMode);
        }

        private bool ExistsInCache(Feature feature, byte mode)
        {
            if (!_nativeHandle.IsValidHandle())
            {
                return false;
            }

            return Native.ExistsInCache(_nativeHandle, (byte) feature, mode);
        }

        public override bool ClearFromCache(DepthMode depthMode)
        {
            return ClearFromCache(Feature.Depth, (byte) depthMode);
        }

        public override bool ClearFromCache(SemanticsMode semanticsMode)
        {
            return ClearFromCache(Feature.Semantics, (byte) semanticsMode);
        }

        public override bool ClearFromCache(ObjectDetectionMode objectDetectionMode)
        {
            return ClearFromCache(Feature.ObjectDetection, (byte)objectDetectionMode);
        }

        private bool ClearFromCache(Feature feature, byte mode)
        {
            if (!_nativeHandle.IsValidHandle())
            {
                return false;
            }

            return Native.ClearFromCache(_nativeHandle, (byte) feature, mode);
        }

        private static class Native
        {
            [DllImport(LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_Preloader_Create")]
            public static extern IntPtr Create(IntPtr unityContext);

            [DllImport(LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_Preloader_Release")]
            public static extern void Release(IntPtr preloaderHandle);

            [DllImport(LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_Preloader_DownloadModel")]
            public static extern PreloaderStatusCode DownloadModel(IntPtr preloaderHandle, byte feature, byte mode);

            [DllImport(LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_Preloader_RegisterModel")]
            public static extern PreloaderStatusCode RegisterModel(IntPtr preloaderHandle, byte feature, byte mode, string filepath);

            [DllImport(LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_Preloader_CurrentProgress")]
            public static extern PreloaderStatusCode CurrentProgress(IntPtr preloaderHandle, byte feature, byte mode, out float progress);

            [DllImport(LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_Preloader_ExistsInCache")]
            public static extern bool ExistsInCache(IntPtr preloaderHandle, byte feature, byte mode);

            [DllImport(LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_Preloader_ClearFromCache")]
            public static extern bool ClearFromCache(IntPtr preloaderHandle, byte feature, byte mode);
        }
    }

}
