// Copyright 2022-2024 Niantic.
using Niantic.Lightship.AR.Utilities.Attributes;

using UnityEngine;

namespace Niantic.Lightship.AR.LocationAR
{
    [ExecuteInEditMode]
    internal class TransformFixer : MonoBehaviour
    {
        [ReadOnly] public Vector3 Position;
        [ReadOnly] public Quaternion Rotation;
        [ReadOnly] public Vector3 Scale = Vector3.one;

        [ReadOnly] public bool FixScale = true;
        [ReadOnly] public bool FixPosition = true;
        [ReadOnly] public bool FixRotation = true;

        private void Reset()
        {
            hideFlags = HideFlags.HideInInspector;
        }

        private void Update()
        {
#if UNITY_EDITOR
            if (transform.hasChanged && !Application.isPlaying)
            {
                if (FixScale)
                {
                    transform.localScale = Scale;
                }

                if (FixPosition)
                {
                    transform.localPosition = Position;
                }

                if (FixRotation)
                {
                    transform.localRotation = Rotation;
                }
            }
#endif
        }
    }
}
