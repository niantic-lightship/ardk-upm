// Copyright 2022-2024 Niantic.
using Niantic.Lightship.AR.Utilities;

namespace Niantic.Lightship.AR.XRSubsystems
{
    /// <summary>
    /// Contains debug information of XRPersistentAnchorSubsystem
    /// </summary>
    [PublicAPI]
    public struct XRPersistentAnchorDebugInfo
    {
        /// <summary>
        /// Array of XRPersistentAnchorNetworkRequestStatus
        /// </summary>
        public XRPersistentAnchorNetworkRequestStatus[] networkStatusArray { get; private set; }

        /// <summary>
        /// Array of XRPersistentAnchorLocalizationStatus
        /// </summary>
        public XRPersistentAnchorLocalizationStatus[] localizationStatusArray { get; private set; }

        /// <summary>
        /// Array of XRPersistentAnchorFrameDiagnostics
        /// </summary>
        public XRPersistentAnchorFrameDiagnostics[] frameDiagnosticsArray { get; private set; }

        /// <summary>
        /// XRPersistentAnchorDebugInfo Contructor
        /// </summary>
        /// <param name="XRPersistentAnchorDebugInfo">The XRPersistentAnchorDebugInfo with the debug data arrays</param>
        public XRPersistentAnchorDebugInfo(
            XRPersistentAnchorNetworkRequestStatus[] networkStatusArray,
            XRPersistentAnchorLocalizationStatus[] localizationStatusArray,
            XRPersistentAnchorFrameDiagnostics[] frameDiagnosticsArray)
        {
            this.networkStatusArray = networkStatusArray;
            this.localizationStatusArray = localizationStatusArray;
            this.frameDiagnosticsArray = frameDiagnosticsArray;
        }
    }
}
