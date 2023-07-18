#if UNITY_EDITOR
using System.Reflection;
using System.Threading.Tasks;

using Unity.XR.CoreUtils;

using UnityEngine;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace Niantic.Lightship.AR.Subsystems
{
    internal class MockARPersistentAnchorManagerImplementation : IARPersistentAnchorManagerImplementation
    {
        public MockARPersistentAnchorManagerImplementation(ARPersistentAnchorManager arPersistentAnchorManager)
        {
            InitializeMockCamera(arPersistentAnchorManager);
        }

        public bool TryTrackAnchor(ARPersistentAnchorManager arPersistentAnchorManager,
            ARPersistentAnchorPayload payload,
            out ARPersistentAnchor arPersistentAnchor)
        {
            var anchorGameObject = new GameObject("Persistent Anchor", typeof(ARPersistentAnchor));
            
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

        public void DestroyAnchor(ARPersistentAnchorManager arPersistentAnchorManager,
            ARPersistentAnchor arPersistentAnchor)
        {
            SetTrackingState(arPersistentAnchor, TrackingState.None);
            arPersistentAnchorManager.ReportRemovedAnchors(arPersistentAnchor);
            
            Object.Destroy(arPersistentAnchor.gameObject);
        }

        private async void MockLocalization(ARPersistentAnchorManager arPersistentAnchorManager,
            ARPersistentAnchor arPersistentAnchor)
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

        private void InitializeMockCamera(ARPersistentAnchorManager arPersistentAnchorManager)
        {
            var trackedPoseDriver = arPersistentAnchorManager.GetComponentInChildren<TrackedPoseDriver>();
            if (trackedPoseDriver)
            {
                trackedPoseDriver.enabled = false;
                trackedPoseDriver.gameObject.AddComponent<MockCamera>();
            }
            else
            {
                Debug.LogError($"No TrackedPoseDriver was found as a child of the ARLocationManager GameObject.",
                    arPersistentAnchorManager.gameObject);
            }
        }

        private void SetTrackingState(ARPersistentAnchor arPersistentAnchor, TrackingState trackingState)
        {
            var xrPersistentAnchor = (object)arPersistentAnchor.SessionRelativeData;
            var trackingStateFieldInfo = xrPersistentAnchor.GetType().GetField("m_TrackingState",
                BindingFlags.NonPublic |
                BindingFlags.Instance);
            trackingStateFieldInfo.SetValue(xrPersistentAnchor, trackingState);
            var sessionRelativeDataFieldInfo = typeof(ARTrackable<XRPersistentAnchor, ARPersistentAnchor>)
                .GetField("<sessionRelativeData>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
            sessionRelativeDataFieldInfo.SetValue(arPersistentAnchor, xrPersistentAnchor);
        }
    }
}
#endif
