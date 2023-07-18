using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Niantic.Lightship.AR.ARFoundation;
using Niantic.Lightship.AR.Loader;
using Niantic.Lightship.AR.Utilities;
using Unity.Collections;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Scripting;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.Management;

namespace Niantic.Lightship.AR
{
    /// <summary>
    /// This subsystem provides implementing functionality for the <c>XROcclusionSubsystem</c> class.s
    /// </summary>
    [Preserve]
    class LightshipOcclusionSubsystem : XROcclusionSubsystem, _ILightshipSettingsUser
    {
        /// <summary>
        /// Register the Lightship occlusion subsystem.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Register()
        {
            Debug.Log("LightshipOcclusionSubsystem.Register");
            const string id = "Lightship-Occlusion";
            var xrOcclusionSubsystemCinfo = new XROcclusionSubsystemCinfo()
            {
                id = id,
                providerType = typeof(LightshipOcclusionProvider),
                subsystemTypeOverride = typeof(LightshipOcclusionSubsystem),
                humanSegmentationStencilImageSupportedDelegate = () => Supported.Unsupported,
                humanSegmentationDepthImageSupportedDelegate = () => Supported.Unsupported,
                environmentDepthImageSupportedDelegate = () => Supported.Supported,
                environmentDepthConfidenceImageSupportedDelegate = () => Supported.Unsupported,
                environmentDepthTemporalSmoothingSupportedDelegate = () => Supported.Unsupported
            };

            XROcclusionSubsystem.Register(xrOcclusionSubsystemCinfo);
        }

        void _ILightshipSettingsUser.SetLightshipSettings(LightshipSettings settings)
        {
            ((_ILightshipSettingsUser)provider).SetLightshipSettings(settings);
        }

        /// <summary>
        /// The implementation provider class.
        /// </summary>
        class LightshipOcclusionProvider : Provider, _ILightshipSettingsUser
        {
            /// <summary>
            /// The shader property name for the environment depth texture.
            /// </summary>
            /// <value>
            /// The shader property name for the environment depth texture.
            /// </value>
            private const string k_TextureEnvironmentDepthPropertyName = "_EnvironmentDepth";

            /// <summary>
            /// The shader property name identifier for the environment depth texture.
            /// </summary>
            /// <value>
            /// The shader property name identifier for the environment depth texture.
            /// </value>
            private static readonly int s_TextureEnvironmentDepthPropertyId =
                Shader.PropertyToID(k_TextureEnvironmentDepthPropertyName);

            /// <summary>
            /// The shader keyword for enabling environment depth rendering for ARKit Background shader.
            /// </summary>
            /// <value>
            /// The shader keyword for enabling environment depth rendering.
            /// </value>
            private const string k_EnvironmentDepthEnabledARKitMaterialKeyword = "ARKIT_ENVIRONMENT_DEPTH_ENABLED";

            /// <summary>
            /// The shader keyword for enabling environment depth rendering for ARCore Background shader.
            /// </summary>
            /// <value>
            /// The shader keyword for enabling environment depth rendering.
            /// </value>
            private const string k_EnvironmentDepthEnabledARCoreMaterialKeyword = "ARCORE_ENVIRONMENT_DEPTH_ENABLED";

            /// <summary>
            /// The shader keyword for enabling environment depth rendering for Lightship Playback Background shader.
            /// </summary>
            /// <value>
            /// The shader keyword for enabling environment depth rendering.
            /// </value>
            private const string k_EnvironmentDepthEnabledLightshipMaterialKeyword =
                "LIGHTSHIP_ENVIRONMENT_DEPTH_ENABLED";

            /// <summary>
            /// The url of the models used in the inference for depth based on its quality/performance metrics
            /// </summary>
            private const string k_ARDKModelURLFast = "https://armodels.eng.nianticlabs.com/niantic_ca_v1.2_fast.bin";

            private const string k_ARDKModelURLDefault = "https://armodels.eng.nianticlabs.com/niantic_ca_v1.2.bin";

            private const string k_ARDKModelURLSmooth =
                "https://armodels.eng.nianticlabs.com/niantic_ca_v1.2_antiflicker.bin";

            /// <summary>
            /// The shader keywords for enabling environment depth rendering.
            /// </summary>
            /// <value>
            /// The shader keywords for enabling environment depth rendering.
            /// </value>
            private static readonly List<string> s_EnvironmentDepthEnabledMaterialKeywords =
                new()
                {
                    k_EnvironmentDepthEnabledARKitMaterialKeyword,
                    k_EnvironmentDepthEnabledARCoreMaterialKeyword,
                    k_EnvironmentDepthEnabledLightshipMaterialKeyword
                };

