// Copyright 2022-2024 Niantic.

using System;
using Niantic.Lightship.AR.Utilities.Logging;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.ARFoundation;

namespace Niantic.Lightship.AR.Common
{
    [Obsolete("Use ConditionalRenderer instead.")]
    [RequireComponent(typeof(ARCameraManager))]
    public abstract class LightshipPostBackgroundRenderer : MonoBehaviour
    {
        /// <summary>
        /// The AR Camera Manager component.
        /// </summary>
        protected ARCameraManager Camera { get; private set; }

        /// <summary>
        /// The material using the shader specified by ShaderName.
        /// </summary>
        protected Material Material { get; private set; }

        // The command buffer used to render the image effect
        private CommandBuffer _commandBuffer;

        // State
        private const int KMinFramesBeforeAttaching = 2;
        private bool _didInitializeCommandBuffer;
        private bool _didAttachCommandBuffer;
        private int _frameCount;

        /// <summary>
        /// The name of the shader used by the rendering material.
        /// </summary>
        protected abstract string ShaderName { get; }

        /// <summary>
        /// The name of this rendering component.
        /// </summary>
        protected abstract string RendererName { get; }

        /// <summary>
        /// Invoked when it is time to configure the command buffer.
        /// </summary>
        /// <param name="commandBuffer">The command buffer resource.</param>
        /// <returns>Whether the command buffer could be successfully configured.</returns>
        protected abstract bool ConfigureCommandBuffer(CommandBuffer commandBuffer);

        protected virtual void Awake()
        {
            if (!Initialize())
            {
                Destroy(this);
            }
        }

        protected virtual void OnDestroy()
        {
            _commandBuffer?.Dispose();
            if (Material != null)
            {
                Destroy(Material);
            }
        }

        protected virtual void OnEnable()
        {
            Camera.frameReceived += Camera_OnFrameReceived;
        }

        protected virtual void OnDisable()
        {
            Camera.frameReceived -= Camera_OnFrameReceived;
            ToggleCommandBuffer(false);
        }

        protected virtual void Update()
        {
            // Set up the command buffer
            if (!_didInitializeCommandBuffer)
            {
                _didInitializeCommandBuffer = ConfigureCommandBuffer(_commandBuffer);
            }
        }

        private bool Initialize()
        {
            var shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                Log.Error("Failed to find shader: " + ShaderName);
                return false;
            }

            // Allocate resources
            Material = new Material(shader);
            _commandBuffer = new CommandBuffer { name = RendererName };

            // Acquire components
            Camera = GetComponent<ARCameraManager>();

            return true;
        }

        private void ToggleCommandBuffer(bool enable)
        {
            // Wait for a few frames before attaching the command buffer
            if (_frameCount < KMinFramesBeforeAttaching)
            {
                return;
            }

            if (enable && !_didAttachCommandBuffer)
            {
                Camera.GetComponent<Camera>().AddCommandBuffer(CameraEvent.BeforeForwardOpaque, _commandBuffer);
                _didAttachCommandBuffer = true;
            }
            else if (!enable && _didAttachCommandBuffer)
            {
                Camera.GetComponent<Camera>().RemoveCommandBuffer(CameraEvent.BeforeForwardOpaque, _commandBuffer);
                _didAttachCommandBuffer = false;
                _frameCount = 0;
            }
        }

        private void Camera_OnFrameReceived(ARCameraFrameEventArgs args)
        {
            // Count the number of frames
            // This is used to make sure the command buffer is executed after the background is rendered
            _frameCount = (_frameCount % int.MaxValue) + 1;

            // Enable the command buffer if it is not already enabled
            if (enabled && !_didAttachCommandBuffer)
            {
                ToggleCommandBuffer(true);
            }
        }
    }
}
