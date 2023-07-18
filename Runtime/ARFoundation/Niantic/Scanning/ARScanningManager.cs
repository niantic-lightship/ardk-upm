using System.Threading.Tasks;
using Niantic.ARDK.AR.Scanning;
using Niantic.Lightship.AR.Subsystems;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.ARFoundation;

namespace Niantic.Lightship.AR.ARFoundation.Scanning
{
    /// <summary>
    /// The manager for the scanning subsystem.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(LightshipARUpdateOrder.k_ScanningManager)]
    public class ARScanningManager
        : SubsystemLifecycleManager<XRScanningSubsystem, XRScanningSubsystemDescriptor, XRScanningSubsystem.Provider>
    {
        [SerializeField]
        private string scanPathRelative;

        /// <summary>
        /// The Raycast Color Buffer for Scan Visualization.
        /// </summary>
        /// <value>
        /// The Raycast Color Buffer for Scan Visualization.
        /// </value>
        ARTextureInfo _raycastColorTextureInfo;

        /// <summary>
        /// The Raycast Normal Buffer for Scan Visualization.
        /// </summary>
        /// <value>
        /// The Raycast Normal Buffer for Scan Visualization.
        /// </value>
        ARTextureInfo _raycastNormalTextureInfo;

        /// <summary>
        /// The Raycast Position Buffer for Scan Visualization.
        /// </summary>
        /// <value>
        /// The Raycast Position Buffer for Scan Visualization.
        /// </value>
        ARTextureInfo _raycastPositionTextureInfo;

        /// <summary>
        /// Read the current raycast color texture.
        /// </summary>
        /// <value>
        /// The color texture for raycast visualization, if configured and ready. Otherwise, <c>null</c>.
        /// </value>
        public Texture2D GetRaycastColorTexture()
        {
            return GetRaycastTexture(_raycastColorTextureInfo);
        }

        /// <summary>
        /// Read the current raycast normal texture.
        /// </summary>
        /// <value>
        /// The normal texture for raycast visualization, if configured and ready. Otherwise, <c>null</c>.
        /// </value>
        public Texture2D GetRaycastNormalTexture()
        {
            return GetRaycastTexture(_raycastNormalTextureInfo);
        }

        /// <summary>
        /// Read the current raycast position texture.
        /// </summary>
        /// <value>
        /// The position texture for raycast visualization, if configured and ready. Otherwise, <c>null</c>.
        /// </value>
        public Texture2D GetRaycastPositionTexture()
        {
            return GetRaycastTexture(_raycastPositionTextureInfo);
        }

        private Texture2D GetRaycastTexture(ARTextureInfo textureInfo)
        {
            if (textureInfo.Descriptor.dimension != TextureDimension.Tex2D
                && textureInfo.Descriptor.dimension != TextureDimension.None)
            {
                Debug.Log("Scanning Raycast texture needs to be a Texture2D, but instead is "
                    + $"{textureInfo.Descriptor.dimension.ToString()}.");
                return null;
            }

            return textureInfo.Texture as Texture2D;
        }

        protected override void OnBeforeStart()
        {
            ResetTextureInfos();
            var config = subsystem.CurrentConfiguration;
            config.ScanBasePath = Application.persistentDataPath + scanPathRelative;
            subsystem.CurrentConfiguration = config;
        }

        public ScanStore GetScanStore()
        {
            return new ScanStore(Application.persistentDataPath + scanPathRelative);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            ResetTextureInfos();
        }

        private void ResetTextureInfos()
        {
            _raycastColorTextureInfo.Reset();
            _raycastNormalTextureInfo.Reset();
            _raycastPositionTextureInfo.Reset();
        }

        void Update()
        {
            if (subsystem != null)
            {
                UpdateTexturesInfos();
            }
        }

        /// <summary>
        /// Save the current scan. This stops any further recording immediately, and the coroutine finishes when
        /// the saving is fully complete.
        ///
        /// Do not disable the component or exit the app when this is in progress. The scan will not be saved correctly
        /// if this process is interrupted.
        /// </summary>
        /// <returns></returns>
        public async Task SaveScan()
        {
            subsystem.SaveCurrentScan();
            while (subsystem.GetState() == XRScanningState.Saving)
            {
                if (!Application.isPlaying)
                {
                    return;
                }

                await Task.Delay(1);
            }
        }

        /// <summary>
        /// Discard the current scan. This stops further recording immediately, and the coroutine finishes when all
        /// existing data is deleted.
        /// </summary>
        /// <returns></returns>
        public async Task DiscardScan()
        {
            subsystem.DiscardCurrentScan();
            while (subsystem.GetState() == XRScanningState.Discarding)
            {
                if (!Application.isPlaying)
                {
                    return;
                }

                await Task.Delay(1);
            }
        }

        /// <summary>
        /// Returns the current scanID. The result is only present when scan is in progress.
        /// </summary>
        /// <returns></returns>
        public string GetCurrentScanId()
        {
            return subsystem.GetScanId();
        }

        private void UpdateTexturesInfos()
        {
            if (subsystem.TryGetRaycastBuffer(out var raycastBufferDescriptor, out var normalBufferDescriptor, out var positionTextureDescriptor))
            {
                _raycastColorTextureInfo =
                    ARTextureInfo.GetUpdatedTextureInfo(_raycastColorTextureInfo, raycastBufferDescriptor);
                _raycastNormalTextureInfo =
                    ARTextureInfo.GetUpdatedTextureInfo(_raycastNormalTextureInfo, normalBufferDescriptor);
                _raycastPositionTextureInfo =
                    ARTextureInfo.GetUpdatedTextureInfo(_raycastPositionTextureInfo, positionTextureDescriptor);

            }
        }
    }
}
