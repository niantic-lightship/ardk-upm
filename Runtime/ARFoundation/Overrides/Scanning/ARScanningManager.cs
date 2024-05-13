// Copyright 2022-2024 Niantic.

using System.IO;
using System.Threading.Tasks;
using Niantic.ARDK.AR.Scanning;
using Niantic.Lightship.AR.ARFoundation.Unity;
using Niantic.Lightship.AR.Common;
using Niantic.Lightship.AR.Utilities;
using Niantic.Lightship.AR.Utilities.Logging;
using Niantic.Lightship.AR.XRSubsystems;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;
using UnityEngine.XR.ARFoundation;

namespace Niantic.Lightship.AR.Scanning
{
    /// <summary>
    /// A manager for recording scans of the AR scene for Playback.
    /// The recording will start when the manager is enabled.
    /// Use SaveScan() to stop and save the recording into the ScanPath.
    /// </summary>
    [PublicAPI("apiref/Niantic/Lightship/AR/Scanning/ARScanningManager/")]
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(LightshipARUpdateOrder.ScanningManager)]
    public class ARScanningManager
        : SubsystemLifecycleManager<XRScanningSubsystem, XRScanningSubsystemDescriptor, XRScanningSubsystem.Provider>
    {
        /// <summary>
        /// The scan path to store the scan data.
        /// If an absolute path is provided (starting with '/', '\', or a drive name), the directory must be writable,
        /// and the application must have permissions to write to the folder.
        /// Otherwise, the path will be interpreted as relative to Application.persistentDataPath.
        /// </summary>
        [Tooltip("The scan path to store the scan data.\n" +
            "If an absolute path is provided (starting with '/', '\\', or a drive name), the directory must be writable " +
            "and the application must have permissions to write to the folder.\n" +
            "Otherwise, the path will be interpreted as relative to Application.persistentDataPath.")]
        public string ScanPath;

        /// <summary>
        /// Record full resolution images for scan reconstruction.
        /// </summary>
        [Tooltip("Record full resolution images for reconstruction.")]
        public bool FullResolutionEnabled;

        /// <summary>
        /// The scan target ID.
        /// Must be set before scanning starts to take effect.
        /// </summary>
        [Tooltip("The scan target ID. Must be set before scanning starts to take effect.")]
        public string ScanTargetId;

        /// <summary>
        /// The scan recording framerate.
        /// Must be set before scanning starts to take effect.
        /// </summary>
        public int ScanRecordingFramerate { get; set; }

        /// <summary>
        /// Enable raycast visualization for scanning. Required to access the raycast textures.
        /// </summary>
        [Tooltip("Enable raycast visualization for scanning. Required to access the raycast textures.")]
        public bool EnableRaycastVisualization;

        /// <summary>
        /// Enable voxel visualization for scanning. Required to compute voxels.
        /// </summary>
        [Tooltip("Enable voxel visualization for scanning. Required to compute voxels.")]
        public bool EnableVoxelVisualization;

        /// <summary>
        /// Use predicted depth data if the device does not have native depth implementation.
        /// Required if any visualization is enabled.
        /// </summary>
        [FormerlySerializedAs("_useEstimatedDepth")]
        [Tooltip("Use predicted depth data if the device does not have native depth implementation. "
            + "Required if any visualization is enabled.")]
        public bool UseEstimatedDepth;

        /// <summary>
        /// The Raycast Color Buffer for Scan Visualization.
        /// </summary>
        /// <value>
        /// The Raycast Color Buffer for Scan Visualization.
        /// </value>
        private ARTextureInfo _raycastColorTextureInfo;

        /// <summary>
        /// The Raycast Normal Buffer for Scan Visualization.
        /// </summary>
        /// <value>
        /// The Raycast Normal Buffer for Scan Visualization.
        /// </value>
        private ARTextureInfo _raycastNormalTextureInfo;

        /// <summary>
        /// The Raycast Position Buffer for Scan Visualization.
        /// </summary>
        /// <value>
        /// The Raycast Position Buffer for Scan Visualization.
        /// </value>
        private ARTextureInfo _raycastPositionTextureInfo;

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
                Log.Info("Scanning Raycast texture needs to be a Texture2D, but instead is "
                    + $"{textureInfo.Descriptor.dimension.ToString()}.");
                return null;
            }

            return textureInfo.Texture as Texture2D;
        }

        protected override void OnBeforeStart()
        {
            ResetTextureInfos();
            var config = subsystem.CurrentConfiguration;
            config.ScanBasePath = GetScanBasePath();
            config.ScanTargetId = ScanTargetId;
            config.FullResolutionEnabled = FullResolutionEnabled;
            config.RaycasterVisualizationEnabled = EnableRaycastVisualization;
            config.VoxelVisualizationEnabled = EnableVoxelVisualization;
            config.UseEstimatedDepth = UseEstimatedDepth;
            if (ScanRecordingFramerate > 0)
            {
                config.Framerate = ScanRecordingFramerate;
            }
            subsystem.CurrentConfiguration = config;
        }

        public ScanStore GetScanStore()
        {
            return new ScanStore(GetScanBasePath());
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

        private void Update()
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

        private string GetScanBasePath()
        {
            if (string.IsNullOrEmpty(ScanPath))
            {
                return Application.persistentDataPath;
            }
            if (Path.IsPathRooted(ScanPath))
            {
                return ScanPath;
            }
            return Path.Combine(Application.persistentDataPath, ScanPath);
        }
    }
}
