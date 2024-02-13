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

#if NIANTIC_ARDK_EXPERIMENTAL_FEATURES
        /// <summary>
        /// Array of XRPersistentAnchorFrameDiagnostics
        /// </summary>
        public XRPersistentAnchorFrameDiagnostics[] frameDiagnosticsArray { get; private set; }
#endif

        /// <summary>
        /// XRPersistentAnchorDebugInfo Contructor
        /// </summary>
        /// <param name="XRPersistentAnchorDebugInfo">The XRPersistentAnchorDebugInfo with the debug data arrays</param>
        public XRPersistentAnchorDebugInfo(
            XRPersistentAnchorNetworkRequestStatus[] networkStatusArray,
#if NIANTIC_ARDK_EXPERIMENTAL_FEATURES
            XRPersistentAnchorLocalizationStatus[] localizationStatusArray,
            XRPersistentAnchorFrameDiagnostics[] frameDiagnosticsArray)
#else
            XRPersistentAnchorLocalizationStatus[] localizationStatusArray)
#endif
        {
            this.networkStatusArray = networkStatusArray;
            this.localizationStatusArray = localizationStatusArray;
#if NIANTIC_ARDK_EXPERIMENTAL_FEATURES
            this.frameDiagnosticsArray = frameDiagnosticsArray;
#endif

        }
    }
}
