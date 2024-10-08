// Copyright 2023-2024 Niantic.

using Niantic.Lightship.AR.Utilities;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using System;

using Niantic.Lightship.AR.XRSubsystems;

using Unity.XR.CoreUtils;

namespace Niantic.Lightship.AR.WorldPositioning
{
    /// <summary>
    /// The <c>ARWorldPositioningManager</c> class controls the <c>XRWorldPositioningSubsystem</c> and provides access to the
    /// underlying AR to world transform from the World Positioning System (WPS).
    /// </summary>
    /// <remarks>
    /// It is unlikely that an application will need to use the value of WorldTransform directly.
    /// For applications requiring only a more accurate/stable version of GPS & compass, the properties on
    /// DefaultCameraHelper can be accessed to obtain Latitude, Longitude and Heading values which work
    /// similarly to those available through location services.
    ///
    /// The transform can also be used to place objects into the AR View using geographic coordinates.
    /// WorldPositioningPositioningHelper provides a more convenient interface for adding objects to the scene and
    /// updating their positions as WPS data becomes more accurate.
    /// </remarks>
    [PublicAPI]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(XROrigin))]
    public class ARWorldPositioningManager :
        SubsystemLifecycleManager<XRWorldPositioningSubsystem, XRWorldPositioningSubsystemDescriptor, XRWorldPositioningSubsystem.Provider>
    {
        /// <summary>
        /// The current estimate of the transform between the AR tracking coordinates and the world
        /// geographic coordinate system.  This property should only be used when the Status
        /// property is <c>Available</c>, otherwise it is undefined.
        /// </summary>
        public ARWorldPositioningTangentialTransform WorldTransform { get; internal set; } = new ARWorldPositioningTangentialTransform();

        private WorldPositioningStatus _status = WorldPositioningStatus.NoGnss;

        /// <summary>
        /// The Status of the WorldTransform estimate.  The WorldTransform is only valid when Status is <c>Available</c>.
        /// </summary>
        public WorldPositioningStatus Status
        {
            get => _status;
            internal set
            {
                if (value != _status)
                {
                    _status = value;
                    OnStatusChanged?.Invoke(_status);
                }
            }
        }


        /// <summary>
        /// Returns true if World Positioning is available.
        /// </summary>
        public bool IsAvailable => Status == WorldPositioningStatus.Available;

        /// <summary>
        /// Action that is called when the status changes
        /// </summary>
        public Action<WorldPositioningStatus> OnStatusChanged;

        /// <summary>
        /// A ARWorldPositioningCameraHelper which is automatically generated for the default AR camera
        /// </summary>
        public ARWorldPositioningCameraHelper DefaultCameraHelper { get; internal set; }

        private bool _simulationMode = false;

        void Awake()
        {
            DefaultCameraHelper = gameObject.GetComponent<XROrigin>().Camera.gameObject.AddComponent<ARWorldPositioningCameraHelper>();
            DefaultCameraHelper.SetWorldPositioningManager(this);
        }
        protected override void OnDisable()
        {
            base.OnDisable();

            Status = WorldPositioningStatus.SubsystemNotRunning;
        }

        public void Update()
        {
            // Update the transform:
            Status = TryGetXRToWorld(ref WorldTransform.TangentialToEUN, ref WorldTransform.OriginLatitude, ref WorldTransform.OriginLongitude, ref WorldTransform.OriginAltitude);
        }

        public WorldPositioningStatus TryGetXRToWorld(ref Matrix4x4 arToWorld, ref double originLatitude, ref double originLongitude, ref double originAltitude)
        {
            if (_simulationMode)
            {
                return Status;
            }

            if (subsystem == null)
            {
                return WorldPositioningStatus.SubsystemNotRunning;
            }

            return subsystem.TryGetXRToWorld(ref arToWorld, ref originLatitude, ref originLongitude, ref originAltitude);
        }


        /// <summary>
        /// Overrides the World Positioning transform with the value specified.  This method allows the developer
        /// to simulate different locations.  The ARWorldPositioningEditorControls class can be used to simulate
        /// different locations in the Unity editor.
        /// </summary>
        public void OverrideTransform(ARWorldPositioningTangentialTransform simulatedTransform)
        {
            _simulationMode = true;
            WorldTransform = simulatedTransform;
            Status = WorldPositioningStatus.Available;
        }

        /// <summary>
        /// Stops overriding the World Positioning transform.  Use this to switch back to the real transform (on a
        /// device) or playback (in the Unity editor)
        /// </summary>
        public void EndOverride()
        {
            _simulationMode = false;
            Status = WorldPositioningStatus.Initializing;
        }
    }
}
