// Copyright 2022-2025 Niantic.

using Niantic.Lightship.AR.Common;
using Niantic.Lightship.AR.Utilities;
using Niantic.Lightship.AR.Utilities.Logging;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.ARFoundation;

#if MODULE_URP_ENABLED
using UnityEngine.Rendering.Universal;
#endif

namespace Niantic.Lightship.AR.Occlusion.Features
{
    /// <summary>
    /// A feature to stabilize the occlusion map by blending between per-frame and fused depth.
    /// </summary>
    internal sealed class Stabilization : RenderComponent
    {
        // Error messages
        private const string KMissingMeshManagerMessage =
            "Missing ARMeshManager component reference. " +
            "One in the scene is required to enable occlusion stabilization.";
        private const string KMissingMaterialMessage =
            "Null material reference passed or could not find shader named " + RequiredShaderName +
            " required for occlusion stabilization.";
        private const string KMissingResourcesMessage =
            "Missing depth texture or extrinsics matrix for occlusion stabilization.";
        private const string KUndeterminedOcclusionTechniqueMessage =
            "The occlusion technique is undetermined.";

        /// <summary>
        /// Shader keyword for occlusion stabilization.
        /// </summary>
        protected override string Keyword
        {
            get => "FEATURE_STABILIZATION";
        }

        /// <summary>
        /// The shader used by the mesh observer camera to copy the z-buffer into a texture.
        /// </summary>
        public const string RequiredShaderName = "Lightship/CopyEyeDepth";

        // Observer resources
        private Camera _meshObserverCamera;
        private Material _meshObserverMaterialInternal;
        private CommandBuffer _meshObserverCommandBuffer;

        // Additional resources
        private Texture2D _defaultMeshDepthTexture;
        public RenderTexture MeshDepthTexture { get; private set; }

        // URP render pass
#if MODULE_URP_ENABLED
        private OffScreenBlitPass _renderPass;

        private RTHandle _meshDepthTextureHandle;

        private RTHandleSystem _rtHandleSystem;
#endif

        /// <summary>
        /// The material used by the mesh observer camera to copy the z-buffer into a texture.
        /// </summary>
        public Material Material { get; private set; }

        /// <summary>
        /// Whether to prefer per-frame (0) or fused depth (1) during occlusion stabilization.
        /// </summary>
        public float Threshold
        {
            get => _threshold;
            set => _threshold = Mathf.Clamp(value, 0.0f, 1.0f);
        }

        private float _threshold = 0.5f;
        private Matrix4x4? _fusedDepthTransform;
        private LightshipOcclusionExtension.OcclusionTechnique _technique;

        // Constants
        private const int KFusedDepthWidth = 1024;
        private const int KFusedDepthHeight = 1024;
        private const int KFusedDepthDepthBits = 16;

