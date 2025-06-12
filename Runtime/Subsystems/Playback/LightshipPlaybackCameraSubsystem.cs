// Copyright 2022-2025 Niantic.
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Niantic.Lightship.AR.Common;
using Niantic.Lightship.AR.PAM;
using Niantic.Lightship.AR.Utilities.Logging;
using Niantic.Lightship.AR.Subsystems.Common;
using Niantic.Lightship.AR.Utilities;
using Niantic.Lightship.AR.Utilities.Textures;
using Niantic.Lightship.Utilities.UnityAssets;
using Niantic.Platform.Analytics.Telemetry;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.XR.CoreUtils.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Scripting;
using UnityEngine.XR.ARSubsystems;

namespace Niantic.Lightship.AR.Subsystems.Playback
{
    [Preserve]
    public class LightshipPlaybackCameraSubsystem : XRCameraSubsystem, IPlaybackDatasetUser
    {
        private const string BeforeOpaquesBackgroundShaderName = "Unlit/LightshipPlaybackBackground";
        private const string LightshipImageConversionShaderName = "Unlit/LightshipImageConversion";

        public static readonly string[] BackgroundShaderNames =
            new[] { BeforeOpaquesBackgroundShaderName, LightshipImageConversionShaderName };

        /// <summary>
        /// The list of shader keywords to avoid during compilation.
        /// </summary>
        /// <value>
        /// The list of shader keywords to avoid during compilation.
        /// </value>
        internal static List<string> backgroundShaderKeywordsToNotCompile { get; } = new() { };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Register()
        {
            Log.Info("LightshipPlaybackCameraSubsystem.Register");
            const string id = "Lightship-Playback-Camera";

#if UNITY_6000_0_OR_NEWER
            var info = new XRCameraSubsystemDescriptor.Cinfo
            {
                id = id,
                providerType = typeof(LightshipPlaybackProvider),
                subsystemTypeOverride = typeof(LightshipPlaybackCameraSubsystem),
                supportsAverageBrightness = false,
                supportsAverageColorTemperature = false,
                supportsColorCorrection = false,
                supportsDisplayMatrix = true,
                supportsProjectionMatrix = true,
                supportsTimestamp = true,
                supportsCameraConfigurations = false,
                supportsCameraImage = true,
                supportsAverageIntensityInLumens = false,
                supportsFocusModes = true,
                supportsCameraGrain = false,
                supportsFaceTrackingAmbientIntensityLightEstimation = false,
                supportsFaceTrackingHDRLightEstimation = false,
                supportsWorldTrackingAmbientIntensityLightEstimation = false,
                supportsWorldTrackingHDRLightEstimation = false
            };

             XRCameraSubsystemDescriptor.Register(info);

#else
            var info = new XRCameraSubsystemCinfo
            {
                id = id,
                providerType = typeof(LightshipPlaybackProvider),
                subsystemTypeOverride = typeof(LightshipPlaybackCameraSubsystem),
                supportsAverageBrightness = false,
                supportsAverageColorTemperature = false,
                supportsColorCorrection = false,
                supportsDisplayMatrix = true,
                supportsProjectionMatrix = true,
                supportsTimestamp = true,
                supportsCameraConfigurations = false,
                supportsCameraImage = true,
                supportsAverageIntensityInLumens = false,
                supportsFocusModes = true,
                supportsCameraGrain = false,
                supportsFaceTrackingAmbientIntensityLightEstimation = false,
                supportsFaceTrackingHDRLightEstimation = false,
                supportsWorldTrackingAmbientIntensityLightEstimation = false,
                supportsWorldTrackingHDRLightEstimation = false
            };

             XRCameraSubsystem.Register(info);
#endif
        }

        void IPlaybackDatasetUser.SetPlaybackDatasetReader(PlaybackDatasetReader reader)
        {
            ((IPlaybackDatasetUser)provider).SetPlaybackDatasetReader(reader);
        }

