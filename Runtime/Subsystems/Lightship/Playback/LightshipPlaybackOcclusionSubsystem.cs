// Copyright 2023 Niantic, Inc. All Rights Reserved.

using System;
using System.Collections.Generic;
using System.IO;
using Niantic.Lightship.AR.Utilities;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Scripting;
using UnityEngine.XR.ARSubsystems;

namespace Niantic.Lightship.AR.Playback
{
    [Preserve]
    public class LightshipPlaybackOcclusionSubsystem : XROcclusionSubsystem, IPlaybackDatasetUser
    {
        /// <summary>
        /// Register the Lightship Playback occlusion subsystem.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Register()
        {
            Debug.Log("LightshipPlaybackOcclusionSubsystem.Register");
            const string id = "Lightship-Playback-Occlusion";
            var xrOcclusionSubsystemCinfo = new XROcclusionSubsystemCinfo()
            {
                id = id,
                providerType = typeof(LightshipPlaybackProvider),
                subsystemTypeOverride = typeof(LightshipPlaybackOcclusionSubsystem),
                humanSegmentationStencilImageSupportedDelegate = () => Supported.Unsupported,
                humanSegmentationDepthImageSupportedDelegate = () => Supported.Unsupported,
                environmentDepthImageSupportedDelegate = () => Supported.Supported,
                environmentDepthConfidenceImageSupportedDelegate = () => Supported.Supported,
                environmentDepthTemporalSmoothingSupportedDelegate = () => Supported.Unsupported
            };

            XROcclusionSubsystem.Register(xrOcclusionSubsystemCinfo);
        }

        void IPlaybackDatasetUser.SetPlaybackDatasetReader(PlaybackDatasetReader reader)
        {
            ((IPlaybackDatasetUser)provider).SetPlaybackDatasetReader(reader);
        }

        /// <summary>
        /// The implementation provider class.
        /// </summary>
        class LightshipPlaybackProvider : Provider, IPlaybackDatasetUser
        {
            /// <summary>
            /// The shader property name for the environment depth texture.
            /// </summary>
            /// <value>
            /// The shader property name for the environment depth texture.
            /// </value>
            private const string k_TextureEnvironmentDepthPropertyName = "_EnvironmentDepth";

            /// <summary>
            /// The shader property name for the environment depth confidence texture.
            /// </summary>
            /// <value>
            /// The shader property name for the environment depth confidence texture.
            /// </value>
            const string k_TextureEnvironmentDepthConfidencePropertyName = "_EnvironmentDepthConfidence";

            /// <summary>
            /// The shader property name identifier for the environment depth texture.
            /// </summary>
            /// <value>
            /// The shader property name identifier for the environment depth texture.
            /// </value>
            private static readonly int k_TextureEnvironmentDepthPropertyId =
                Shader.PropertyToID(k_TextureEnvironmentDepthPropertyName);

            /// <summary>
            /// The shader property name identifier for the environment depth texture.
            /// </summary>
            /// <value>
            /// The shader property name identifier for the environment depth texture.
            /// </value>
            static readonly int k_TextureEnvironmentDepthConfidencePropertyId =
                Shader.PropertyToID(k_TextureEnvironmentDepthConfidencePropertyName);

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

            private PlaybackDatasetReader m_DatasetReader;

            // This value will strongly affect memory usage.  It can also be set by the user in configuration.
            // The value represents the number of frames in memory before the user must make a copy of the data
            private const int k_FramesInMemoryCount = 2;
            private BufferedTextureCache m_EnvironmentDepthTextures;
            private BufferedTextureCache m_EnvironmentConfidenceTextures;

            private EnvironmentDepthMode m_requestedEnvironmentDepthMode;

            /// <summary>
            /// Construct the implementation provider.
            /// </summary>
            public LightshipPlaybackProvider()
            {
                Debug.Log("LightshipPlaybackOcclusionSubsystem.LightshipPlaybackProvider construct");

                // Default depth mode
                m_requestedEnvironmentDepthMode = EnvironmentDepthMode.Fastest;
            }

            public override void Start() { }

            public override void Stop() { }

