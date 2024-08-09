// Copyright 2022-2024 Niantic.
using Niantic.Lightship.AR.Subsystems.Playback;
using Niantic.Lightship.AR.Utilities;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Simulation;

namespace Niantic.Lightship.AR.Simulation
{
    /// <summary>
    /// Based on Unity Simulation's SimulationCamera.
    /// Takes mouse and keyboard input and uses it to compute a new camera transform.
    /// </summary>
    internal class LightshipSimulationDevice : MonoBehaviour
    {
        private static LightshipSimulationDevice s_instance;
        private bool _removedOffset = false;
        private float _lastAspectRatio = 1f;

        private static Quaternion s_cameraSensorToGameViewRotation
        {
            get
            {
                if (LightshipSimulationEditorUtility.GetGameViewAspectRatio() >= 1.0)
                {
                    return Quaternion.Euler(0, 0, 0);
                }

                return Quaternion.Euler(0, 0, -90);
            }
        }

        public Transform CameraParent { get; private set; }
        public Camera RgbCamera { get; private set; }

        private void Update()
        {
            // Workaround for https://niantic.atlassian.net/browse/ARDK-1015 to remove y-offset.
            if (!_removedOffset)
            {
                var xrOrigin = FindObjectOfType<XROrigin>();
                if (xrOrigin != null)
                {
                    xrOrigin.CameraYOffset = 0;
                    _removedOffset = true;
                }
            }

            var pose = transform.GetWorldPose();
            SendPoseToInputProvider(pose);
    }

        internal void SendPoseToInputProvider(Pose pose)
        {
            ApplyOrientationCorrection();

            Matrix4x4 poseAsMatrixOfCamera = Matrix4x4.TRS(pose.position, Quaternion.Inverse(s_cameraSensorToGameViewRotation) * pose.rotation, Vector3.one);
            Matrix4x4 poseAsMatrixOfDevice = Matrix4x4.TRS(pose.position, pose.rotation, Vector3.one);

            Vector3 position = pose.position;
            Quaternion cameraToLocal = pose.rotation;

            var displayToLocal = cameraToLocal * CameraMath.DisplayToCameraRotation(poseAsMatrixOfDevice.GetScreenOrientation());

            LightshipInputProvider.SetPose(position, displayToLocal, (uint)poseAsMatrixOfCamera.inverse.GetScreenOrientation());
        }

        private void ApplyOrientationCorrection()
        {
            // If the display aspect ratio has changed between landscape and portrait, update the camera rotation to match.
            if (CameraParent != null)
            {
                var aspectRatio = LightshipSimulationEditorUtility.GetGameViewAspectRatio();
                if (aspectRatio >= 1.0f && _lastAspectRatio < 1.0f)
                {
                    CameraParent.rotation *= Quaternion.Euler(0, 0, 90);
                }
                else if (aspectRatio < 1.0f && _lastAspectRatio >= 1.0f)
                {
                    CameraParent.rotation *= Quaternion.Euler(0, 0, -90);
                }

                _lastAspectRatio = aspectRatio;
            }
        }

        internal static LightshipSimulationDevice GetOrCreateSimulationCamera()
        {
            if (!s_instance)
            {
                SimulationCamera.GetOrCreateSimulationCamera();
                var xrSimulationCamera = GameObject.Find("SimulationCamera");

                s_instance = xrSimulationCamera.AddComponent<LightshipSimulationDevice>();

                var cameraParentGo = new GameObject("LightshipSimulationCameras");
                cameraParentGo.transform.SetParent(xrSimulationCamera.transform, false);
                cameraParentGo.transform.localRotation = s_cameraSensorToGameViewRotation;
                cameraParentGo.transform.localPosition = Vector3.zero;
                s_instance.CameraParent = cameraParentGo.transform;

                var rgbCameraGo = new GameObject("LightshipSimulationRgbCamera");
                rgbCameraGo.transform.SetParent(cameraParentGo.transform, false);
                var camera = rgbCameraGo.AddComponent<Camera>();
                camera.enabled = false;
                s_instance.RgbCamera = camera;

                s_instance._lastAspectRatio = LightshipSimulationEditorUtility.GetGameViewAspectRatio();
            }

            return s_instance;
        }
    }
}
