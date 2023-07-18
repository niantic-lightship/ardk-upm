using Niantic.Lightship.AR.Utilities;
using UnityEngine;

namespace Niantic.Lightship.AR.Subsystems
{
    /// <summary>
    /// The ARLocation is the digital twin of the physical location
    /// </summary>
    [PublicAPI]
    public class ARLocation : MonoBehaviour
    {
        /// <summary>
        /// The payload associated with the ARLocation
        /// </summary>
        public ARPersistentAnchorPayload Payload;

        [SerializeField]
        internal GameObject MeshContainer;

        [SerializeField]
        internal bool IncludeMeshInBuild;

#if UNITY_EDITOR
        [SerializeField]
        internal ARLocationManifest ARLocationManifest;
#endif
    }
}
