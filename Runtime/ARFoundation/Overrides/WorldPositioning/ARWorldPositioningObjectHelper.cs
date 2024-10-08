// Copyright 2023-2024 Niantic.

using System.Collections.Generic;
using System;
using UnityEngine;
using Niantic.Lightship.AR.Utilities;
using Niantic.Lightship.AR.XRSubsystems;

using Unity.XR.CoreUtils;

namespace Niantic.Lightship.AR.WorldPositioning
{
    /// <summary>
    /// <c>ARWorldPositioningObjectHelper</c> provides a simple way to position objects using geographic
    /// coordinates.  When an object is added it will be automatically updated as the accuracy of
    /// the WPS data improves.
    /// </summary>
    [PublicAPI]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(XROrigin))]
    [RequireComponent(typeof(ARWorldPositioningManager))]
    public class ARWorldPositioningObjectHelper : MonoBehaviour
    {
        private XROrigin _sessionOrigin;
        private Camera _arCamera;
        private ARWorldPositioningManager _wpsManager;

        /// <summary>
        /// The altitude above sea level is often inaccurate and the floor height unknown.  The
        /// AltitudeMode provides the option to use a different system for representing
        /// altitude/height.
        /// </summary>
        /// <value><c>SEA_LEVEL</c>The standard WGS84 altitude above sea-level.  This option is only recommended for use when working with map data where the altitude is known.</value>
        /// <value><c>TRACKING</c>Uses the y coordinate of the AR tracking coordinate system.  This is recommended if content is generated using runtime estimates of the ground position, which will usually be in AR tracking coordinates.</value>
        /// <value><c>CAMERA</c>Positions content relative the height of the camera.  This can be used for navigation arrows or similar visual indicators.</value>
        /// <value><c>CAMERA_AVERAGED</c>Similar to CAMERA but uses the average camera height to provide a stable relative position while behaving intuitevely if the user moves the device up and down momentarily.  This method is recommended for most applications.  </value>
        public enum AltitudeMode
        {
            [InspectorName("Metres above sea level (WGS84)")]
            SEA_LEVEL = 0,
            [InspectorName("Height in XROrigin coordinates")]

            TRACKING = 1,
            [InspectorName("Camera-relative")]
            CAMERA = 2,
            [InspectorName("Camera-relative with smart averaging")]
            CAMERA_AVERAGED = 3
        };


        [Tooltip("The altitude mode to use")]
        [SerializeField]
        private AltitudeMode _altitudeMode = AltitudeMode.SEA_LEVEL;

        [Tooltip("An additional altitude offset")]
        [SerializeField]
        public float _altitudeOffset = 0.0f;

        private bool _firstAveraged = true;
        private float _averageCameraY = 0.0f;
        private Vector3 _previousCameraPosition = new();

        private float MOTION_HALF_LIFE_METRES = 5.0f;

        /// <summary>
        /// Adds an object using a world geographic position, or updates the position if it has
        /// already been added through a previous call to this method.  Updating is only rquired for
        /// dynamic objects where the world position needs to change.  The object will be added to
        /// the scene and placed at the corresponding location in the unity scene.  If the WPS data
        /// becomes more accurate, the object position will be automatically updated.
        /// </summary>
        /// <param name="gameObject">The GameObject to add</param>
        /// <param name="latitude">The latitude where the object should be placed</param>
        /// <param name="longitude">The longitude where the object should be place</param>
        /// <param name="altitude">The altitude where the object should be place</param>
        /// <param name="rotationXYZToEUN">The rotation from object coordinates to world East-Up-North coordinates</param>
        public void AddOrUpdateObject(GameObject gameObject, double latitude, double longitude, double altitude, Quaternion rotationXYZToEUN)
        {
            gameObject.transform.SetParent(_rootObject.transform);
            gameObject.transform.localRotation = rotationXYZToEUN;

            _addedObjects[gameObject] = new WorldPosition
            {
                latitude = latitude,
                longitude = longitude,
                altitude = altitude,
                rotation = rotationXYZToEUN
            };

            if (_placementTransform != null)
                UpdateObjectPosition(gameObject, _addedObjects[gameObject]);
        }

        /// <summary>
        /// Removes the object from the scene and stops updating the position based on WPS data.
        /// This should only be called for objects which were previously added through a call to
        /// AddOrUpdateObject.
        /// </summary>
        /// <param name="gameObject">The object to remove and stop updating</param>
        public void RemoveObject(GameObject gameObject)
        {
            _addedObjects.Remove(gameObject);
            gameObject.transform.SetParent(null);
        }

        /// <summary>
        /// Removes all objects from the scene which were previously added through a call to
        /// AddOrUpdateObject.  The positions will no longer be updated using WPS data.
        /// </summary>
        public void RemoveAllObjects()
        {
            foreach (GameObject gameObject in _addedObjects.Keys)
            {
                gameObject.transform.SetParent(null);
            }
            _addedObjects.Clear();
        }

        private GameObject _rootObject;

        void Awake()
        {
            _rootObject = new("World Positioning System Root Object");
            _rootObject.SetActive(false); // Hide until we know the user's location
            _wpsManager = gameObject.GetComponent<ARWorldPositioningManager>();
            _sessionOrigin = gameObject.GetComponent<XROrigin>();
            _arCamera = _sessionOrigin.Camera;
            _firstAveraged = true;
        }

        private void UpdateObjectPosition(GameObject gameObject, WorldPosition worldPosition)
        {
            Vector3 tangentialPosition;
            Quaternion tangentialRotation;
            _placementTransform.WorldToTangential(worldPosition.latitude, worldPosition.longitude, worldPosition.altitude, worldPosition.rotation, out tangentialPosition, out tangentialRotation);
            if (_altitudeMode != AltitudeMode.SEA_LEVEL)
                tangentialPosition[1] = (float)worldPosition.altitude;
            tangentialPosition[1] += _altitudeOffset;
            gameObject.transform.localPosition = tangentialPosition;
            gameObject.transform.localRotation = tangentialRotation;
        }

        void Update()
        {
            if (_wpsManager.Status == WorldPositioningStatus.Available)
            {
                // There is a valid transform - make objects visible if not already:
                _rootObject.SetActive(true);

                ARWorldPositioningTangentialTransform wpsTransform = _wpsManager.WorldTransform;

                // If the origin position has changed then we have a different tangential coordinate
                // system.  Update the object positions relative to the root object accordingly.
                if ((_placementTransform == null) ||
                    wpsTransform.OriginLatitude != _placementTransform.OriginLatitude ||
                    wpsTransform.OriginLongitude != _placementTransform.OriginLongitude ||
                    wpsTransform.OriginAltitude != _placementTransform.OriginAltitude ||
                    _altitudeOffset != _lastAltitudeOffset)
                {
                    _lastAltitudeOffset = _altitudeOffset;

                    _placementTransform = new ARWorldPositioningTangentialTransform(wpsTransform.TangentialToEUN, wpsTransform.OriginLatitude, wpsTransform.OriginLongitude, wpsTransform.OriginAltitude);

                    foreach (KeyValuePair<GameObject, WorldPosition> objectPosition in _addedObjects)
                    {
                        UpdateObjectPosition(objectPosition.Key, objectPosition.Value);
                    }

                    _rootObject.transform.SetParent(_sessionOrigin.TrackablesParent);
                }

                // Adjust the root position to put objects in the correct place:
                // All objects get converted to tangential coordinates using _placementTransform
                // We now transform the root object to apply the different between the latest transform
                // (wpsTransform) and the one we used for the conversion.
                // This is to avoid updating each object transform on every frame which could get slow with many objects
                Matrix4x4 placementToCurrent = wpsTransform.TangentialToEUN.inverse * _placementTransform.TangentialToEUN;
                Quaternion rootRotation = placementToCurrent.rotation;
                Vector3 rootPosition = new Vector3(placementToCurrent[0, 3], placementToCurrent[1, 3], placementToCurrent[2, 3]); ;

                switch (_altitudeMode)
                {
                    case AltitudeMode.SEA_LEVEL:
                        // Already the correct value
                        break;
                    case AltitudeMode.TRACKING:
                        rootPosition[1] = 0.0f;
                        break;
                    case AltitudeMode.CAMERA:
                        rootPosition[1] = _arCamera.transform.localPosition[1];
                        break;
                    case AltitudeMode.CAMERA_AVERAGED:
                        if (_firstAveraged)
                        {
                            // Initialise with the current height:
                            _averageCameraY = _arCamera.transform.localPosition[1];
                            _firstAveraged = false;
                        }
                        else
                        {
                            // Not first measurement - update ground height estimate:
                            float xDiff = _arCamera.transform.localPosition[0] - _previousCameraPosition[0];
                            float zDiff = _arCamera.transform.localPosition[2] - _previousCameraPosition[2];
                            float horizontalDistance = MathF.Sqrt(xDiff * xDiff + zDiff * zDiff);
                            float halfLifeFraction = horizontalDistance / MOTION_HALF_LIFE_METRES;
                            float originalFraction = MathF.Pow(0.5f, halfLifeFraction);
                            _averageCameraY = _averageCameraY * originalFraction + (1.0f - originalFraction) * _arCamera.transform.localPosition[1];
                        }
                        _previousCameraPosition = _arCamera.transform.localPosition;
                        rootPosition[1] = _averageCameraY;
                        break;
                }

                _rootObject.transform.localPosition = rootPosition;
                _rootObject.transform.localRotation = rootRotation;
            }
        }

        private Dictionary<GameObject, WorldPosition> _addedObjects = new();
        private ARWorldPositioningTangentialTransform _placementTransform = null;
        private float _lastAltitudeOffset = 0.0f;

        private class WorldPosition
        {
            internal double latitude = 0.0;
            internal double longitude = 0.0;
            internal double altitude = 0.0;
            internal Quaternion rotation = Quaternion.identity;
        }
    }

}