            /// <summary>
            /// The occlusion preference mode for when rendering the background.
            /// </summary>
            private OcclusionPreferenceMode m_OcclusionPreferenceMode;

            /// <summary>
            /// The handle to the native version of the provider
            /// </summary>
            private IntPtr m_nativeProviderHandle;

            // This value will strongly affect memory usage.  It can also be set by the user in configuration.
            // The value represents the number of frames in memory before the user must make a copy of the data
            private const int k_FramesInMemoryCount = 2;
            private BufferedTextureCache m_EnvironmentDepthTextures;

            // The index of the frame where the depth buffer was last updated
            private int m_FrameIndexOfLastUpdate = 0;

            private EnvironmentDepthMode m_requestedEnvironmentDepthMode;

            private LightshipSettings m_lightshipSettings;

            /// <summary>
            /// Construct the implementation provider.
            /// </summary>
            public LightshipOcclusionProvider()
            {
                Debug.Log("LightshipOcclusionSubsystem.LightshipOcclusionProvider construct");

#if NIANTIC_LIGHTSHIP_AR_LOADER_ENABLED
                m_nativeProviderHandle = NativeApi.Construct(LightshipUnityContext.UnityContextHandle);
#endif
                Debug.Log("LightshipOcclusionSubsystem got nativeProviderHandle: " + m_nativeProviderHandle);

                // Default depth mode
                m_requestedEnvironmentDepthMode = EnvironmentDepthMode.Fastest;

                m_EnvironmentDepthTextures = new BufferedTextureCache(k_FramesInMemoryCount);

                // Reset settings possibly inherited from a previous session
                OcclusionContext.ResetOccludee();
            }

            void _ILightshipSettingsUser.SetLightshipSettings(LightshipSettings settings)
            {
                m_lightshipSettings = settings;
            }

            /// <summary>
            /// Start the provider.
            /// </summary>
            public override void Start()
            {
                Debug.Log("LightshipOcclusionSubsystem.Start");
                ConfigureProvider();
                NativeApi.Start(m_nativeProviderHandle);
            }

            private string ModelFromMode(EnvironmentDepthMode mode)
            {
                switch (mode)
                {
                    case EnvironmentDepthMode.Best:
                        return k_ARDKModelURLSmooth;
                    case EnvironmentDepthMode.Fastest:
                        return k_ARDKModelURLFast;
                    case EnvironmentDepthMode.Medium:
                        return k_ARDKModelURLDefault;
                }

                return "";
            }

            private void ConfigureProvider()
            {
                NativeApi.Configure(m_nativeProviderHandle, ModelFromMode(requestedEnvironmentDepthMode),
                    m_lightshipSettings.LightshipDepthFrameRate);
            }

            /// <summary>
            /// Stop the provider.
            /// </summary>
            public override void Stop()
            {
                Debug.Log("LightshipOcclusionSubsystem.Stop");
                NativeApi.Stop(m_nativeProviderHandle);
            }

            /// <summary>
            /// Destroy the provider.
            /// </summary>
            public override void Destroy()
            {
                m_EnvironmentDepthTextures.Dispose();
                NativeApi.Destruct(m_nativeProviderHandle);
            }

            /// <summary>
            /// Property to get or set the requested environment depth mode.
            /// </summary>
            /// <value>
            /// The requested environment depth mode.
            /// </value>
            public override EnvironmentDepthMode requestedEnvironmentDepthMode
            {
                get => m_requestedEnvironmentDepthMode;
                set
                {
                    if (m_requestedEnvironmentDepthMode != value)
                    {
                        m_requestedEnvironmentDepthMode = value;
                        ConfigureProvider();
                    }
                }
            }

            /// <summary>
            /// Property to get the current environment depth mode.
            /// </summary>
            public override EnvironmentDepthMode currentEnvironmentDepthMode
                => m_requestedEnvironmentDepthMode;

            /// <summary>
            /// Specifies the requested occlusion preference mode.
            /// </summary>
            /// <value>
            /// The requested occlusion preference mode.
            /// </value>
            public override OcclusionPreferenceMode requestedOcclusionPreferenceMode
            {
                get => m_OcclusionPreferenceMode;
                set => m_OcclusionPreferenceMode = value;
            }

