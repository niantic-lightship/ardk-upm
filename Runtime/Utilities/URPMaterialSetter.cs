// Copyright 2022-2024 Niantic.
using UnityEngine;

namespace Niantic.Lightship.AR.Utilities
{
    /// <summary>
    /// Controls URP related execution paths in the material's shader to prevent
    /// compilation errors when the Universal RP package is not present.
    /// </summary>
    public class URPMaterialSetter : MonoBehaviour
    {
        [SerializeField]
        private MeshRenderer _renderer;

        private const string k_UniversalRPKeyword = "LIGHTSHIP_URP";

        private void Awake()
        {
            if (_renderer == null)
            {
                _renderer = GetComponent<MeshRenderer>();
            }
        }

        private void Start()
        {
#if MODULE_URP_ENABLED
            if (!_renderer.material.IsKeywordEnabled(k_UniversalRPKeyword))
            {
                _renderer.material.EnableKeyword(k_UniversalRPKeyword);
            }
#else
            if (_renderer.material.IsKeywordEnabled(k_UniversalRPKeyword))
            {
                _renderer.material.DisableKeyword(k_UniversalRPKeyword);
            }
#endif
        }
    }
}
