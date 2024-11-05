// Copyright 2022-2024 Niantic.

using System;
using UnityEngine;
using UnityEngine.Rendering;

#if !MODULE_URP_ENABLED
using UnityEngine.Rendering;
#else
using UnityEngine.Rendering.Universal;
#endif

namespace Niantic.Lightship.AR.Occlusion
{
    public class LightshipFusedDepthCamera : MonoBehaviour
    {
        /// <summary>
        /// Use this to get the material in use for rendering the fused depth texture or inject an external one.
        /// </summary>
        internal Material Material
        {
            get
            {
                return _externalMaterial != null ? _externalMaterial : _internalMaterial;
            }

            set
            {
                _externalMaterial = value;
            }
        }

        /// <summary>
        /// The GPU texture containing depth values of the fused mesh.
        /// </summary>
        internal RenderTexture GpuTexture { get; private set; }

        // Resources
        private Camera _camera;
        private Material _internalMaterial;
        private Material _externalMaterial;

        // Helpers
        public const string DefaultShaderName = "Lightship/CopyEyeDepth";

        /// <summary>
        /// Configures the camera for capturing depth.
        /// </summary>
        /// <param name="mainCamera">The main rendering camera to copy attributes from.</param>
        /// <param name="meshLayer">The layer of the fused mesh.</param>
        internal void Configure(Camera mainCamera, int meshLayer)
        {
            // Configure camera
            _camera.clearFlags = CameraClearFlags.Depth;
            _camera.cullingMask = 1 << meshLayer;
            _camera.nearClipPlane = mainCamera.nearClipPlane;
            _camera.farClipPlane = mainCamera.farClipPlane;
            _camera.depth = mainCamera.depth - 1;

            // Set the camera to yield a depth texture
#if MODULE_URP_ENABLED
            _camera.GetUniversalAdditionalCameraData().requiresDepthOption = CameraOverrideOption.On;
#else
            _camera.depthTextureMode = DepthTextureMode.Depth;
#endif
        }

        /// <summary>
        /// Updates the view and projection matrices of the camera.
        /// </summary>
        /// <param name="worldToCamera">The view matrix.</param>
        /// <param name="projection">The projection matrix.</param>
        internal void SetViewProjection(Matrix4x4 worldToCamera, Matrix4x4 projection)
        {
            _camera.worldToCameraMatrix = worldToCamera;
            _camera.projectionMatrix = projection;
        }

        private void Awake()
        {
            // Prerequisites
            var shader = Shader.Find(DefaultShaderName);
            if (shader == null)
            {
                throw new InvalidOperationException
                (
                    $"Could not find shader named '{DefaultShaderName}' required " +
                    "for depth processing."
                );
            }

            // Reset transform
            var cameraTransform = transform;
            cameraTransform.localPosition = Vector3.zero;
            cameraTransform.localRotation = Quaternion.identity;
            cameraTransform.localScale = Vector3.one;

            // Allocate GPU texture
            GpuTexture = new RenderTexture(1024, 1024, 16, RenderTextureFormat.RFloat)
            {
                filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp
            };

            GpuTexture.Create();

            // Allocate a camera
            _camera = gameObject.AddComponent<Camera>();

            // Allocate the material
            _internalMaterial = new Material(shader);
        }

        private void OnDestroy()
        {
            if (_camera != null)
            {
                Destroy(_camera);
            }

            if (_internalMaterial != null)
            {
                Destroy(_internalMaterial);
            }

            if (GpuTexture != null)
            {
                Destroy(GpuTexture);
            }
        }

#if !MODULE_URP_ENABLED
        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            Graphics.Blit(null, GpuTexture, Material);
        }
#else
        private OffScreenBlitPass _renderPass;

        private void OnEnable()
        {
            // Allocate the render pass
            _renderPass = new OffScreenBlitPass(
                name: "Lightship Occlusion Extension (Fused Depth)",
                renderPassEvent: RenderPassEvent.AfterRendering);

            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;

            // Release the render pass
            _renderPass = null;
        }

        private void OnBeginCameraRendering(ScriptableRenderContext context, Camera cam)
        {
            if (cam == _camera)
            {
                // Configure the render pass
                _renderPass.Setup(Material, GpuTexture);

                // Enqueue the render pass
                cam.GetUniversalAdditionalCameraData().scriptableRenderer.EnqueuePass(_renderPass);
            }
        }
#endif
    }
}
