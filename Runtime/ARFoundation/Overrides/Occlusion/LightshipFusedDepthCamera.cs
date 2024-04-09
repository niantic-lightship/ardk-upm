// Copyright 2022-2024 Niantic.

using UnityEngine;

namespace Niantic.Lightship.AR.Occlusion
{
    public class LightshipFusedDepthCamera : MonoBehaviour
    {
        /// <summary>
        /// The GPU texture containing depth values of the fused mesh.
        /// </summary>
        internal RenderTexture GpuTexture { get; private set; }

        // Resources
        private Camera _camera;

        /// <summary>
        /// Configures the camera for capturing depth.
        /// </summary>
        /// <param name="meshLayer">The layer of the fused mesh.</param>
        internal void Configure(int meshLayer)
        {
            // Configure camera
            _camera.clearFlags = CameraClearFlags.Depth;
            _camera.depthTextureMode = DepthTextureMode.Depth;
            _camera.cullingMask = 1 << meshLayer;
            _camera.nearClipPlane = 0.1f;
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
            // Reset transform
            var cameraTransform = transform;
            cameraTransform.localPosition = Vector3.zero;
            cameraTransform.localRotation = Quaternion.identity;
            cameraTransform.localScale = Vector3.one;

            // Allocate GPU texture
            GpuTexture = new RenderTexture(1024, 1024, 16, RenderTextureFormat.Depth)
            {
                filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp
            };

            GpuTexture.Create();

            // Allocate a camera
            _camera = gameObject.AddComponent<Camera>();
            _camera.targetTexture = GpuTexture;
        }

        private void OnDestroy()
        {
            Destroy(_camera);

            if (GpuTexture != null)
            {
                Destroy(GpuTexture);
            }
        }
    }
}