        private class LightshipPlaybackProvider : Provider, IPlaybackDatasetUser
        {
            // TODO: Support URP
            private readonly List<string> _legacyRPEnabledMaterialKeywords = new List<string>();
            private readonly List<string> _legacyRPDisabledMaterialKeywords = new List<string>();

            // The display matrix coming from AR Foundation varies per platform. We need to know what is the running
            // platform to apply the matrix properly in the shader.
            private const string AndroidPlatformKeyword = "ANDROID_PLATFORM";

            private static readonly int s_texturePropertyId = Shader.PropertyToID("_CameraTex");
            private Material _cameraMaterial;

            private PlaybackDatasetReader _datasetReader;

            public override XRCpuImage.Api cpuImageApi => LightshipCpuImageApi.Instance;

            public override bool permissionGranted => true;
            public override Feature currentCamera => Feature.WorldFacingCamera;

            private (int Id, Texture2D Frame) _currentFrame;
            private int _textureWidth;
            private int _textureHeight;
            private TextureFormat _textureFormat;
            private readonly Texture2D _encodedTexHolder = new Texture2D(2, 2);

            public override Feature requestedCamera
            {
                get => currentCamera;
                set { }
            }

            public override Material cameraMaterial
            {
                get
                {
                    // TODO: Different camera material based on background rendering mode
                    if (_cameraMaterial == null)
                    {
                        _cameraMaterial = CreateCameraMaterial(BeforeOpaquesBackgroundShaderName);
                    }

                    return _cameraMaterial;
                }
            }

            public override XRSupportedCameraBackgroundRenderingMode requestedBackgroundRenderingMode { get; set; }

            public override XRCameraBackgroundRenderingMode currentBackgroundRenderingMode
            {
                get
                {
                    switch (requestedBackgroundRenderingMode)
                    {
                        case XRSupportedCameraBackgroundRenderingMode.AfterOpaques:
                            return XRCameraBackgroundRenderingMode.AfterOpaques;
                        case XRSupportedCameraBackgroundRenderingMode.BeforeOpaques:
                        case XRSupportedCameraBackgroundRenderingMode.Any:
                            return XRCameraBackgroundRenderingMode.BeforeOpaques;
                        default:
                            return XRCameraBackgroundRenderingMode.None;
                    }
                }
            }

            public override XRSupportedCameraBackgroundRenderingMode supportedBackgroundRenderingMode =>
                XRSupportedCameraBackgroundRenderingMode.Any;

            public override bool autoFocusEnabled
            {
                get { return _datasetReader?.GetAutofocusEnabled() ?? false; }
            }

            public override bool autoFocusRequested
            {
                get => autoFocusEnabled;
                set { }
            }

            public LightshipPlaybackProvider()
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                _legacyRPEnabledMaterialKeywords.Add(AndroidPlatformKeyword);
#else
                _legacyRPDisabledMaterialKeywords.Add(AndroidPlatformKeyword);
#endif
            }

            public override void Destroy()
            {
                _datasetReader = null;
            }

            public void SetPlaybackDatasetReader(PlaybackDatasetReader reader)
            {
                _datasetReader = reader;

                var imageRes = _datasetReader.GetImageResolution();
                _textureWidth = imageRes.x;
                _textureHeight = imageRes.y;
                _textureFormat = TextureFormat.RGB24;

                // If the resolution is 0 (not encoded in dataset), we need to create a texture with at least 1x1 size
                //   to avoid a Unity error.
                var tempWidth = _textureWidth == 0 ? 1 : _textureWidth;
                var tempHeight = _textureHeight == 0 ? 1 : _textureHeight;
                _currentFrame = new ValueTuple<int, Texture2D>
                    (-99, new Texture2D(tempWidth, tempHeight, _textureFormat, false));
            }