            /// <summary>
            /// Get the occlusion preference mode currently in use by the provider.
            /// </summary>
            public override OcclusionPreferenceMode currentOcclusionPreferenceMode => m_OcclusionPreferenceMode;

            /// <summary>
            /// Get the environment texture descriptor.
            /// </summary>
            /// <param name="xrTextureDescriptor">The environment depth texture descriptor to be populated, if
            /// available.</param>
            /// <returns>
            /// <c>true</c> if the environment depth texture descriptor is available and is returned. Otherwise,
            /// <c>false</c>.
            /// </returns>
            public override bool TryGetEnvironmentDepth(out XRTextureDescriptor xrTextureDescriptor)
            {
                xrTextureDescriptor = default;

                // TODO [AR-16080]: Standardize whatever check we're using to prevent crashes due to accessing
                // deleted resources in C++
                if (!running)
                    return false;

                var needsToUpdate = m_FrameIndexOfLastUpdate != Time.frameCount;
                if (needsToUpdate)
                {
                    if (AcquireEnvironmentDepth(
                            requestPostProcessing: true,
                            out IntPtr resourceHandle,
                            out IntPtr memoryBuffer,
                            out int size,
                            out int width,
                            out int height,
                            out TextureFormat format))
                    {
                        m_FrameIndexOfLastUpdate = Time.frameCount;
                        m_EnvironmentDepthTextures.GetUpdatedTextureFromBuffer(memoryBuffer, size, width, height,
                            format, (uint)m_FrameIndexOfLastUpdate, out _);
                        NativeApi.DisposeResource(m_nativeProviderHandle, resourceHandle);
                    }
                }

                var texture = m_EnvironmentDepthTextures.GetActiveTexture();
                if (texture == null)
                    return false;

                var nativeTexture = m_EnvironmentDepthTextures.GetActiveTexturePtr();
                if (nativeTexture == IntPtr.Zero)
                    return false;

                xrTextureDescriptor = new XRTextureDescriptor(
                    nativeTexture, texture.width, texture.height, 0, texture.format,
                    s_TextureEnvironmentDepthPropertyId, 0, TextureDimension.Tex2D);
                return true;
            }

            /// <summary>
            /// Gets the CPU construction information for a environment depth image.
            /// </summary>
            /// <param name="cinfo">The CPU image construction information, on success.</param>
            /// <returns>
            /// <c>true</c> if the environment depth texture is available and its CPU image construction information is
            /// returned. Otherwise, <c>false</c>.
            /// </returns>
            public override bool TryAcquireEnvironmentDepthCpuImage(out XRCpuImage.Cinfo cinfo)
            {
                cinfo = default;

                var needsToUpdate = m_FrameIndexOfLastUpdate != Time.frameCount;
                if (needsToUpdate)
                {
                    if (AcquireEnvironmentDepth(
                            requestPostProcessing: true,
                            out IntPtr resourceHandle,
                            out IntPtr memoryBuffer,
                            out int size,
                            out int width,
                            out int height,
                            out TextureFormat format))
                    {
                        m_FrameIndexOfLastUpdate = Time.frameCount;
                        m_EnvironmentDepthTextures.GetUpdatedTextureFromBuffer(memoryBuffer, size, width, height,
                            format, (uint)m_FrameIndexOfLastUpdate, out _);
                        NativeApi.DisposeResource(m_nativeProviderHandle, resourceHandle);
                    }
                }

                var texture = m_EnvironmentDepthTextures.GetActiveTexture();
                if (texture == null)
                    return false;

                var nativeTexture = m_EnvironmentDepthTextures.GetActiveTexturePtr();
                if (nativeTexture == IntPtr.Zero)
                    return false;

                int nativeHandle = nativeTexture.ToInt32();
                ((LightshipCpuImageApi)environmentDepthCpuImageApi).AddManagedXRCpuImage(nativeHandle, texture);
                Vector2Int dimensions = new Vector2Int(texture.width, texture.height);
                cinfo = new XRCpuImage.Cinfo(nativeHandle, dimensions, 1, 0.0d, texture.format.XRCpuImageFormat());
                return true;
            }

            public override bool TryAcquireRawEnvironmentDepthCpuImage(out XRCpuImage.Cinfo cinfo)
            {
                return TryAcquireEnvironmentDepthCpuImage(out cinfo);
            }

