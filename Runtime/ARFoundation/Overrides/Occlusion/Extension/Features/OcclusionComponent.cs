// Copyright 2022-2025 Niantic.

using Niantic.Lightship.AR.ARFoundation.Unity;
using Niantic.Lightship.AR.Common;
using Niantic.Lightship.AR.Subsystems.Occlusion;
using Niantic.Lightship.AR.Utilities;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.ARSubsystems;

namespace Niantic.Lightship.AR.Occlusion.Features
{
    /// <summary>
    /// Base class for render components that implement an occlusion technique.
    /// </summary>
    internal abstract class OcclusionComponent : RenderComponent
    {
        protected override string Keyword
        {
            get => null;
        }

        /// <summary>
        /// The depth image acquired from the occlusion subsystem.
        /// </summary>
        public XRCpuImage CPUDepth { get; private set; }

        /// <summary>
        /// The depth texture that is used in the shader.
        /// </summary>
        public Texture2D GPUDepth { get; private set; }

        /// <summary>
        /// The matrix that converts from normalized viewport coordinates to normalized texture coordinates.
        /// </summary>
        public Matrix4x4 DepthTransform { get; private set; }

        // Components
        protected XROcclusionSubsystem OcclusionSubsystem { get; private set; }

        // Resources
        private Texture2D _tempTexture;
        private ARTextureInfo _platformDepthTextureInfo;

        /// <summary>
        /// Assigns the occlusion subsystem to the occlusion feature.
        /// </summary>
        public void Configure(XROcclusionSubsystem occlusionSubsystem)
        {
            OcclusionSubsystem = occlusionSubsystem;
        }

        /// <summary>
        /// Updates the XRCpuImage reference by fetching the latest depth image on cpu memory.
        /// If the XRCpuImage already exists, it will be disposed before updating. Finally,
        /// the function will update the reference to the depth image on gpu that is most
        /// appropriate for the current configuration.
        /// </summary>
        protected override void OnUpdate(Camera camera)
        {
            base.OnUpdate(camera);

            // Defaults
            GPUDepth = null;

            // Specify the screen as the viewport
            var viewport = new XRCameraParams
            {
                screenWidth = Screen.width,
                screenHeight = Screen.height,
                screenOrientation = XRDisplayContext.GetScreenOrientation()
            };

            // Using Lightship occlusion subsystem
            if (OcclusionSubsystem is LightshipOcclusionSubsystem lsSubsystem)
            {
                // When using lightship depth, we acquire the image in its native container
                // and use a custom matrix to fit it to the viewport. This matrix may contain
                // re-projection (warping) as well.
                if (lsSubsystem.TryAcquireEnvironmentDepthCpuImage(viewport, out var image, out var matrix))
                {
                    // Release the previous image
                    if (CPUDepth.valid)
                    {
                        CPUDepth.Dispose();
                    }

                    // Cache the reference to the new image
                    CPUDepth = image;

                    // Update the depth texture from the cpu image
                    var gotGpuImage = ImageSamplingUtils.CreateOrUpdateTexture(
                        source: CPUDepth,
                        destination: ref _tempTexture,
                        destinationFilter: FilterMode.Bilinear,
                        pushToGpu: true
                    );

                    GPUDepth = gotGpuImage ? _tempTexture : null;
                    DepthTransform = matrix;
                }
            }
            // Using foreign or simulation occlusion subsystem
            else if (OcclusionSubsystem != null)
            {
                if (OcclusionSubsystem.TryAcquireEnvironmentDepthCpuImage(out var image))
                {
                    // Release the previous image
                    if (CPUDepth.valid)
                    {
                        CPUDepth.Dispose();
                    }

                    // Cache the reference to the new image
                    CPUDepth = image;

                    // When using lidar, the image container will have the same aspect ratio
                    // as the camera image, according to ARFoundation standards. For the matrix
                    // here, we do not warp, just use an affine display matrix which is the same
                    // that is used to display the camera image.
                    DepthTransform = CameraMath.CalculateDisplayMatrix
                    (
                        image.width,
                        image.height,
                        (int)viewport.screenWidth,
                        (int)viewport.screenHeight,
                        viewport.screenOrientation
                    );

                    // We make a special case for Lidar on fastest setting, because there is
                    // a bug in ARFoundation that makes the manager owned texture flicker.
                    // By creating a texture from the cpu image and retaining ourselves, the
                    // image becomes stable. This issue is probably due to the changes introduced
                    // in iOS 16 where the metal command buffer do not implicitly retain textures.
                    if (OcclusionSubsystem.currentEnvironmentDepthMode == EnvironmentDepthMode.Fastest)
                    {
                        // Update the depth texture from the cpu image
                        var gotGpuImage = ImageSamplingUtils.CreateOrUpdateTexture(
                            source: CPUDepth,
                            destination: ref _tempTexture,
                            destinationFilter: FilterMode.Bilinear,
                            pushToGpu: true
                        );

                        GPUDepth = gotGpuImage ? _tempTexture : null;
                        return;
                    }

                    // We rely on the foreign occlusion subsystem to manage the gpu image
                    GPUDepth = GetPlatformDepthTexture();
                }
                // Using foreign or simulation occlusion subsystem, but no cpu depth image available
                else
                {
                    var texture = GetPlatformDepthTexture();
                    if (texture != null)
                    {
                        GPUDepth = texture;
                        DepthTransform = CameraMath.CalculateDisplayMatrix
                        (
                            texture.width,
                            texture.height,
                            (int)viewport.screenWidth,
                            (int)viewport.screenHeight,
                            viewport.screenOrientation
                        );
                    }
                }
            }
        }

        private Texture2D GetPlatformDepthTexture()
        {
            if (OcclusionSubsystem is null or LightshipOcclusionSubsystem)
            {
                return null;
            }

            if (OcclusionSubsystem.TryGetEnvironmentDepth(out var environmentDepthDescriptor))
            {
                _platformDepthTextureInfo = ARTextureInfo.GetUpdatedTextureInfo(_platformDepthTextureInfo,
                    environmentDepthDescriptor);
                Debug.Assert(
                    _platformDepthTextureInfo.Descriptor.dimension is TextureDimension.Tex2D
                        or TextureDimension.None,
                    "Environment depth texture needs to be a Texture 2D, but instead is "
                    + $"{_platformDepthTextureInfo.Descriptor.dimension.ToString()}.");
                return _platformDepthTextureInfo.Texture as Texture2D;
            }

            return null;
        }

        protected override void OnReleaseResources()
        {
            base.OnReleaseResources();

            if (_tempTexture != null)
            {
                Object.Destroy(_tempTexture);
            }

            if (CPUDepth.valid)
            {
                CPUDepth.Dispose();
            }

            _platformDepthTextureInfo.Dispose();
        }
    }
}
