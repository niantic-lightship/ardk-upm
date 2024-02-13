// Copyright 2022-2024 Niantic.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;

using Niantic.Lightship.AR.Utilities.Logging;
using Niantic.Lightship.AR.LocationAR;
using Niantic.Lightship.AR.XRSubsystems;

using Unity.XR.CoreUtils;

using UnityEngine;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

using Object = UnityEngine.Object;

namespace Niantic.Lightship.AR.PersistentAnchors
{
    internal class MockARPersistentAnchorManagerImplementation : IDisposable
    {
        private List<Behaviour> disabledComponents = new List<Behaviour>();
        private const string PersistentAnchorGameObjectName = "Persistent Anchor";

        public MockARPersistentAnchorManagerImplementation(ARPersistentAnchorManager arPersistentAnchorManager)
        {
            InitializeMockCamera(arPersistentAnchorManager);
        }

#if UNITY_EDITOR
        private MockCamera mockCamera;
#endif

        private void InitializeMockCamera(ARPersistentAnchorManager arPersistentAnchorManager)
        {
#if UNITY_EDITOR
            var trackedPoseDriver = arPersistentAnchorManager.GetComponentInChildren<TrackedPoseDriver>();

            if (trackedPoseDriver && trackedPoseDriver.enabled)
            {
                trackedPoseDriver.enabled = false;
                disabledComponents.Add(trackedPoseDriver);
            }

#pragma warning disable 0618
            // ARPoseDriver is deprecated but is currently the official workaround for tracking drift
            var arPoseDriver = arPersistentAnchorManager.GetComponentInChildren<ARPoseDriver>();
#pragma warning restore 0618
            if (arPoseDriver && arPoseDriver.enabled)
            {
                arPoseDriver.enabled = false;
                disabledComponents.Add(arPoseDriver);
            }

            var camera = arPersistentAnchorManager.GetComponentInChildren<Camera>();
            if (!camera)
            {
                Log.Error
                (
                    "No Camera found as a child of the ARPersistentAnchorManager, cannot use Mock"
                );

                return;
            }

            mockCamera = camera.gameObject.AddComponent<MockCamera>();
#endif
        }

        public bool TryTrackAnchor
        (
            ARPersistentAnchorManager arPersistentAnchorManager,
            ARPersistentAnchorPayload payload,
            out ARPersistentAnchor arPersistentAnchor
        )
        {
            GameObject anchorGameObject;
            if (arPersistentAnchorManager.DefaultAnchorPrefab)
            {
                anchorGameObject = Object.Instantiate
                    (arPersistentAnchorManager.DefaultAnchorPrefab);

                anchorGameObject.name = PersistentAnchorGameObjectName;
                if (anchorGameObject.GetComponent<ARPersistentAnchor>() == null)
                {
                    anchorGameObject.AddComponent<ARPersistentAnchor>();
                }
            }
            else
            {
                anchorGameObject = new GameObject
                    (PersistentAnchorGameObjectName, typeof(ARPersistentAnchor));
            }

            // If we can grab the TrackablesParent, use that, otherwise just child to the persistent anchor manager
            var xrOrigin = arPersistentAnchorManager.GetComponent<XROrigin>();
            if (xrOrigin != null && xrOrigin.TrackablesParent)
            {
                anchorGameObject.transform.SetParent(xrOrigin.TrackablesParent.transform, false);
            }
            else
            {
                anchorGameObject.transform.SetParent(arPersistentAnchorManager.transform, false);
            }

            arPersistentAnchor = anchorGameObject.GetComponent<ARPersistentAnchor>();
            MockLocalization(arPersistentAnchorManager, arPersistentAnchor);
            return true;
        }

        public void DestroyAnchor
        (
            ARPersistentAnchorManager arPersistentAnchorManager,
            ARPersistentAnchor arPersistentAnchor
        )
        {
            SetTrackingState(arPersistentAnchor, TrackingState.None);
            arPersistentAnchorManager.ReportRemovedAnchors(arPersistentAnchor);

            Object.Destroy(arPersistentAnchor.gameObject);
        }

        public bool GetVpsSessionId(out string vpsSessionId)
        {
            // Invalid vps session id because no real system is running
            vpsSessionId = default;
            return false;
        }

        public void Dispose()
        {
#if UNITY_EDITOR
            if (mockCamera)
            {
                Object.Destroy(mockCamera);
            }
#endif

            foreach (var component in disabledComponents)
            {
                component.enabled = true;
            }
        }

        private async void MockLocalization
        (
            ARPersistentAnchorManager arPersistentAnchorManager,
            ARPersistentAnchor arPersistentAnchor
        )
        {
            const int addAnchorMockDuration = 1000;
            const int trackAnchorMockDuration = 1000;

            SetTrackingState(arPersistentAnchor, TrackingState.None);
            await Task.Delay(addAnchorMockDuration);
            arPersistentAnchorManager.ReportAddedAnchors(arPersistentAnchor);
            await Task.Delay(trackAnchorMockDuration);
            SetTrackingState(arPersistentAnchor, TrackingState.Tracking);
            arPersistentAnchorManager.ReportUpdatedAnchors(arPersistentAnchor);
            //TODO: Mock tracking lost
        }

        private void SetTrackingState
        (
            ARPersistentAnchor arPersistentAnchor,
            TrackingState trackingState
        )
        {
            var xrPersistentAnchor = (object)arPersistentAnchor.SessionRelativeData;
            var trackingStateFieldInfo = xrPersistentAnchor.GetType()
                .GetField
                (
                    "m_TrackingState",
                    BindingFlags.NonPublic |
                    BindingFlags.Instance
                );

            trackingStateFieldInfo.SetValue(xrPersistentAnchor, trackingState);
            var sessionRelativeDataFieldInfo =
                typeof(ARTrackable<XRPersistentAnchor, ARPersistentAnchor>)
                    .GetField
                    (
                        "<sessionRelativeData>k__BackingField",
                        BindingFlags.Instance | BindingFlags.NonPublic
                    );

            sessionRelativeDataFieldInfo.SetValue(arPersistentAnchor, xrPersistentAnchor);
        }
    }
}