            public override bool TryAcquireSmoothedEnvironmentDepthCpuImage(out XRCpuImage.Cinfo cinfo)
            {
                return TryAcquireEnvironmentDepthCpuImage(out cinfo);
            }

            /// <summary>
            /// Acquires the latest environment depth resource.
            /// </summary>
            /// <param name="requestPostProcessing">Whether to perform post-processing to align the image with
            ///     the current camera pose. Requesting this feature does not guarantee it will actually occur.</param>
            /// <param name="resourceHandle">Handle to the native resource.</param>
            /// <param name="memoryBuffer">Handle to the data buffer.</param>
            /// <param name="size">The size of the data buffer.</param>
            /// <param name="width">The width if the image.</param>
            /// <param name="height">The height of the image.</param>
            /// <param name="format">The texture format that should be used to represent the image.</param>
            /// <returns>Whether the resource has been acquired.</returns>
            private bool AcquireEnvironmentDepth(bool requestPostProcessing, out IntPtr resourceHandle, out IntPtr memoryBuffer,
                out int size, out int width, out int height, out TextureFormat format)
            {
                // Verify the aspect ratio we need to comply with
                var cameraImageAspectRatio = OcclusionContext.Shared.CameraImageAspectRatio;
                var isCameraAspectRatioValid =
                    cameraImageAspectRatio.HasValue && !cameraImageAspectRatio.Value.IsUndefined();

                // Cannot acquire an environment depth image in an appropriate image container
                if (!isCameraAspectRatioValid)
                {
                    resourceHandle = IntPtr.Zero;
                    memoryBuffer = IntPtr.Zero;
                    size = default;
                    width = default;
                    height = default;
                    format = default;
                    return false;
                }

                // Acquire the inference result
                resourceHandle = NativeApi.GetEnvironmentDepth(m_nativeProviderHandle, out memoryBuffer,
                    out size, out width, out height, out format, out _);
                if (resourceHandle == IntPtr.Zero)
                    return false;

                // Calculate the aligned image container
                height = Mathf.FloorToInt(width / cameraImageAspectRatio.Value);

                // Acquire the most recent device pose
                var didAcquirePose = PoseProvider.TryAcquireCurrentPose(out var poseMatrix);

                // Successfully acquired the native resource, but no post-processing
                // was requested or we have insufficient information to perform it
                if (!requestPostProcessing || !didAcquirePose)
                {
                    // Blit the original image to a container that matches the camera image aspect ratio
                    var newResource = NativeApi.Blit(m_nativeProviderHandle, resourceHandle, width, height, out memoryBuffer, out size);

                    // Release the original buffer
                    NativeApi.DisposeResource(m_nativeProviderHandle, resourceHandle);

                    // Deliver the image
                    resourceHandle = newResource;
                    return resourceHandle != IntPtr.Zero;
                }

                // Convert the pose to native ARDK format
                var pose = _Convert.Matrix4x4ToInternalArray(poseMatrix.FromUnityToArdk());

                // Warp the original depth image to align it with the current pose
                var processedResource = NativeApi.Warp(
                    m_nativeProviderHandle,
                    resourceHandle,
                    pose,
                    width,
                    height,
                    OcclusionContext.Shared.OccludeeEyeDepth,
                    out memoryBuffer,
                    out size);

                // Release the original buffer
                NativeApi.DisposeResource(m_nativeProviderHandle, resourceHandle);

                // Deliver the image
                resourceHandle = processedResource;
                return resourceHandle != IntPtr.Zero;
            }

            /// <summary>
            /// The CPU image API for interacting with the environment depth image.
            /// </summary>
            public override XRCpuImage.Api environmentDepthCpuImageApi => LightshipCpuImageApi.instance;


            /// <summary>
            /// Gets the occlusion texture descriptors associated with the current AR frame.
            /// </summary>
            /// <param name="defaultDescriptor">The default descriptor value.</param>
            /// <param name="allocator">The allocator to use when creating the returned <c>NativeArray</c>.</param>
            /// <returns>The occlusion texture descriptors.</returns>
            public override unsafe NativeArray<XRTextureDescriptor> GetTextureDescriptors(
                XRTextureDescriptor defaultDescriptor,
                Allocator allocator)
            {
                if (TryGetEnvironmentDepth(out var xrTextureDescriptor))
                {
                    var nativeArray = new NativeArray<XRTextureDescriptor>(1, allocator);
                    nativeArray[0] = xrTextureDescriptor;
                    return nativeArray;
                }

                return new NativeArray<XRTextureDescriptor>(0, allocator);
            }

