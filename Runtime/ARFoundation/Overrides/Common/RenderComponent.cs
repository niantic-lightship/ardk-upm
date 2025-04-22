// Copyright 2022-2025 Niantic.

using System;
using Niantic.Lightship.AR.Utilities.Logging;
using UnityEngine;

namespace Niantic.Lightship.AR.Common
{
    /// <summary>
    /// Base class for components that modify the state of a material.
    /// </summary>
    public abstract class RenderComponent : IDisposable
    {
        /// <summary>
        /// Keyword to enable in the shader.
        /// </summary>
        protected abstract string Keyword { get; }

        // The reference to the target material.
        private Material _target;

        // Whether the target material has been set.
        private bool _targetMaterialSet;

        /// <summary>
        /// Sets the material that this component is modifying.
        /// </summary>
        public void SetTargetMaterial(Material mat)
        {
            if (_target == mat)
            {
                return;
            }

            if (_target != null)
            {
                // Revert the previous material
                OnMaterialDetach(_target);
                ToggleKeyword(false);
                _target = null;
            }

            // Try assign the new material
            _targetMaterialSet = false;
            _target = mat;

            if (_target != null)
            {
                // Prepare the new material
                ToggleKeyword(true);
                OnMaterialAttach(_target);
                _targetMaterialSet = true;
            }
        }

        /// <summary>
        /// Performs render feature related logic and updates the target material.
        /// </summary>
        /// <param name="camera">The camera that is rendering the material.</param>
        /// <param name="isRendering">Whether the material is being rendered.</param>
        public void Update(Camera camera, bool isRendering)
        {
            OnUpdate(camera);

            if (isRendering)
            {
                if (_targetMaterialSet)
                {
                    OnMaterialUpdate(_target);
                }
                else
                {
                    Log.Warning("RenderComponent with type " + GetType().Name +
                        " tried to update but it is not attached to a material.");
                }
            }
        }

        /// <summary>
        /// Retrieves a texture from the target material.
        /// </summary>
        protected Texture GetTexture(int propertyId)
        {
            return _target == null || !_target.HasTexture(propertyId) ? null : _target.GetTexture(propertyId);
        }

        /// <summary>
        /// Retrieves a matrix from the target material.
        /// </summary>
        protected Matrix4x4? GetMatrix(int propertyId)
        {
            return _target == null || !_target.HasMatrix(propertyId) ? null : _target.GetMatrix(propertyId);
        }

        /// <summary>
        /// Perform custom logic during Update.
        /// <param name="camera">The camera that is rendering the material.</param>
        /// </summary>
        protected virtual void OnUpdate(Camera camera) { }

        /// <summary>
        /// Invoked when the render component is initialized with a new material.
        /// </summary>
        protected virtual void OnMaterialAttach(Material mat) { }

        /// <summary>
        /// Invoked when the render component needs to update the target material.
        /// </summary>
        protected virtual void OnMaterialUpdate(Material mat) { }

        /// <summary>
        /// Invoked when the render component is removed from the material.
        /// </summary>
        protected virtual void OnMaterialDetach(Material mat) { }

        /// <summary>
        /// Invoked when the component is being disposed.
        /// </summary>
        protected virtual void OnReleaseResources() { }

        /// <summary>
        /// Toggles the keyword on the target material.
        /// </summary>
        /// <param name="enable">Whether the keyword should be enabled.</param>
        private void ToggleKeyword(bool enable)
        {
            if (_target == null)
            {
                return;
            }

            if (!string.IsNullOrEmpty(Keyword) && _target.IsKeywordEnabled(Keyword) != enable)
            {
                if (enable)
                {
                    _target.EnableKeyword(Keyword);
                }
                else
                {
                    _target.DisableKeyword(Keyword);
                }
            }
        }

        public void Dispose()
        {
            SetTargetMaterial(null);
            OnReleaseResources();
            GC.SuppressFinalize(this);
        }
    }
}
