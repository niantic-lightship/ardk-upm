// Copyright 2022-2024 Niantic.

using System;
using Unity.Collections;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.Management;

namespace Niantic.Lightship.AR.Occlusion
{
    /// <summary>
    /// Container for data relevant to manipulating the depth buffer.
    /// </summary>
    public struct OcclusionContext
    {
        /// Linear eye-depth from the camera to the occludee
        [Obsolete("Use XRDisplayContext.OccludeeEyeDepth instead.")]
        public float OccludeeEyeDepth;

        /// Global setting
        [Obsolete("Use XRDisplayContext instead.")]
        public static OcclusionContext Shared;

        static OcclusionContext()
        {
            ResetOccludee();
        }

        private static void ResetOccludee()
        {
            Shared.OccludeeEyeDepth = 5.0f;
        }

        private float? _cachedAspectRatio;

        [Obsolete("Instead, calculate the camera image aspect ratio by using the XRCameraSubsystem.TryGetIntrinsics method's return value.")]
        public bool TryGetCameraImageAspectRatio(out float aspectRatio)
        {
            if (_cachedAspectRatio.HasValue)
            {
                aspectRatio = _cachedAspectRatio.Value;
                return true;
            }

            XRCameraSubsystem cameraSubsystem = null;
            if (XRGeneralSettings.Instance != null && XRGeneralSettings.Instance.Manager != null)
            {
                var loader = XRGeneralSettings.Instance.Manager.activeLoader;
                if (loader != null)
                    cameraSubsystem = loader.GetLoadedSubsystem<XRCameraSubsystem>();
            }

            if (cameraSubsystem == null)
            {
                aspectRatio = 0f;
                return false;
            }

            int width = 0;
            int height = 0;
            var descriptors = cameraSubsystem.GetTextureDescriptors(Allocator.Temp);
            if (descriptors.Length > 0)
            {
                // Use the size of the largest image plane
                var size = 0;
                for (var i = 0; i < descriptors.Length; i++)
                {
                    var plane = descriptors[i];
                    var planeSize = plane.width * plane.height;
                    if (planeSize > size)
                    {
                        size = planeSize;
                        width = plane.width;
                        height = plane.height;
                    }
                }
            }

            descriptors.Dispose();

            if (width > 0 && height > 0)
            {
                aspectRatio = width / (float)height;
                _cachedAspectRatio = aspectRatio;
                return true;
            }

            aspectRatio = 0f;
            return false;
        }
    }
}