            public override NativeArray<XRCameraConfiguration> GetConfigurations
            (
                XRCameraConfiguration defaultCameraConfiguration,
                Allocator allocator
            )
            {
                var config =
                    new XRCameraConfiguration
                    (
                        IntPtr.Zero,
                        _datasetReader.GetImageResolution(),
                        _datasetReader.GetFramerate(),
                        _datasetReader.GetIsLidarAvailable() ? Supported.Supported : Supported.Unsupported
                    );

                var na = new NativeArray<XRCameraConfiguration>(1, allocator);
                na[0] = config;
                return na;
            }

            public override bool TryGetFrame(XRCameraParams cameraParams, out XRCameraFrame cameraFrame)
            {
                var frame = _datasetReader.CurrFrame;
                if (frame == null)
                {
                    cameraFrame = default;
                    return false;
                }

                // Playback footage is only permitted to play in the orientation it was recorded in
                cameraParams.screenOrientation = frame.Orientation;

                const XRCameraFrameProperties props =
                    XRCameraFrameProperties.Timestamp |
                    XRCameraFrameProperties.DisplayMatrix |
                    XRCameraFrameProperties.ProjectionMatrix;

                // Device orientation is always LandscapeLeft because we only care about the image sensor, and that is
                // built in LandscapeLeft
                var resolution = _datasetReader.GetImageResolution();

                var displayMatrix =
                    CameraMath.CalculateDisplayMatrix
                    (
                        resolution.x,
                        resolution.y,
                        (int)cameraParams.screenWidth,
                        (int)cameraParams.screenHeight,
                        frame.Orientation,
#if UNITY_ANDROID && !UNITY_EDITOR
                        invertVertically: false,
                        layout: CameraMath.MatrixLayout.ColumnMajor,
                        reverseRotation: true
#else
                        invertVertically: true,
                        layout: CameraMath.MatrixLayout.RowMajor
#endif
                    );

                var projectionMatrix = CameraMath.CalculateProjectionMatrix(frame.Intrinsics, cameraParams);

                cameraFrame = new XRCameraFrame
                (
                    (long)(frame.TimestampInSeconds * 1000000000), // seconds * 1e+9
                    0,
                    0,
                    default,
                    projectionMatrix,
                    displayMatrix,
                    frame.TrackingState,
                    IntPtr.Zero,
                    props,
                    0,
                    0,
                    0,
                    0,
                    default,
                    Vector3.forward,
                    default,
                    default,
                    0
                );

                return true;
            }

            public override bool TryAcquireLatestCpuImage(out XRCpuImage.Cinfo cinfo)
            {
                if (_datasetReader == null)
                {
                    cinfo = default;
                    return false;
                }

                var frame = _datasetReader.CurrFrame;
                if (frame == null)
                {
                    cinfo = default;
                    return false;
                }

                UpdateCachedCurrentFrameInfo(frame);
                var rgbData = _currentFrame.Frame.GetRawTextureData<byte>();
                var lightshipCpuImageApi = (LightshipCpuImageApi)cpuImageApi;

#if NIANTIC_LIGHTSHIP_YUV_PLAYBACK
                // Convert RGB to YUV
                if (!ImageConversionUtils.Convert(rgbData, _currentFrame.Frame.width, _currentFrame.Frame.height,
                        ImageFormatCEnum.RGB24, ImageFormatCEnum.Yuv420_888, out var yuvData))
                {
                    cinfo = default;
                    return false;
                }

                // Calculate the sizes of the YUV planes
                int width = _currentFrame.Frame.width;
                int height = _currentFrame.Frame.height;
                int ySize = width * height;
                int uvWidth = (width + 1) / 2;
                int uvHeight = (height + 1) / 2;
                int uvSize = uvWidth * uvHeight;
                var sizes = new[] {ySize, uvSize, uvSize};

                // Get the pointers to the image planes
                IntPtr[] planes;
                unsafe
                {
                    // Get the pointer to the flat YUV image buffer
                    var yuvDataPtr = (IntPtr)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(yuvData);
                    planes = new[] {yuvDataPtr, yuvDataPtr + ySize, yuvDataPtr + ySize + uvSize};
                }

                try
                {
                    // Copy the YUV data to the LightshipCpuImageApi repository
                    return lightshipCpuImageApi.TryAddManagedXRCpuImage(planes, sizes, _currentFrame.Frame.width,
                        _currentFrame.Frame.height, XRCpuImage.Format.AndroidYuv420_888,
                        (ulong)(frame.TimestampInSeconds * 1000.0), out cinfo);
                }
                finally
                {
                    // Dispose of the source data
                    yuvData.Dispose();
                }

#endif
                IntPtr rgbDataPtr;
                unsafe
                {
                    rgbDataPtr = (IntPtr)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(rgbData);
                }

                return lightshipCpuImageApi.TryAddManagedXRCpuImage
                (
                    rgbDataPtr,
                    _currentFrame.Frame.width * _currentFrame.Frame.height * _currentFrame.Frame.format.BytesPerPixel(),
                    _currentFrame.Frame.width,
                    _currentFrame.Frame.height,
                    _currentFrame.Frame.format,
                    (ulong)(frame.TimestampInSeconds * 1000.0),
                    out cinfo
                );
            }

