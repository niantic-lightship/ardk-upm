// Copyright 2023-2024 Niantic.

using UnityEngine;
using Niantic.Lightship.AR.Utilities;
using Niantic.Lightship.AR.XRSubsystems;

namespace Niantic.Lightship.AR.WorldPositioning
{
    /// <summary>
    /// <c>ARWorldPositioningCameraHelper</c> provides information relating to the world position of a camera and implements 'Heads Down Mode' to improve user comfort during extended gameplay.
    /// </summary>
    [PublicAPI]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public class ARWorldPositioningCameraHelper : MonoBehaviour
    {
        private ARWorldPositioningManager _wpsManager;
        private Camera _camera;

        /// <summary>
        /// The heading of the camera relative to true north, measured in degrees
        /// </summary>
        public float TrueHeading { get; private set; }

        /// <summary>
        /// The latitude of the camera, measured in degrees
        /// </summary>
        public double Latitude { get; private set; }

        /// <summary>
        /// The longitude of the camera, measured in degrees
        /// </summary>
        public double Longitude { get; private set; }

        /// <summary>
        /// The altitude of the camera, measured in metres above sea level
        /// </summary>
        public double Altitude { get; private set; }

        /// <summary>
        /// The rotation from camera coordinates (Right-Up-Forwards) to global geographic
        /// coordinates (East-Up-North)
        /// </summary>
        public Quaternion RotationCameraRUFToWorldEUN { get; private set; }

        /// <summary>
        /// The camera forward vector in global geographic coordinates (East-Up-North)
        /// </summary>
        public Vector3 Forward { get; private set; }

        /// <summary>
        /// The <c>CameraControlMode</c> represents the adjustment to be applied to the AR camera
        /// before rendering
        /// </summary>
        /// <value><c>Default</c> leaves the camera position unchanged so that it matched the device trackign position</value>
        /// <value><c>HeadsDown</c> adjusts the camera view so that a downwards 45 degree tilt results in a horizontal view being rendered.  This allows the user to hold the device in a more comfortable position during extended gameplay.</value>
        public enum CameraControlMode
        {
            Default = 0,
            HeadsDown = 1
        };

        /// <summary>
        /// The <c>CameraMode</c> The selected CameraControlMode for the camera
        /// </summary>
        public CameraControlMode CameraMode { get; set; } = CameraControlMode.Default;

        public void ToggleHeadsDownMode()
        {
            if (CameraMode == CameraControlMode.HeadsDown)
            {
                CameraMode = CameraControlMode.Default;
            }
            else
            {
                CameraMode = CameraControlMode.HeadsDown;
            }
        }

        public void Awake()
        {
            _camera = gameObject.GetComponent<Camera>();
        }
        internal void SetWorldPositioningManager(ARWorldPositioningManager wpsManager)
        {
            _wpsManager = wpsManager;
        }

        public void OnDisable()
        {
            Camera.onPreCull -= PreCull;
        }

        private void PreCull(Camera cam)
        {
            if (CameraMode == CameraControlMode.HeadsDown)
                if (cam == _camera)
                {
                    Vector3 rotationAxis = Vector3.Cross(cam.transform.localRotation * Vector3.forward, Vector3.up).normalized;
                    _camera.transform.localRotation = _camera.transform.localRotation * Quaternion.AngleAxis(45.0f, Quaternion.Inverse(_camera.transform.localRotation) * rotationAxis);
                }
        }

        void OnEnable()
        {
            Camera.onPreCull += PreCull;
        }

        void Update()
        {
            if (_wpsManager.Status == WorldPositioningStatus.Available)
            {
                _wpsManager.WorldTransform.TangentialToWorld(
                    _camera.transform,
                    out var latitude,
                    out var longitude,
                    out var altitude,
                    out var rotationCameraRUFToWorldEUN);

                Latitude = latitude;
                Longitude = longitude;
                Altitude = altitude;
                RotationCameraRUFToWorldEUN = rotationCameraRUFToWorldEUN;
                Forward = rotationCameraRUFToWorldEUN * Vector3.forward;
                Vector3 rightHorizontal = rotationCameraRUFToWorldEUN * Vector3.right;
                rightHorizontal.y = 0.0f;
                rightHorizontal.Normalize();
                TrueHeading = Quaternion.FromToRotation(Vector3.right,rightHorizontal).eulerAngles.y;
            }

        }
    }

}
