using Niantic.Lightship.AR.ARFoundation;
using Niantic.Lightship.AR.Subsystems;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.ARFoundation;

namespace ARFoundation.Niantic.Scanning
{
    /// <summary>
    /// The manager for the scanning subsystem.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(ARUpdateOrder.k_PointCloudManager)]
    public class ARScanningManager
        : SubsystemLifecycleManager<XRScanningSubsystem, XRScanningSubsystemDescriptor, XRScanningSubsystem.Provider>
    {

        /// <summary>
        /// The Raycast Color Buffer for Scan Visualization.
        /// </summary>
        /// <value>
        /// The Raycast Color Buffer for Scan Visualization.
        /// </value>
        ARTextureInfo _raycastColorTextureInfo;

        /// <summary>
        /// Read the current raycast color texture.
        /// </summary>
        /// <value>
        /// The color texture for raycast visualization, if configured and ready. Otherwise, <c>null</c>.
        /// </value>
        public Texture2D GetRaycastColorTexture()
        {
            if (_raycastColorTextureInfo.Descriptor.dimension != TextureDimension.Tex2D
                && _raycastColorTextureInfo.Descriptor.dimension != TextureDimension.None)
            {
                Debug.Log("Scanning Raycast texture needs to be a Texture2D, but instead is "
                    + $"{_raycastColorTextureInfo.Descriptor.dimension.ToString()}.");
                return null;
            }
            return _raycastColorTextureInfo.Texture as Texture2D;
        }

        protected override void OnBeforeStart()
        {
            ResetTextureInfos();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            ResetTextureInfos();
        }

        private void ResetTextureInfos()
        {
            _raycastColorTextureInfo.Reset();
        }

        public void Update()
        {
            if (subsystem != null)
            {
                UpdateTexturesInfos();
            }
        }

        private void UpdateTexturesInfos()
        {
            if (subsystem.TryGetRaycastBuffer(out var raycastBufferDescriptor))
            {
                _raycastColorTextureInfo = ARTextureInfo.GetUpdatedTextureInfo(_raycastColorTextureInfo, raycastBufferDescriptor);
            }
        }
    }
}