            /// <summary>
            /// Destroy the provider.
            /// </summary>
            public override void Destroy()
            {
                m_EnvironmentDepthTextures.Dispose();
                m_EnvironmentConfidenceTextures.Dispose();

                m_DatasetReader = null;
            }

            public void SetPlaybackDatasetReader(PlaybackDatasetReader reader)
            {
                m_DatasetReader = reader;

                var depthRes = m_DatasetReader.GetDepthResolution();

                m_EnvironmentDepthTextures =
                    new BufferedTextureCache
                    (
                        k_FramesInMemoryCount,
                        depthRes.x,
                        depthRes.y,
                        TextureFormat.RFloat,
                        true
                    );

                m_EnvironmentConfidenceTextures =
                    new BufferedTextureCache
                    (
                        k_FramesInMemoryCount,
                        depthRes.x,
                        depthRes.y,
                        TextureFormat.R8,
                        true
                    );
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
                set { m_requestedEnvironmentDepthMode = value; }
            }

            /// <summary>
            /// Property to get the current environment depth mode.
            /// </summary>
            public override EnvironmentDepthMode currentEnvironmentDepthMode => m_requestedEnvironmentDepthMode;

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
                // TODO (kcho): Check if LiDAR frames are fetched after subsystem is stopped by ARKit
                if (m_DatasetReader == null)
                {
                    xrTextureDescriptor = default;
                    return false;
                }

                var frame = m_DatasetReader.CurrFrame;
                if (frame == null || !frame.HasDepth)
                {
                    xrTextureDescriptor = default;
                    return false;
                }

                var path = Path.Combine(m_DatasetReader.GetDatasetPath(), frame.DepthPath);

                m_EnvironmentDepthTextures.GetUpdatedTextureFromPath
                (
                    path,
                    (uint)frame.Sequence,
                    out IntPtr nativeTexturePtr
                );

                var depthResolution = m_DatasetReader.GetDepthResolution();
                xrTextureDescriptor =
                    new XRTextureDescriptor
                    (
                        nativeTexturePtr,
                        depthResolution.x,
                        depthResolution.y,
                        0,
                        TextureFormat.RFloat,
                        k_TextureEnvironmentDepthPropertyId,
                        0,
                        TextureDimension.Tex2D
                    );

                return true;
            }

            //  Values are 0, 1, and 2 for low, medium, and high, respectively.
            public override bool TryGetEnvironmentDepthConfidence
            (
                out XRTextureDescriptor environmentDepthConfidenceDescriptor
            )
            {
                // TODO (kcho): Check if LiDAR frames are fetched after subsystem is stopped by ARKit
                if (m_DatasetReader == null)
                {
                    environmentDepthConfidenceDescriptor = default;
                    return false;
                }

                var frame = m_DatasetReader.CurrFrame;
                if (frame == null || !frame.HasDepth)
                {
                    environmentDepthConfidenceDescriptor = default;
                    return false;
                }

                var path = Path.Combine(m_DatasetReader.GetDatasetPath(), frame.DepthConfidencePath);

                m_EnvironmentConfidenceTextures.GetUpdatedTextureFromPath
                (
                    path,
                    (uint)frame.Sequence,
                    out IntPtr nativeTexturePtr
                );

                var res = m_DatasetReader.GetDepthResolution();
                environmentDepthConfidenceDescriptor =
                    new XRTextureDescriptor
                    (
                        nativeTexturePtr,
                        res.x,
                        res.y,
                        0,
                        TextureFormat.R8,
                        k_TextureEnvironmentDepthConfidencePropertyId,
                        0,
                        TextureDimension.Tex2D
                    );

                return true;
            }


            /// <summary>
            /// Gets the occlusion texture descriptors associated with the current AR frame.
            /// </summary>
            /// <param name="defaultDescriptor">The default descriptor value.</param>
            /// <param name="allocator">The allocator to use when creating the returned <c>NativeArray</c>.</param>
            /// <returns>The occlusion texture descriptors.</returns>
            public override NativeArray<XRTextureDescriptor> GetTextureDescriptors
            (
                XRTextureDescriptor defaultDescriptor,
                Allocator allocator
            )
            {
                // TODO: verify if confidence descriptor is included
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
    }
}