        public bool Configure(
            LightshipOcclusionExtension.OcclusionTechnique technique,
            Camera renderingCamera,
            ARMeshManager meshManager,
            Material externalMaterial = null)
        {
            // Verify the mesh manager
            if (meshManager == null)
            {
                Debug.LogError(KMissingMeshManagerMessage);
                return false;
            }

            // Create the built-in material if an external material is not provided
            var useInternalMaterial = externalMaterial == null;
            if (useInternalMaterial)
            {
                _meshObserverMaterialInternal = new Material(Shader.Find(RequiredShaderName));
            }

            // Verify the material
            Material = useInternalMaterial ? _meshObserverMaterialInternal : externalMaterial;
            if (Material == null)
            {
                Debug.LogError(KMissingMaterialMessage);
                return false;
            }

            // The occlusion technique must be decided before adding this feature
            _technique = technique;
            if (_technique == LightshipOcclusionExtension.OcclusionTechnique.Automatic)
            {
                Log.Error(KUndeterminedOcclusionTechniqueMessage);
                return false;
            }


            // Allocate the GPU texture
            MeshDepthTexture =
                new RenderTexture(KFusedDepthWidth, KFusedDepthHeight, KFusedDepthDepthBits, RenderTextureFormat.RFloat)
                {
                    filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp
                };

            MeshDepthTexture.Create();

            // Create and configure the camera
            _meshObserverCamera = CreateMeshObserverCamera(renderingCamera, meshManager.meshPrefab.gameObject.layer);

#if !MODULE_URP_ENABLED
            // Create the command buffer
            _meshObserverCommandBuffer = new CommandBuffer {name = "Occlusion Stabilization"};
            _meshObserverCommandBuffer.Blit(null, MeshDepthTexture, Material);

            // Add the command buffer to the camera
            _meshObserverCamera.AddCommandBuffer(CameraEvent.AfterDepthTexture, _meshObserverCommandBuffer);
            _meshObserverCamera.AddCommandBuffer(CameraEvent.AfterGBuffer, _meshObserverCommandBuffer);
#else
            // Allocate the render pass
            _renderPass = new OffScreenBlitPass(
                name: "Lightship Occlusion Extension (Stabilization)",
                renderPassEvent: RenderPassEvent.AfterRendering);

            _rtHandleSystem = new RTHandleSystem();
            _rtHandleSystem.Initialize(KFusedDepthWidth, KFusedDepthHeight);
            _meshDepthTextureHandle = _rtHandleSystem.Alloc(MeshDepthTexture);
#endif
            return true;
        }

        protected override void OnMaterialAttach(Material mat)
        {
            base.OnMaterialAttach(mat);

            // Register the camera rendering event (urp)
            RenderPipelineManager.beginCameraRendering += EnqueueUniversalRenderPass;

            // The default texture bound to the fused depth property on the material.
            // It lets every pixel pass through until the real depth texture is ready to use.
            if (_defaultMeshDepthTexture == null)
            {
                // The mesh depth texture contains linear eye depth
                // Here we default to 1000 meters, because we don't
                // have access to the camera's far clip plane.
                const float val = 1000.0f;

                _defaultMeshDepthTexture = new Texture2D(2, 2, TextureFormat.RFloat, mipChain: false);
                _defaultMeshDepthTexture.SetPixelData(new[] {val, val, val, val}, 0);
                _defaultMeshDepthTexture.Apply(false);
            }

            // Bind pass-through texture by default
            mat.SetTexture(ShaderProperties.FusedDepthTextureId, _defaultMeshDepthTexture);
        }

        protected override void OnMaterialDetach(Material mat)
        {
            base.OnMaterialDetach(mat);

            // Unregister the camera rendering event (urp)
            RenderPipelineManager.beginCameraRendering -= EnqueueUniversalRenderPass;
        }

        protected override void OnUpdate(Camera camera)
        {
            base.OnUpdate(camera);

            Matrix4x4 pose;
            if (_technique == LightshipOcclusionExtension.OcclusionTechnique.OcclusionMesh)
            {
                // Acquire resources from the frame
                var frameDepthTexture = GetTexture(ShaderProperties.DepthTextureId);
                var frameDepthExtrinsics = GetMatrix(ShaderProperties.ExtrinsicsId);
                if (!frameDepthExtrinsics.HasValue || frameDepthTexture == null)
                {
                    Log.Warning(KMissingResourcesMessage);
                    return;
                }

                // The resolution of the fused depth texture is arbitrary, since it will always capture the full screen.
                // Here we need to calculate a transform that takes from the frame depth texture coordinates to screen
                // coordinates. This is because the occlusion mesh shader calculates its UVs based on the occluder plane
                // mesh that was created from the texture.
                _fusedDepthTransform = AffineMath.Fit
                (
                    Screen.width,
                    Screen.height,
                    XRDisplayContext.GetScreenOrientation(),
                    frameDepthTexture.width,
                    frameDepthTexture.height,
                    ScreenOrientation.LandscapeLeft
                ).transpose;

                // Drive the mesh observer camera from image extrinsics
                pose = frameDepthExtrinsics.Value *
                    Matrix4x4.Rotate(CameraMath.DisplayToCameraRotation(XRDisplayContext.GetScreenOrientation()));
            }
            else
            {
                // Drive the mesh observer camera from the main camera
                pose = camera.transform.localToWorldMatrix;
            }

            // Update the projection matrix
            _meshObserverCamera.projectionMatrix = camera.projectionMatrix;

            // Apply pose
            _meshObserverCamera.transform.SetPositionAndRotation(
                MatrixUtils.ToPosition(pose),
                MatrixUtils.ToRotation(pose));
        }