            /// <summary>
            /// Get the enabled and disabled shader keywords for the material.
            /// </summary>
            /// <param name="enabledKeywords">The keywords to enable for the material.</param>
            /// <param name="disabledKeywords">The keywords to disable for the material.</param>
            public override void GetMaterialKeywords(out List<string> enabledKeywords,
                out List<string> disabledKeywords)
            {
                enabledKeywords = s_EnvironmentDepthEnabledMaterialKeywords;
                disabledKeywords = null;
            }
        }

        /// <summary>
        /// Container to wrap the native Lightship human body APIs.
        /// </summary>
        static class NativeApi
        {
#if NIANTIC_LIGHTSHIP_AR_LOADER_ENABLED
            [DllImport(_LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_OcclusionProvider_Construct")]
            public static extern IntPtr Construct(IntPtr unityContext);

            [DllImport(_LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_OcclusionProvider_Start")]
            public static extern void Start(IntPtr depthApiHandle);

            [DllImport(_LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_OcclusionProvider_Stop")]
            public static extern void Stop(IntPtr depthApiHandle);

            [DllImport(_LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_OcclusionProvider_Configure")]
            public static extern void Configure(IntPtr depthApiHandle, string modelUrl, uint frameRate);

            [DllImport(_LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_OcclusionProvider_Destruct")]
            public static extern void Destruct(IntPtr depthApiHandle);

            [DllImport(_LightshipPlugin.Name,
                EntryPoint = "Lightship_ARDK_Unity_OcclusionProvider_GetEnvironmentDepth")]
            public static extern IntPtr GetEnvironmentDepth
            (
                IntPtr depthApiHandle,
                out IntPtr memoryBuffer,
                out int size,
                out int width,
                out int height,
                out TextureFormat format,
                out uint frameId
            );

            [DllImport(_LightshipPlugin.Name,
                EntryPoint = "Lightship_ARDK_Unity_OcclusionProvider_Warp")]
            public static extern IntPtr Warp
            (
                IntPtr depthApiHandle,
                IntPtr depthResourceHandle,
                float[] poseMatrix,
                int targetWidth,
                int targetHeight,
                float backProjectionPlane,
                out IntPtr memoryBuffer,
                out int size
            );

            [DllImport(_LightshipPlugin.Name,
                EntryPoint = "Lightship_ARDK_Unity_OcclusionProvider_Blit")]
            public static extern IntPtr Blit
            (
                IntPtr depthApiHandle,
                IntPtr depthResourceHandle,
                int targetWidth,
                int targetHeight,
                out IntPtr memoryBuffer,
                out int size
            );

            [DllImport(_LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_OcclusionProvider_ReleaseResource")]
            public static extern IntPtr DisposeResource(IntPtr depthApiHandle, IntPtr resourceHandle);

#else
            public static IntPtr Construct(IntPtr unityContext)
            {
                throw new NotImplementedException();
            }

            public static void Start(IntPtr depthApiHandle)
            {
                throw new NotImplementedException();
            }

            public static void Stop(IntPtr depthApiHandle)
            {
                throw new NotImplementedException();
            }

            public static void Configure(IntPtr depthApiHandle, string modelUrl, uint frameRate)
            {
                throw new NotImplementedException();
            }

            public static void Destruct(IntPtr depthApiHandle)
            {
                throw new NotImplementedException();
            }

            public static IntPtr GetEnvironmentDepth(IntPtr depthApiHandle, out IntPtr memoryBuffer, out int size, out int width, out int height, out TextureFormat format, out uint frameId)
            {
                throw new NotImplementedException();
            }

            public static IntPtr Warp(IntPtr depthApiHandle, IntPtr depthResourceHandle, float[] poseMatrix, int targetWidth, int targetHeight, float backProjectionPlane, out IntPtr memoryBuffer, out int size)
            {
                throw new NotImplementedException();
            }

            public static IntPtr Blit(IntPtr depthApiHandle, IntPtr depthResourceHandle, int targetWidth,
                int targetHeight, out IntPtr memoryBuffer, out int size)
            {
                throw new NotImplementedException();
            }

            public static IntPtr DisposeResource(IntPtr depthApiHandle, IntPtr resourceHandle)
            {
                throw new NotImplementedException();
            }
#endif
        }
    }
}