            public override bool TryGetIntrinsics(out XRCameraIntrinsics cameraIntrinsics)
            {
                var frame = _datasetReader.CurrFrame;
                if (frame == null)
                {
                    return base.TryGetIntrinsics(out cameraIntrinsics);
                }

                cameraIntrinsics = frame.Intrinsics;
                return true;
            }


            public override NativeArray<XRTextureDescriptor> GetTextureDescriptors
            (
                XRTextureDescriptor defaultDescriptor,
                Allocator allocator
            )
            {
                var frame = _datasetReader.CurrFrame;
                if (frame == null)
                {
                    return base.GetTextureDescriptors(defaultDescriptor, allocator);
                }

                var arr = new NativeArray<XRTextureDescriptor>(1, allocator);

                UpdateCachedCurrentFrameInfo(frame);

                var res = _datasetReader.GetImageResolution();
                arr[0] =
                    new XRTextureDescriptor
                    (
                        _currentFrame.Frame.GetNativeTexturePtr(),
                        res.x,
                        res.y,
                        0,
                        TextureFormat.RGB24,
                        s_texturePropertyId,
                        0,
                        TextureDimension.Tex2D
                    );

                return arr;
            }

            public override void GetMaterialKeywords(out List<string> enabledKeywords,
                out List<string> disabledKeywords)
            {
                enabledKeywords = _legacyRPEnabledMaterialKeywords;
                disabledKeywords = _legacyRPDisabledMaterialKeywords;
            }

#if UNITY_6000_0_OR_NEWER
            public override ShaderKeywords GetShaderKeywords() => new(
                enabledKeywords: _legacyRPEnabledMaterialKeywords.AsReadOnly(),
                disabledKeywords: _legacyRPDisabledMaterialKeywords.AsReadOnly()
            );
#endif

            private void UpdateCachedCurrentFrameInfo(PlaybackDataset.FrameMetadata frame)
            {
                if (_currentFrame.Id != frame.Sequence)
                {
                    Texture2D tex = _currentFrame.Frame;
                    var wasReinitialisationSuccessful = tex.Reinitialize(_textureWidth, _textureHeight,
                        _textureFormat, false);

                    if (!wasReinitialisationSuccessful)
                    {
                        tex = new Texture2D(_textureWidth, _textureHeight, _textureFormat, false);
                    }

                    var path = Path.Combine(_datasetReader.GetDatasetPath(), frame.ImagePath);
                    byte[] buffer = FileUtilities.GetAllBytes(path);
                    _encodedTexHolder.LoadImage(buffer);
                    tex.LoadMirroredOverXAxis(_encodedTexHolder, _textureFormat.BytesPerPixel());
                    tex.Apply();

                    _currentFrame = (frame.Sequence, tex);
                }
            }
        }
    }
}
