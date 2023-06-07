using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Niantic.Lightship.AR.Subsystems
{
    public class ARLocation : MonoBehaviour
    {
#if UNITY_EDITOR
        [Tooltip("The AR Location Manifest associated with this persistent content root")] [SerializeField]
        private ARLocationManifest _arLocationManifest;
#endif

        [Tooltip("The payload for the persistent content root")] [SerializeField]
        public string _payload;

        [Tooltip("Whether or not to include the mesh in the build")] [SerializeField]
        private bool includeMeshInBuild = false;

        /// <summary>
        /// The payload associated with the ARLocation
        /// </summary>
        public ARPersistentAnchorPayload Payload => new(_payload);

        [SerializeField]
        [HideInInspector]
        private GameObject _meshContainer;

#if UNITY_EDITOR
        private ARLocationManifest _previousARLocationManifest;

        private void Awake()
        {
            _previousARLocationManifest = _arLocationManifest;
        }

        private void OnValidate()
        {
            UpdateMeshTag();
            UpdateARLocationManifest();
        }

        private void UpdateMeshTag()
        {
            if (_meshContainer == null)
            {
                return;
            }

            EditorApplication.delayCall += UpdateTagNextFrame;
        }

        private void UpdateTagNextFrame()
        {
            EditorApplication.delayCall -= UpdateTagNextFrame;
            if (_meshContainer)
            {
                _meshContainer.tag = includeMeshInBuild ? "Untagged" : "EditorOnly";
            }
        }

        private void UpdateARLocationManifest()
        {
            if (_previousARLocationManifest != _arLocationManifest)
            {
                EditorApplication.delayCall += UpdateARLocationManifestNextFrame;
                _previousARLocationManifest = _arLocationManifest;
            }

            if (_arLocationManifest != null)
            {
                if (_payload != _arLocationManifest.MeshOriginAnchorPayload)
                {
                    _payload = _arLocationManifest.MeshOriginAnchorPayload;
                }
            }
        }

        private void UpdateARLocationManifestNextFrame()
        {
            EditorApplication.delayCall -= UpdateARLocationManifestNextFrame;
            if (_meshContainer)
            {
                DestroyImmediate(_meshContainer);
            }

            if (this && _arLocationManifest)
            {
                name = _arLocationManifest.LocationName;
                _payload = _arLocationManifest.MeshOriginAnchorPayload;
                _meshContainer = Instantiate(_arLocationManifest.MockAsset, transform);
                _meshContainer.hideFlags = HideFlags.HideInHierarchy;
                UpdateMeshTag();
            }
            else
            {
                _payload = null;
                _meshContainer = null;
            }
        }
#endif
    }
}
