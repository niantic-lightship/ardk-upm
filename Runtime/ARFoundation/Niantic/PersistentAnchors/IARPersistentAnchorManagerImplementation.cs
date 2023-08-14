using System;

namespace Niantic.Lightship.AR.Subsystems
{
    internal interface IARPersistentAnchorManagerImplementation : IDisposable
    {
        bool TryTrackAnchor(ARPersistentAnchorManager arPersistentAnchorManager, ARPersistentAnchorPayload payload, out ARPersistentAnchor arPersistentAnchor);
        void DestroyAnchor(ARPersistentAnchorManager arPersistentAnchorManager, ARPersistentAnchor arPersistentAnchor);
    }
}