        protected override void OnMaterialUpdate(Material mat)
        {
            base.OnMaterialUpdate(mat);

            mat.SetFloat(ShaderProperties.StabilizationThreshold, _threshold);
            mat.SetTexture(ShaderProperties.FusedDepthTextureId, MeshDepthTexture);

            if (_fusedDepthTransform.HasValue)
            {
                mat.SetMatrix(ShaderProperties.FusedDepthTransformId, _fusedDepthTransform.Value);
            }
        }

        protected override void OnReleaseResources()
        {
            base.OnReleaseResources();

            if (_meshObserverCamera != null)
            {
                Object.Destroy(_meshObserverCamera.gameObject);
            }

            if (_meshObserverMaterialInternal != null)
            {
                Object.Destroy(_meshObserverMaterialInternal);
            }

            if (_defaultMeshDepthTexture != null)
            {
                Object.Destroy(_defaultMeshDepthTexture);
            }

            if (MeshDepthTexture != null)
            {
                Object.Destroy(MeshDepthTexture);
            }

#if MODULE_URP_ENABLED
            if( _meshDepthTextureHandle != null )
            {
                _meshDepthTextureHandle.Release();
            }
#endif

            _meshObserverCommandBuffer?.Dispose();
        }

        /// <summary>
        /// Creates a camera to observe the mesh layer.
        /// </summary>
        /// <param name="mainCamera">The camera that renders the scene.</param>
        /// <param name="meshLayer">The index of the mesh layer.</param>
        /// <returns>The new camera that observes the mesh layer.</returns>
        private static Camera CreateMeshObserverCamera(Camera mainCamera, int meshLayer)
        {
            // Allocate camera
            var result = new GameObject("MeshObserverCamera").AddComponent<Camera>();

            // Reset the camera's transform
            result.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            result.transform.localScale = Vector3.one;

            // Configure the camera
            result.clearFlags = CameraClearFlags.Depth;
            result.cullingMask = 1 << meshLayer;
            result.nearClipPlane = mainCamera.nearClipPlane;
            result.farClipPlane = mainCamera.farClipPlane;
            result.depth = mainCamera.depth - 1;
            EnableDepthCapture(result);

            // We don't have ownership of the camera in the scene
            // hierarchy, so we prevent destroying the camera on
            // scene load. The camera will be destroyed when the
            // render component is released.
            Object.DontDestroyOnLoad(result);

            return result;
        }

        /// <summary>
        /// Sets the camera to yield a depth texture.
        /// </summary>
        private static void EnableDepthCapture(Camera camera)
        {
#if MODULE_URP_ENABLED
            camera.GetUniversalAdditionalCameraData().requiresDepthOption = CameraOverrideOption.On;
#else
            camera.depthTextureMode = DepthTextureMode.Depth;
#endif
        }

        private void EnqueueUniversalRenderPass(ScriptableRenderContext context, Camera cam)
        {
#if MODULE_URP_ENABLED
            if (cam == _meshObserverCamera)
            {
                // Configure the render pass
                _renderPass.Setup(Material, MeshDepthTexture, _meshDepthTextureHandle);

                // Enqueue the render pass
                cam.GetUniversalAdditionalCameraData().scriptableRenderer.EnqueuePass(_renderPass);
            }
#endif
        }
    }
}
