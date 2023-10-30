// Copyright 2023 Niantic, Inc. All Rights Reserved.
using Niantic.Lightship.AR.Utilities;

namespace Niantic.Lightship.AR.PersistentAnchors
{
    /// <summary>
    /// Contains information about an ARPersistentAnchor that has changed state.
    /// </summary>
    [PublicAPI]
    public struct ARPersistentAnchorStateChangedEventArgs
    {
        /// <summary>
        /// The ARPersistentAnchor that has changed state
        /// </summary>
        public ARPersistentAnchor arPersistentAnchor { get; private set; }

        /// <summary>
        /// Creates the args for state changes on ARPersistentAnchor
        /// </summary>
        /// <param name="arPersistentAnchor">The ARPersistentAnchor with the state change</param>
        public ARPersistentAnchorStateChangedEventArgs(ARPersistentAnchor arPersistentAnchor)
        {
            this.arPersistentAnchor = arPersistentAnchor;
        }
    }
}
