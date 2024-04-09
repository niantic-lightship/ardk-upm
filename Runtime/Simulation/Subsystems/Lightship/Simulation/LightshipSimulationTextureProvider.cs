// Copyright 2022-2024 Niantic.

using System;
using System.Collections.Generic;
using Niantic.Lightship.AR.Subsystems.Playback;
using Niantic.Lightship.AR.Utilities;
using Unity.Collections;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.Simulation;

namespace Niantic.Lightship.AR.Simulation
{
    /// <summary>
    /// Based on Unity Simulation's CameraTextureProvider.
    /// Input handler for movement in the device view and game view when using <c>CameraPoseProvider</c>.
    /// </summary>
    internal abstract class LightshipSimulationTextureProvider : MonoBehaviour
    {
        internal static event Action<Camera> preRenderCamera;
        internal static event Action<Camera> postRenderCamera;
        internal event Action<LightshipCameraTextureFrameEventArgs> frameReceived;

        protected Camera m_XrCamera;
        protected Camera m_SimulationRenderCamera;
        protected RenderTexture m_RenderTexture;
        protected Texture2D m_ProviderTexture;
        protected IntPtr m_TexturePtr;
        protected LightshipCameraTextureFrameEventArgs? m_CameraFrameEventArgs;

        private readonly List<Texture2D> m_CameraImagePlanes = new List<Texture2D>();

        internal LightshipCameraTextureFrameEventArgs? CameraFrameEventArgs => m_CameraFrameEventArgs;

        protected bool _initialized;

        private int _sensorWidth = 720;
        private int _sensorHeight = 540;
        private float _sensorFocalLength = 623.5382f;

        internal virtual void OnDestroy()
        {
            if (m_SimulationRenderCamera != null)
                m_SimulationRenderCamera.targetTexture = null;

            if (m_RenderTexture != null)
                m_RenderTexture.Release();

            if (m_ProviderTexture != null)
                UnityObjectUtils.Destroy(m_ProviderTexture);
        }

        private void Update()
        {
            if (!_initialized)
                return;

            // Currently assuming the main camera is being set to the correct settings for rendering to the target device
            m_XrCamera.ResetProjectionMatrix();
            DoCameraRender(m_SimulationRenderCamera);

            if (!m_RenderTexture.IsCreated() && !m_RenderTexture.Create())
                return;

            if (m_ProviderTexture.width != m_RenderTexture.width
                || m_ProviderTexture.height != m_RenderTexture.height)
            {
                if (!m_ProviderTexture.Reinitialize(m_RenderTexture.width, m_RenderTexture.height))
                    return;

                m_TexturePtr = m_ProviderTexture.GetNativeTexturePtr();
            }

            Graphics.CopyTexture(m_RenderTexture, m_ProviderTexture);

            m_CameraImagePlanes.Clear();
            m_CameraImagePlanes.Add(m_ProviderTexture);

            var orientation = transform.localToWorldMatrix.GetScreenOrientation();

            var displayMatrix =
                CameraMath.CalculateDisplayMatrix
                (
                    m_RenderTexture.width,
                    m_RenderTexture.height,
                    Screen.width,
                    Screen.height,
                    orientation,
                    true,
                    // Via ARF, external textures are formatted with their
                    // first pixel in the top left. Thus the displayMatrix needs
                    // to include a flip because Unity displays textures with
                    // their first pixel in the bottom left.
                    // For this we invert the projection matrix in OnPreCull
                    layout: CameraMath.MatrixLayout.RowMajor
                );

            m_SimulationRenderCamera.ResetProjectionMatrix();
            var projectionMatrix = m_SimulationRenderCamera.projectionMatrix;

            // because this camera is rotated 90 degrees we have to temporarily swap the field of view axis when we
            // send the projection matrix to the ar camera.
            if (orientation == ScreenOrientation.Portrait || orientation == ScreenOrientation.PortraitUpsideDown)
            {
                var localFieldOfView = m_SimulationRenderCamera.fieldOfView;
                m_SimulationRenderCamera.fieldOfView = Camera.VerticalToHorizontalFieldOfView(m_SimulationRenderCamera.fieldOfView, m_SimulationRenderCamera.aspect);
                projectionMatrix = m_SimulationRenderCamera.projectionMatrix;
                m_SimulationRenderCamera.fieldOfView = localFieldOfView;
            }

            var frameEventArgs = new LightshipCameraTextureFrameEventArgs
            {
                timestampNs = (long)(DateTimeOffset.UtcNow.ToUnixTimeSeconds() * 1e9),
                projectionMatrix = projectionMatrix,
                displayMatrix = displayMatrix,
                intrinsics = GetCameraIntrinsics(),
                textures = m_CameraImagePlanes,
            };

            frameReceived?.Invoke(frameEventArgs);
        }

        private XRCameraIntrinsics GetCameraIntrinsics()
        {
            return new XRCameraIntrinsics
                (
                    new Vector2(_sensorFocalLength, _sensorFocalLength),
                    new Vector2(_sensorWidth/2f, _sensorHeight/2f),
                    new Vector2Int(m_SimulationRenderCamera.scaledPixelWidth, m_SimulationRenderCamera.scaledPixelHeight)
                );
        }

        protected virtual void InitializeProvider(Camera xrCamera, Camera simulationCamera)
        {
            var simulationEnvironmentLayer = 1 << XRSimulationRuntimeSettings.Instance.environmentLayer;

            simulationCamera.cullingMask = simulationEnvironmentLayer;
            simulationCamera.depth = xrCamera.depth - 1;

            xrCamera.clearFlags = CameraClearFlags.Color;
            xrCamera.backgroundColor = Color.black;
            // our ar camera will be stripped from seeing the simulation layer
            xrCamera.cullingMask -= simulationEnvironmentLayer;

            simulationCamera.usePhysicalProperties = true;
            // sensor size is in mm, pixel size comes from sensor size divided by target texture resolution
            // we make pixels on the sensor 1mm by later setting the target textures to the same values as sensor size
            simulationCamera.sensorSize = new Vector2(_sensorHeight, _sensorWidth);
            var calculatedFov = Camera.FocalLengthToFieldOfView(_sensorFocalLength, simulationCamera.sensorSize.y);
            simulationCamera.fieldOfView = calculatedFov;
        }

        private void DoCameraRender(Camera renderCamera)
        {
            preRenderCamera?.Invoke(renderCamera);
            renderCamera.Render();
            postRenderCamera?.Invoke(renderCamera);
        }

        internal abstract bool TryGetTextureDescriptors(out NativeArray<XRTextureDescriptor> planeDescriptors,
            Allocator allocator);

        internal bool TryGetLatestImagePtr(out IntPtr nativePtr)
        {
            if (m_CameraImagePlanes != null && m_CameraImagePlanes.Count > 0 && m_ProviderTexture != null
                && m_ProviderTexture.isReadable)
            {
                nativePtr =  m_TexturePtr;
                return true;
            }

            nativePtr = IntPtr.Zero;
            return false;
        }

        // upside down rendering to put first pixel top left (png convention) instead of bottom left (Unity texture)
        private void OnPreCull () {
            m_SimulationRenderCamera.ResetProjectionMatrix ();
            m_SimulationRenderCamera.projectionMatrix *= Matrix4x4.Scale(new Vector3(1, -1, 1));
        }

        // since we are inverting the projection matrix, we need to invert culling so normals are considered inverted.
        // this would normally be done to achieve reflection and mirroring effects
        private void OnPreRender ()
        {
            GL.invertCulling = true;
        }

        private void OnPostRender () {
            GL.invertCulling = false;
        }
    }
}
