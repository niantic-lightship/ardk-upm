// Copyright 2022-2024 Niantic.

using System.Collections.Generic;
using System.Linq;
using Niantic.Lightship.AR.Utilities.Logging;
using UnityEngine;

namespace Niantic.Lightship.AR.Common
{
    internal abstract class CompositeRenderer : ConditionalRenderer
    {
        // The list of active components on this renderer
        private readonly List<RenderComponent> _components = new();

        /// <summary>
        /// Creates and adds the specified render component for this renderer.
        /// </summary>
        /// <typeparam name="TComponent">The type of the render component to add.</typeparam>
        /// <returns>Whether the component was successfully added.</returns>
        protected bool AddRenderComponent<TComponent>() where TComponent : RenderComponent, new()
        {
            // Check if the component already exists
            var component = _components.FirstOrDefault(comp => comp is TComponent);

            // If it doesn't exist, create a new one
            if (component == null)
            {
                component = new TComponent();
                if (OnAddRenderComponent(component))
                {
                    _components.Add(component);
                }
                else
                {
                    Log.Warning("Render component " + typeof(TComponent) +
                        " failed to initialize and could not be added to the renderer.");
                    component.Dispose();
                    return false;
                }
            }

            // Configure the component
            component.SetTargetMaterial(Material);
            return true;
        }

        /// <summary>
        /// Retrieves the specified render component from this renderer.
        /// </summary>
        /// <typeparam name="TComponent">The type of the render component to look for.</typeparam>
        /// <returns>The render component reference, if it was present.</returns>
        protected TComponent GetRenderComponent<TComponent>() where TComponent : RenderComponent
        {
            return _components.FirstOrDefault(comp => comp is TComponent) as TComponent;
        }

        /// <summary>
        /// Removes the specified render component from this renderer, if it exists.
        /// </summary>
        /// <typeparam name="TComponent">The type of the render component to remove.</typeparam>
        /// <returns>Whether the component was found and removed.</returns>
        protected bool RemoveRenderComponent<TComponent>() where TComponent : RenderComponent
        {
            // Find the component and remove it
            var component = _components.FirstOrDefault(comp => comp is TComponent);
            if (component != null)
            {
                _components.Remove(component);
                component.Dispose();
                return true;
            }

            return false;
        }

        /// <summary>
        /// Checks if the specified render component is present on this renderer.
        /// </summary>
        /// <typeparam name="TComponent">The type of the render component to look for.</typeparam>
        /// <returns>Whether the component is present on this renderer.</returns>
        protected bool HasRenderComponent<TComponent>() where TComponent : RenderComponent
        {
            return _components.Any(comp => comp is TComponent);
        }

        /// <summary>
        /// Invoked when a new component is being added to the renderer.
        /// </summary>
        /// <param name="component">The component to be added.</param>
        /// <returns>Whether the component is eligible to run on this renderer.</returns>
        protected virtual bool OnAddRenderComponent(RenderComponent component)
        {
            return true;
        }

        /// <summary>
        /// Invoked when the material state needs to be reset or initialized with
        /// default values. This usually occurs when the material is first created.
        /// </summary>
        /// <param name="mat"></param>
        protected override void OnInitializeMaterial(Material mat)
        {
            base.OnInitializeMaterial(mat);

            foreach (var component in _components)
            {
                component.SetTargetMaterial(mat);
            }
        }

        protected override void Update()
        {
            base.Update();

            // Evaluate whether rendering is enabled
            var isRendering = IsCommandBufferAdded || !IsUsingLegacyRenderPipeline;
            var renderingCamera = Camera;

            // Update all components
            foreach (var component in _components)
            {
                component.Update(renderingCamera, isRendering);
            }
        }

        protected override void OnDestroy()
        {
            // Perform cleanup
            base.OnDestroy();

            // Disable all components
            foreach (var component in _components)
            {
                component.Dispose();
            }

            _components.Clear();
        }
    }
}
