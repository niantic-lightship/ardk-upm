// Copyright 2022-2024 Niantic.

using System;
using System.Linq;
using Niantic.Lightship.AR.Utilities.Logging;
using UnityEngine;
using UnityEngine.Rendering;

namespace Niantic.Lightship.AR.Common
{
    /// <summary>
    /// Base class for rendering components that attach command buffers to the camera.
    /// This component allows precise scheduling of rendering commands.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public abstract class ConditionalRenderer : MonoBehaviour
    {
        #region Properties

        /// <summary>
        /// The Unity camera component.
        /// </summary>
        protected Camera Camera
        {
            get { return _camera ??= GetComponent<Camera>(); }
        }

        /// <summary>
        /// The material currently used in rendering.
        /// </summary>
        public Material Material
        {
            get
            {
                // Allocate the material lazily
                if (!_didOverrideMaterial && _material == null)
                {
                    // Find the default shader
                    var shader = Shader.Find(ShaderName);
                    if (shader == null)
                    {
                        throw new InvalidOperationException($"Could not find shader named '{ShaderName}'.");
                    }

                    // Create the material
                    _material = new Material(shader);

                    // Initialize the material with defaults
                    OnInitializeMaterial(_material);
                }

                return _material;
            }

            private set
            {
                _material = value;
                if (_material != null)
                {
                    // Initialize the material with defaults
                    OnInitializeMaterial(_material);
                }
            }
        }

        /// <summary>
        /// The camera events that the command buffer should be attached to.
        /// </summary>
        protected virtual CameraEvent[] CameraEvents => new[]
        {
            CameraEvent.BeforeForwardOpaque,
            CameraEvent.BeforeGBuffer
        };

        /// <summary>
        /// Custom logic to determine whether the command buffer should be attached.
        /// </summary>
        protected virtual bool ShouldAddCommandBuffer
        {
            get => true;
        }

        /// <summary>
        /// Whether the command buffer is attached to the Unity camera.
        /// </summary>
        protected bool IsCommandBufferAdded { get; private set; }

        /// <summary>
        /// Whether the application is using the default render pipeline.
        /// </summary>
        protected static bool IsUsingLegacyRenderPipeline => GraphicsSettings.currentRenderPipeline == null;

        /// <summary>
        /// The name of the shader used by the rendering material.
        /// </summary>
        protected abstract string ShaderName { get; }

        /// <summary>
        /// The name of this renderer.
        /// </summary>
        protected abstract string RendererName { get; }

        #endregion

        // Components
        private Camera _camera;

        // Resources
        private Material _material;
        private CommandBuffer _commandBuffer;

        // State
        private bool _didOverrideMaterial;
        private bool _isCommandBufferDirty = true;
        private int _numberOfARFramesSinceStart;
        private bool _silenceCommandBufferWarnings;

        /// <summary>
        /// Invoked when it is time to add rendering commands to the command buffer.
        /// </summary>
        /// <param name="cmd">The command buffer resource.</param>
        /// <param name="mat">The material that should be used to draw the frame.</param>
        /// <returns>Whether the command buffer could be successfully configured.</returns>
        protected abstract bool OnAddRenderCommands(CommandBuffer cmd, Material mat);

        /// <summary>
        /// Invoked when the material state needs to be reset or initialized with
        /// default values. This usually occurs when the material is first created.
        /// </summary>
        /// <param name="mat"></param>
        protected virtual void OnInitializeMaterial(Material mat) { }

        /// <summary>
        /// Invoked to query the external command buffers that need to run before our own.
        /// </summary>
        /// <param name="evt">The camera event to search command buffers for.</param>
        /// <returns>Names or partial names of the command buffers.</returns>
        protected virtual string[] OnRequestExternalPassDependencies(CameraEvent evt)
        {
            return null;
        }

        protected virtual void Awake()
        {
            // Create the command buffer
            _commandBuffer = new CommandBuffer {name = RendererName};
        }

        protected virtual void OnDestroy()
        {
            // Dispose the command buffer
            _commandBuffer?.Dispose();

            // Destroy the material if it was created by this component
            if (!_didOverrideMaterial && _material != null)
            {
                Destroy(_material);
            }
        }

        protected virtual void OnDisable()
        {
            // Disable rendering
            ToggleCommandBuffer(false);
        }

        protected virtual void Update()
        {
            // Check if the command buffer needs to be reconfigured
            if (_isCommandBufferDirty)
            {
                // Attempt to add the rendering commands
                _commandBuffer.Clear();
                var success = OnAddRenderCommands(_commandBuffer, Material);

                // If the command buffer was not successfully configured,
                // mark it as dirty .Adding commands may fail if some resource
                // is not ready (e.g. a texture is not yet available).
                _isCommandBufferDirty = !success;
            }

            // Handle command buffer behavior
            var shouldAddCommandBuffer = gameObject.activeInHierarchy && enabled && ShouldAddCommandBuffer;
            if (!IsCommandBufferAdded)
            {
                // Attach?
                if (shouldAddCommandBuffer)
                {
                    // Verify dependencies
                    if (VerifyExternalPasses(CameraEvent.BeforeForwardOpaque) &&
                        VerifyExternalPasses(CameraEvent.BeforeGBuffer))
                    {
                        ToggleCommandBuffer(true);
                    }
                }
            }
            else
            {
                // Detach?
                if (!shouldAddCommandBuffer)
                {
                    ToggleCommandBuffer(false);
                }
            }
        }

        /// <summary>
        /// Verifies that the required command buffers are present on the camera.
        /// </summary>
        /// <param name="evt">The render event to query external passes from.</param>
        /// <returns>Whether all external dependencies are attached to the camera already.</returns>
        private bool VerifyExternalPasses(CameraEvent evt)
        {
            var dependencies = OnRequestExternalPassDependencies(evt);
            if (dependencies != null)
            {
                var commandBuffers = Camera.GetCommandBuffers(evt);
                foreach (var dependency in dependencies)
                {
                    var dependencyExists = commandBuffers.Any(cb => cb.name.ToLower().Contains(dependency.ToLower()));
                    if (!dependencyExists)
                    {
#if UNITY_EDITOR
                        // Log a warning in editor if the dependency is missing
                        if (!_silenceCommandBufferWarnings)
                        {
                            Log.Warning("Tried to add command buffer for " + RendererName +
                                " but dependency " + dependency + " is missing on the camera (" + Camera.name + ").");
                            _silenceCommandBufferWarnings = true;
                        }
#endif
                        // Fail the verification
                        return false;
                    }
                }
            }

            // All dependencies are present
            return true;
        }

        /// <summary>
        /// Attaches or detaches the command buffer to the camera based on the
        /// specified enable flag.
        /// </summary>
        /// <param name="enable">
        ///     A boolean flag indicating whether to attach (true) or detach (false) the command buffer.
        /// </param>
        private void ToggleCommandBuffer(bool enable)
        {
            switch (enable)
            {
                case true when !IsCommandBufferAdded:
                    foreach (var cameraEvent in CameraEvents)
                        Camera.AddCommandBuffer(cameraEvent, _commandBuffer);
                    IsCommandBufferAdded = true;
                    break;

                case false when IsCommandBufferAdded:
                    foreach (var cameraEvent in CameraEvents)
                        Camera.RemoveCommandBuffer(cameraEvent, _commandBuffer);
                    IsCommandBufferAdded = false;
                    _silenceCommandBufferWarnings = false;
                    break;
            }
        }

        /// <summary>
        /// Overrides the current material used in rendering with a new material.
        /// If the component is using the default material, it will be destroyed.
        /// This method also resets the command buffer initialization state.
        /// </summary>
        /// <param name="material">
        ///     The new material to be used for rendering. If this argument is null, the renderer
        ///     will reconstruct the internal material defined by the ShaderName property.
        /// </param>
        protected void OverrideMaterial(Material material)
        {
            var isUsingInternalMaterial = !_didOverrideMaterial && Material != null;
            if (isUsingInternalMaterial)
            {
                // Destroy the material we have ownership of
                Destroy(Material);
            }

            // Assign the external material
            Material = material;

            _isCommandBufferDirty = true;
            _didOverrideMaterial = material != null;
        }
    }
}
