using Niantic.Lightship.AR.Utilities.Attributes;

using UnityEngine;

namespace Niantic.Lightship.AR.Subsystems
{
    [ExecuteInEditMode]
    internal class _TransformFixer : MonoBehaviour
    {
        [_ReadOnly] public Vector3 Position;
        [_ReadOnly] public Quaternion Rotation;
        [_ReadOnly] public Vector3 Scale = Vector3.one;

        [_ReadOnly] public bool FixScale = true;
        [_ReadOnly] public bool FixPosition = true;
        [_ReadOnly] public bool FixRotation = true;

        private void Reset()
        {
            hideFlags = HideFlags.HideInInspector;
        }

        private void Update()
        {
#if UNITY_EDITOR
            if (transform.hasChanged)
            {
                if (FixScale)
                    transform.localScale = Scale;

                if (FixPosition)
                    transform.localPosition = Position;

                if (FixRotation)
                    transform.localRotation = Rotation;
            }
#endif
        }
    }
}
