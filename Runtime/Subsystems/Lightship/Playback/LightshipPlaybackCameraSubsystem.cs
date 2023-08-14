using System;
using System.Collections.Generic;
using System.Linq;
using Niantic.Lightship.AR.Utilities;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Scripting;
using UnityEngine.XR.ARSubsystems;
using Object = UnityEngine.Object;

namespace Niantic.Lightship.AR.Playback
{
    [Preserve]
    public class LightshipPlaybackCameraSubsystem : XRCameraSubsystem, IPlaybackDatasetUser
    {
        private const string k_BeforeOpaquesBackgroundShaderName = "Unlit/LightshipPlaybackBackground";
        private const string k_LightshipImageConversionShaderName = "Unlit/LightshipImageConversion";

        public static readonly string[] backgroundShaderNames = new[] { k_BeforeOpaquesBackgroundShaderName, k_LightshipImageConversionShaderName };

        /// <summary>
        /// The list of shader keywords to avoid during compilation.
        /// </summary>
        /// <value>
        /// The list of shader keywords to avoid during compilation.
        /// </value>
        private static readonly List<string> k_BackgroundShaderKeywordsToNotCompile = new() { };

        /// <summary>
        /// The list of shader keywords to avoid during compilation.
        /// </summary>
        /// <value>
        /// The list of shader keywords to avoid during compilation.
        /// </value>
        internal static List<string> backgroundShaderKeywordsToNotCompile => k_BackgroundShaderKeywordsToNotCompile;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Register()
        {
            Debug.Log("LightshipPlaybackCameraSubsystem.Register");
            const string id = "Lightship-Playback-Camera";
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

            Register(info);
        }

        void IPlaybackDatasetUser.SetPlaybackDatasetReader(PlaybackDatasetReader reader)
        {
            ((IPlaybackDatasetUser)provider).SetPlaybackDatasetReader(reader);
        }

        private class LightshipPlaybackProvider : Provider, IPlaybackDatasetUser
        {
            // TODO: Support URP
            private List<string> m_LegacyRPEnabledMaterialKeywords = new List<string>();
            private List<string> m_LegacyRPDisabledMaterialKeywords = new List<string>();

            // The display matrix coming from AR Foundation varies per platform. We need to know what is the running
            // platform to apply the matrix properly in the shader.
            private const string k_AndroidPlatformKeyword = "ANDROID_PLATFORM";

            private static readonly int k_TexturePropertyId = Shader.PropertyToID("_CameraTex");
            private Material m_CameraMaterial;

            private PlaybackDatasetReader m_DatasetReader;

            // This value will strongly affect memory usage.  It can also be set by the user in configuration.
            // The value represents the number of frames in memory before the user must make a copy of the data
            private const int k_FramesInMemoryCount = 2;
            private BufferedTextureCache m_CameraImageTextures;

            public override XRCpuImage.Api cpuImageApi => LightshipCpuImageApi.instance;

            public override bool permissionGranted => true;
            public override Feature currentCamera => Feature.WorldFacingCamera;

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
                    if (m_CameraMaterial == null)
                    {
                        m_CameraMaterial = CreateCameraMaterial(k_BeforeOpaquesBackgroundShaderName);
                    }

                    return m_CameraMaterial;
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
                get { return m_DatasetReader?.GetAutofocusEnabled() ?? false; }
            }

            public override bool autoFocusRequested
            {
                get => autoFocusEnabled;
                set { }
            }

            public LightshipPlaybackProvider()
            {
#if UNITY_ANDROID
                m_LegacyRPEnabledMaterialKeywords.Add(k_AndroidPlatformKeyword);
#else
                m_LegacyRPDisabledMaterialKeywords.Add(k_AndroidPlatformKeyword);
#endif
            }

            public override void Destroy()
            {
                m_DatasetReader = null;
                m_CameraImageTextures.Dispose();
            }

            public void SetPlaybackDatasetReader(PlaybackDatasetReader reader)
            {
                m_DatasetReader = reader;

                var imageRes = m_DatasetReader.GetImageResolution();
                m_CameraImageTextures =
                    new BufferedTextureCache
                    (
                        k_FramesInMemoryCount,
                        imageRes.x,
                        imageRes.y,
                        TextureFormat.RGB24, // format that JPG images are loaded as
                        false
                    );
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
                        m_DatasetReader.GetImageResolution(),
                        m_DatasetReader.GetFramerate(),
                        m_DatasetReader.GetIsLidarAvailable() ? Supported.Supported : Supported.Unsupported
                    );

                var na = new NativeArray<XRCameraConfiguration>(1, allocator);
                na[0] = config;
                return na;
            }

            public override bool TryGetFrame(XRCameraParams cameraParams, out XRCameraFrame cameraFrame)
            {
                cameraParams.screenOrientation = GameViewUtils.GetGameViewAspectRatio(cameraParams);
                var frame = m_DatasetReader.CurrFrame;
                if (frame == null)
                {
                    cameraFrame = default;
                    return false;
                }

                const XRCameraFrameProperties props =
                    XRCameraFrameProperties.Timestamp |
                    XRCameraFrameProperties.DisplayMatrix |
                    XRCameraFrameProperties.ProjectionMatrix;

                // Device orientation is always LandscapeLeft because we only care about the image sensor, and that is
                // built in LandscapeLeft
                var resolution = m_DatasetReader.GetImageResolution();

                var displayMatrix =
                    CameraMath.CalculateDisplayMatrix
                    (
                        resolution.x,
                        resolution.y,
                        (int)cameraParams.screenWidth,
                        (int)cameraParams.screenHeight,
                        frame.Orientation,
#if UNITY_ANDROID
                        invertVertically: false,
                        layout: CameraMath.MatrixLayout.ColumnMajor,
                        reverseRotation: true
#else
                        invertVertically: true,
                        layout:CameraMath.MatrixLayout.RowMajor
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
                return base.TryAcquireLatestCpuImage(out cinfo);
            }

            public override bool TryGetIntrinsics(out XRCameraIntrinsics cameraIntrinsics)
            {
                var frame = m_DatasetReader.CurrFrame;
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
                var frame = m_DatasetReader.CurrFrame;
                if (frame == null)
                {
                    return base.GetTextureDescriptors(defaultDescriptor, allocator);
                }

                var arr = new NativeArray<XRTextureDescriptor>(1, allocator);

                m_CameraImageTextures.GetUpdatedTextureFromEncodedBuffer
                (
                    m_DatasetReader.GetCurrentImageData(),
                    (uint)frame.Sequence,
                    true,
                    3,
                    out IntPtr nativeTexturePtr
                );

                var res = m_DatasetReader.GetImageResolution();
                arr[0] =
                    new XRTextureDescriptor
                    (
                        nativeTexturePtr,
                        res.x,
                        res.y,
                        0,
                        TextureFormat.RGB24,
                        k_TexturePropertyId,
                        0,
                        TextureDimension.Tex2D
                    );

                return arr;
            }

            public override void GetMaterialKeywords(out List<string> enabledKeywords,
                out List<string> disabledKeywords)
            {
                enabledKeywords = m_LegacyRPEnabledMaterialKeywords;
                disabledKeywords = m_LegacyRPDisabledMaterialKeywords;
            }
        }
    }
}
