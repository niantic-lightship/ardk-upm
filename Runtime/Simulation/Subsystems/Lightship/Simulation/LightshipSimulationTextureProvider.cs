// Copyright 2022-2024 Niantic.

using System;
using System.Collections.Generic;
using Niantic.Lightship.AR.Subsystems.Playback;
using Niantic.Lightship.AR.Utilities;
using Unity.Collections;
using Unity.XR.CoreUtils;
using UnityEditor;
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
        internal static event Action<Camera> PreRenderCamera;
        internal static event Action<Camera> PostRenderCamera;
        internal event Action<LightshipCameraTextureFrameEventArgs> FrameReceived;

        protected Camera XrCamera;
        protected Camera SimulationRenderCamera;
        protected RenderTexture RenderTexture;
        protected Texture2D ProviderTexture;
        protected IntPtr TexturePtr;

        private Texture2D _cameraPlane = default;

        internal LightshipCameraTextureFrameEventArgs? CameraFrameEventArgs;

        protected bool Initialized;

        private const int SensorWidth = 720;
        private const int SensorHeight = 540;
        private const float SensorFocalLength = 623.5382f;

        private void Update()
        {
            if (!Initialized)
                return;

            // Currently assuming the main camera is being set to the correct settings for rendering to the target device
            XrCamera.ResetProjectionMatrix();
            DoCameraRender(SimulationRenderCamera);

            if (!RenderTexture.IsCreated() && !RenderTexture.Create())
                return;

            if (ProviderTexture.width != RenderTexture.width
                || ProviderTexture.height != RenderTexture.height)
            {
                if (!ProviderTexture.Reinitialize(RenderTexture.width, RenderTexture.height))
                    return;

                TexturePtr = ProviderTexture.GetNativeTexturePtr();
            }

            Graphics.CopyTexture(RenderTexture, ProviderTexture);

            //m_CameraImagePlanes.Clear();
            //m_CameraImagePlanes.Add(m_ProviderTexture);
            _cameraPlane = ProviderTexture;

            var orientation = transform.localToWorldMatrix.GetScreenOrientation();

            var displayMatrix =
                CameraMath.CalculateDisplayMatrix
                (
                    RenderTexture.width,
                    RenderTexture.height,
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

            SimulationRenderCamera.ResetProjectionMatrix();
            var projectionMatrix = SimulationRenderCamera.projectionMatrix;

            // projectionMatrix dictates the AR camera's vertical FOV.
            // we can calculate the corresponding matrix by temporarily setting the simulation camera's FOV
            float originalVerticalFOV = SimulationRenderCamera.fieldOfView;
            float originalHorizontalFOV = Camera.VerticalToHorizontalFieldOfView(originalVerticalFOV, SimulationRenderCamera.aspect);
            float desiredVerticalFOV;
            if (orientation == ScreenOrientation.Portrait || orientation == ScreenOrientation.PortraitUpsideDown)
            {
                // with tall aspect ratios, the simulation camera is rotated 90 degrees, so
                // the ar camera's vertical FOV should match the simulation camera's horizontal FOV
                desiredVerticalFOV = originalHorizontalFOV;
            }
            else
            {
                // with wide aspect ratios, the simulation camera is not rotated, but we need to limit the horizontal FOV
                // we do this by calculating the vertical FOV that corresponds to the simulation camera's horizontal FOV
                desiredVerticalFOV = Camera.HorizontalToVerticalFieldOfView(originalHorizontalFOV, LightshipSimulationEditorUtility.GetGameViewAspectRatio());

            }
            SimulationRenderCamera.fieldOfView = desiredVerticalFOV;
            projectionMatrix = SimulationRenderCamera.projectionMatrix;
            // restore the original FOV
            SimulationRenderCamera.fieldOfView = originalVerticalFOV;

            var frameEventArgs = new LightshipCameraTextureFrameEventArgs(
                timestampNs: (long)(DateTimeOffset.UtcNow.ToUnixTimeSeconds() * 1e9), projectionMatrix: projectionMatrix,
                displayMatrix: displayMatrix, intrinsics: GetCameraIntrinsics(), texture: _cameraPlane);

            FrameReceived?.Invoke(frameEventArgs);
        }

        protected virtual void OnEnable()
        {
            PreRenderCamera += OnPreRenderCamera;
            PostRenderCamera += OnPostRenderCamera;
        }

        protected virtual void OnDisable()
        {
            PreRenderCamera -= OnPreRenderCamera;
            PostRenderCamera -= OnPostRenderCamera;
        }

        internal virtual void OnDestroy()
        {
            if (SimulationRenderCamera != null)
                SimulationRenderCamera.targetTexture = null;

            if (RenderTexture != null)
                RenderTexture.Release();

            if (ProviderTexture != null)
                UnityObjectUtils.Destroy(ProviderTexture);
        }

        private XRCameraIntrinsics GetCameraIntrinsics()
        {
            return new XRCameraIntrinsics
                (
                    new Vector2(SensorFocalLength, SensorFocalLength),
                    new Vector2(SensorWidth/2f, SensorHeight/2f),
                    new Vector2Int(SimulationRenderCamera.scaledPixelWidth, SimulationRenderCamera.scaledPixelHeight)
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
            simulationCamera.sensorSize = new Vector2(SensorHeight, SensorWidth);
            var calculatedFov = Camera.FocalLengthToFieldOfView(SensorFocalLength, simulationCamera.sensorSize.y);
            simulationCamera.fieldOfView = calculatedFov;
        }

        private void DoCameraRender(Camera renderCamera)
        {
            PreRenderCamera?.Invoke(renderCamera);
            renderCamera.Render();
            PostRenderCamera?.Invoke(renderCamera);
        }

        internal abstract bool TryGetTextureDescriptors(out NativeArray<XRTextureDescriptor> planeDescriptors,
            Allocator allocator);

        internal bool TryGetLatestImagePtr(out IntPtr nativePtr)
        {
            if (_cameraPlane != null && ProviderTexture != null
                && ProviderTexture.isReadable)
            {
                nativePtr =  TexturePtr;
                return true;
            }

            nativePtr = IntPtr.Zero;
            return false;
        }

        // since we are inverting the projection matrix, we need to invert culling so normals are considered inverted.
        // this would normally be done to achieve reflection and mirroring effects
        private void OnPreRenderCamera(Camera renderCamera)
        {
            renderCamera.ResetProjectionMatrix();
            renderCamera.projectionMatrix *= Matrix4x4.Scale(new Vector3(1, -1, 1));
            GL.invertCulling = true;
        }

        private void OnPostRenderCamera(Camera renderCamera) {
            GL.invertCulling = false;
            renderCamera.ResetProjectionMatrix();
        }
    }
}
