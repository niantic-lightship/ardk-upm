// Copyright 2022-2023 Niantic.
using System;

namespace Niantic.Lightship.AR.PersistentAnchors
{
    internal interface IARPersistentAnchorManagerImplementation : IDisposable
    {
        bool TryTrackAnchor(ARPersistentAnchorManager arPersistentAnchorManager, ARPersistentAnchorPayload payload, out ARPersistentAnchor arPersistentAnchor);
        void DestroyAnchor(ARPersistentAnchorManager arPersistentAnchorManager, ARPersistentAnchor arPersistentAnchor);
        bool GetVpsSessionId(ARPersistentAnchorManager arPersistentAnchorManager, out string vpsSessionId);
    }
}
