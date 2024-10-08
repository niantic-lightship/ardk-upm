// Copyright 2023-2024 Niantic.

using UnityEngine;
using Niantic.Lightship.AR.Utilities;

using UnityEngine.InputSystem.XR;
using Unity.XR.CoreUtils;

namespace Niantic.Lightship.AR.WorldPositioning
{
    /// <summary>
    /// <c>ARWorldPositioningEditorControls</c> can be used to simulate different locations in the Unity editor.
    /// The arrow keys and WASD can be used to move the camera position.
    /// </summary>
    [PublicAPI]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(XROrigin))]
    [RequireComponent(typeof(ARWorldPositioningManager))]
    public class ARWorldPositioningEditorControls : MonoBehaviour
    {
        private ARWorldPositioningManager _wpsManager;
        private Camera _camera;


        [Tooltip("The latitude value to use")]
        [SerializeField]
        private double _editorLatitude = 51.509915;

        [Tooltip("The longitude value to use")]
        [SerializeField]
        private double _editorLongitude = -0.1653;

        [Tooltip("The altitude value to use")]
        [SerializeField]
        private double _editorAltitude = 0.0;

        [Tooltip("The movement speed when using editor controls")]
        [SerializeField]
        private float _speed = 5.0f;

        [Tooltip("The rotation speed when using editor controls")]
        [SerializeField]
        private float _rotationSpeed = 20.0f;


        private double _previousLatitude = 0.0;
        private double _previousLongitude = 0.0;
        private double _previousAltitude = 0.0;

        private bool _previousEnable = false;

        // Only to be used in the unity editor
#if UNITY_EDITOR
        void Awake()
        {
            _wpsManager = gameObject.GetComponent<ARWorldPositioningManager>();
            _camera = gameObject.GetComponent<XROrigin>().Camera;
        }

        void OnEnable()
        {
            _previousEnable = false;
        }

        void Update()
        {
            if (this.enabled)
            {
                // Disable TrackedPoseDriver so that we don't fight with playback:
                if (_camera.GetComponent<TrackedPoseDriver>())
                    _camera.GetComponent<TrackedPoseDriver>().enabled = false;

                // Reset the transform in ARWorldPositioningManager if the chosen geographic position has changed or if this is the first frame
                if (_previousAltitude != _editorAltitude || _previousLongitude != _editorLongitude || _previousLatitude != _editorLatitude || !_previousEnable)
                {
                    _previousAltitude = _editorAltitude;
                    _previousLongitude = _editorLongitude;
                    _previousLatitude = _editorLatitude;

                    // Set the new origin:
                    _wpsManager.OverrideTransform(new ARWorldPositioningTangentialTransform(Matrix4x4.identity, _editorLatitude, _editorLongitude, _editorAltitude));

                    // Set the camera position to the new origin:
                    _camera.transform.localPosition = Vector3.zero;
                }

                // Handle keyboard input:
                if (Input.GetKey("up") || Input.GetKey("w"))
                {
                    _camera.transform.localPosition += _camera.transform.localRotation*Vector3.forward*_speed*Time.deltaTime;
                }

                if (Input.GetKey("down") || Input.GetKey("s"))
                    _camera.transform.localPosition += _camera.transform.localRotation*Vector3.back*_speed*Time.deltaTime;

                if (Input.GetKey("a"))
                    _camera.transform.localPosition += _camera.transform.localRotation*Vector3.left*_speed*Time.deltaTime;

                if (Input.GetKey("d"))
                    _camera.transform.localPosition += _camera.transform.localRotation*Vector3.right*_speed*Time.deltaTime;

                if (Input.GetKey("left"))
                    _camera.transform.localRotation = Quaternion.Euler(0.0f,-_rotationSpeed*Time.deltaTime,0.0f)*_camera.transform.localRotation;

                if (Input.GetKey("right"))
                    _camera.transform.localRotation = Quaternion.Euler(0.0f,_rotationSpeed*Time.deltaTime,0.0f)* _camera.transform.localRotation;
            }
            _previousEnable = true;
        }

        void OnDisable()
        {
            _wpsManager.EndOverride();
            if (_camera.GetComponent<TrackedPoseDriver>())
                _camera.GetComponent<TrackedPoseDriver>().enabled = true;
            _previousEnable = false;
        }

#endif
    }
}
