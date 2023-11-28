// Copyright 2022-2023 Niantic.
using System;
using Niantic.Lightship.AR.Utilities.Log;
using Niantic.Lightship.AR.Subsystems;
using Niantic.Lightship.AR.XRSubsystems;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace Niantic.Lightship.AR.PersistentAnchors
{
    internal class ARPersistentAnchorManagerImplementation : IARPersistentAnchorManagerImplementation
    {
        public ARPersistentAnchorManagerImplementation(ARPersistentAnchorManager arPersistentAnchorManager)
        {

        }

        public bool TryTrackAnchor(ARPersistentAnchorManager arPersistentAnchorManager,
            ARPersistentAnchorPayload payload,
            out ARPersistentAnchor arPersistentAnchor)
        {
            if (arPersistentAnchorManager.subsystem == null || !arPersistentAnchorManager.subsystem.running)
            {
                arPersistentAnchor = default;
                return false;
            }
            var data = payload.Data;
            var dataNativeArray = new NativeArray<byte>(data, Allocator.Temp);
            IntPtr payloadIntPtr;
            unsafe
            {
                payloadIntPtr = (IntPtr)dataNativeArray.GetUnsafeReadOnlyPtr();
            }

            int payloadSize = payload.Data.Length;
            var xrPersistentAnchorPayload = new XRPersistentAnchorPayload(payloadIntPtr, payloadSize);
            bool success = arPersistentAnchorManager.subsystem.TryLocalize(xrPersistentAnchorPayload, out var xrPersistentAnchor);
            if (success)
            {
                arPersistentAnchor = arPersistentAnchorManager.CreateTrackableImmediate(xrPersistentAnchor);
            }
            else
            {
                Log.Error("Failed to localize." + arPersistentAnchorManager.gameObject);
                arPersistentAnchor = default;
            }

            return success;
        }

        public void DestroyAnchor(ARPersistentAnchorManager arPersistentAnchorManager, ARPersistentAnchor arPersistentAnchor)
        {
            if (arPersistentAnchorManager.subsystem == null)
            {
                return;
            }
            var trackableId = arPersistentAnchor.trackableId;
            bool success = arPersistentAnchorManager.subsystem.TryRemoveAnchor(trackableId);
            if (arPersistentAnchorManager.PendingAdds.ContainsKey(trackableId))
            {
                arPersistentAnchorManager.PendingAdds.Remove(trackableId);
            }
            if (success)
            {
                if (arPersistentAnchor._markedForDestruction)
                {
                    arPersistentAnchorManager.Trackables.Remove(trackableId);
                    arPersistentAnchorManager.ReportRemovedAnchors(arPersistentAnchor);
                }
                else
                {
                    arPersistentAnchor._markedForDestruction = true;
                }
            }
            else if(arPersistentAnchorManager.subsystem.running)
            {
                Log.Error($"Failed to destroy anchor {trackableId}." + arPersistentAnchorManager.gameObject);
            }
        }

        public bool GetVpsSessionId(ARPersistentAnchorManager arPersistentAnchorManager, out string vpsSessionId)
        {
            return arPersistentAnchorManager.subsystem.GetVpsSessionId(out vpsSessionId);
        }

        public void Dispose()
        {
            // Do nothing
        }
    }
}
