// Copyright 2022-2024 Niantic.

using System;
using Niantic.Lightship.AR.Utilities.Logging;
using UnityEngine;

namespace Niantic.Lightship.AR.Common
{
    /// <summary>
    /// Base class for components that modify the state of a material.
    /// </summary>
    internal abstract class RenderComponent : IDisposable
    {
        /// <summary>
        /// Keyword to enable in the shader.
        /// </summary>
        protected abstract string Keyword { get; }

        // The reference to the target material.
        private Material _target;

        /// <summary>
        /// Sets the material that this component is modifying.
        /// </summary>
        public void SetTargetMaterial(Material mat)
        {
            if (_target == mat)
            {
                return;
            }

            // Disable the keyword on the previous material
            ToggleKeyword(false);

            // Assign the new material
            _target = mat;
            if (_target == null)
            {
                Log.Error(Keyword + ": Target material set to null.");
                return;
            }

            // Enable the keyword on the new material
            ToggleKeyword(true);
            OnMaterialAttach(_target);
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
                OnMaterialUpdate(_target);
            }
        }

        /// <summary>
        /// Retrieves a texture from the target material.
        /// </summary>
        protected Texture GetTexture(int propertyId)
        {
            return _target == null ? null : _target.GetTexture(propertyId);
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
            if (_target != null)
            {
                ToggleKeyword(false);
                OnMaterialDetach(_target);
                _target = null;
            }

            OnReleaseResources();
            GC.SuppressFinalize(this);
        }
    }
}
