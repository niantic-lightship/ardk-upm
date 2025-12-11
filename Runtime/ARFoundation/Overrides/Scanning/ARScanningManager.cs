// Copyright 2022-2025 Niantic.

using System.IO;
using System.Threading.Tasks;
using Niantic.ARDK.AR.Scanning;
using Niantic.Lightship.AR.Common;
using Niantic.Lightship.AR.Utilities;
using Niantic.Lightship.AR.Utilities.Logging;
using Niantic.Lightship.AR.XRSubsystems;
using Unity.Collections;
using UnityEngine;
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
        public string ScanPath
        {
            get => _scanPath;
            set
            {
                _scanPath = value;

                if (null != subsystem)
                {
                    if (subsystem.GetState() != XRScanningState.Ready &&
                        subsystem.GetState() != XRScanningState.Stopped)
                    {
                        Log.Error("Scanning is currently in progress. " +
                            "The change will take effect after the current scan is completed.");
                        return;
                    }
                    Configure();
                }
            }
        }

        [SerializeField]
        [FormerlySerializedAs("ScanPath")]
        private string _scanPath;

        /// <summary>
        /// Record full resolution images for scan reconstruction.
        /// Must be set before scanning starts to take effect.
        /// </summary>
        [Tooltip("Record full resolution images for reconstruction.")]
        public bool FullResolutionEnabled
        {
            get => _fullResolutionEnabled;
            set
            {
                _fullResolutionEnabled = value;

                if (null != subsystem)
                {
                    if (subsystem.GetState() != XRScanningState.Ready &&
                        subsystem.GetState() != XRScanningState.Stopped)
                    {
                        Log.Error("Scanning is currently in progress. " +
                            "The change will take effect after the current scan is completed.");
                        return;
                    }
                    Configure();
                }
            }
        }

        [SerializeField]
        [FormerlySerializedAs("FullResolutionEnabled")]
        private bool _fullResolutionEnabled;

        /// <summary>
        /// The framerate for full resolution frame recording.
        /// A framerate of zero means the system will use the default framerate of 2 FPS.
        /// Must be set before scanning starts to take effect.
        /// </summary>
        [Tooltip("The framerate for full resolution frame recording. A framerate of zero means the system will use the default framerate of 2 FPS.")]
        public int FullResolutionFramerate
        {
            get => _fullResolutionFramerate;
            set
            {
                _fullResolutionFramerate = value;

                if (null != subsystem)
                {
                    if (subsystem.GetState() != XRScanningState.Ready &&
                        subsystem.GetState() != XRScanningState.Stopped)
                    {
                        Log.Error("Scanning is currently in progress. " +
                            "The change will take effect after the current scan is completed.");
                        return;
                    }
                    Configure();
                }
            }
        }

        [SerializeField]
        private int _fullResolutionFramerate = 2;

        /// <summary>
        /// The scan target ID.
        /// Must be set before scanning starts to take effect.
        /// </summary>
        [Tooltip("The scan POI target ID.")]
        public string ScanTargetId
        {
            get => _scanTargetId;
            set
            {
                _scanTargetId = value;

                if (null != subsystem)
                {
                    if (subsystem.GetState() != XRScanningState.Ready &&
                        subsystem.GetState() != XRScanningState.Stopped)
                    {
                        Log.Error("Scanning is currently in progress. " +
                            "The change will take effect after the current scan is completed.");
                        return;
                    }
                    Configure();
                }
            }
        }

        [SerializeField]
        [FormerlySerializedAs("ScanTargetId")]
        private string _scanTargetId;

        /// <summary>
        /// The scan recording framerate.
        /// A framerate of zero means the system will use the default framerate of 15 FPS.
        /// Must be set before scanning starts to take effect.
        /// </summary>
        [Tooltip("The scan recording framerate. A framerate of zero means the system will use the default framerate " +
            "of 15 FPS.")]
        public int ScanRecordingFramerate
        {
            get => _scanRecordingFramerate;
            set
            {
                _scanRecordingFramerate = value;

                if (null != subsystem)
                {
                    if (subsystem.GetState() != XRScanningState.Ready &&
                        subsystem.GetState() != XRScanningState.Stopped)
                    {
                        Log.Error("Scanning is currently in progress. " +
                            "The change will take effect after the current scan is completed.");
                        return;
                    }
                    Configure();
                }
            }
        }

        [SerializeField]
        [FormerlySerializedAs("ScanRecordingFramerate")]
        private int _scanRecordingFramerate = 15;

        /// <summary>
        /// Enable raycast visualization for scanning. Required to access the raycast textures.
        /// The data will be available from the <see cref="GetRaycastColorTexture"/>,
        /// <see cref="GetRaycastNormalTexture"/> and <see cref="GetRaycastPositionTexture"/> methods.
        /// Must be set before scanning starts to take effect.
        /// </summary>
        [Tooltip("Enable raycast visualization for scanning. Required to access the raycast textures.\n\n" +
            "The data will be available from GetRaycastColorTexture(), GetRaycastNormalTexture() " +
            "and GetRaycastPositionTexture() methods.")]
        public bool EnableRaycastVisualization
        {
            get => _enableRaycastVisualization;
            set
            {
                _enableRaycastVisualization = value;

                if (null != subsystem)
                {
                    if (subsystem.GetState() != XRScanningState.Ready &&
                        subsystem.GetState() != XRScanningState.Stopped)
                    {
                        Log.Error("Scanning is currently in progress. " +
                            "The change will take effect after the current scan is completed.");
                        return;
                    }
                    Configure();
                }
            }
        }

        [SerializeField]
        [FormerlySerializedAs("EnableRaycastVisualization")]
        private bool _enableRaycastVisualization;

        /// <summary>
        /// Enable voxel visualization for scanning. Required to compute voxels.
        /// <see cref="RequestVoxelUpdate"/> must be called to asynchronously compute the voxel buffers.
        /// Then, <see cref="TryGetVoxelBuffer"/> can be called to get the voxel buffers.
        /// After <see cref="TryGetVoxelBuffer"/> returns true, the voxel data will be available in the
        /// <see cref="VoxelPositions"/>, <see cref="VoxelColors"/> and <see cref="LatestVoxelSize"/> fields.
        /// Must be set before scanning starts to take effect.
        /// </summary>
        [Tooltip("Enable voxel visualization for scanning. Required to compute voxels.\n\n" +
            "RequestVoxelUpdate must be called to asynchronously compute the voxel buffers. " +
            "Then, TryGetVoxelBuffer can be called to get the voxel buffers.\n\n" +
            "After TryGetVoxelBuffer returns true, the voxel data will be available in the " +
            "VoxelPositions, VoxelColors and LatestVoxelSize fields.")]
        public bool EnableVoxelVisualization
        {
            get => _enableVoxelVisualization;
            set
            {
                _enableVoxelVisualization = value;

                if (null != subsystem)
                {
                    if (subsystem.GetState() != XRScanningState.Ready &&
                        subsystem.GetState() != XRScanningState.Stopped)
                    {
                        Log.Error("Scanning is currently in progress. " +
                            "The change will take effect after the current scan is completed.");
                        return;
                    }
                    Configure();
                }
            }
        }

        [SerializeField]
        [FormerlySerializedAs("EnableVoxelVisualization")]
        private bool _enableVoxelVisualization;

        /// <summary>
        /// Record Niantic depth data if the device does not support platform depth such as lidar.
        /// If platform depth is present, it will be used instead of Niantic depth.
        /// </summary>
        [Tooltip("Record Niantic depth data if the device does not support platform depth such as lidar. " +
            "If platform depth is present, it will be used instead of Niantic depth.")]
        public bool UseEstimatedDepth
        {
            get => _useEstimatedDepth;
            set
            {
                _useEstimatedDepth = value;

                if (null != subsystem)
                {
                    if (subsystem.GetState() != XRScanningState.Ready &&
                        subsystem.GetState() != XRScanningState.Stopped)
                    {
                        Log.Error("Scanning is currently in progress. " +
                            "The change will take effect after the current scan is completed.");
                        return;
                    }
                    Configure();
                }
            }
        }

        [SerializeField]
        [FormerlySerializedAs("UseEstimatedDepth")]
        private bool _useEstimatedDepth;

        /// <summary>
        /// The minimum size of voxels for voxel visualization, in meters.
        /// This parameter sets the initial resolution of the voxel grid used for voxel visualization (see
        /// <see cref="EnableVoxelVisualization"/>. Smaller values result in higher resolution but require more memory
        /// and computation. The actual voxel size may be larger due to memory constraints, so this is only a minimum
        /// value. Refer to <see cref="LatestVoxelSize"/> for the correct dimensions when rendering voxels.
        /// Must be set before scanning starts to take effect.
        /// </summary>
        [Tooltip("The size of voxels for voxel visualization, in meters.\n\n" +
            "This parameter sets the initial resolution of the voxel grid used for " +
            "voxel visualization (see EnableVoxelVisualization). Smaller values result in higher resolution " +
            "but require more memory and computation. The actual voxel size may be larger due to memory constraints, " +
            "so this is only a minimum value. Refer to LatestVoxelSize for the correct dimensions when rendering voxels.")]
        public float MinimumVoxelSize
        {
            get => _minimumVoxelSize;
            set
            {
                _minimumVoxelSize = value;

                if (null != subsystem)
                {
                    if (subsystem.GetState() != XRScanningState.Ready &&
                        subsystem.GetState() != XRScanningState.Stopped)
                    {
                        Log.Error("Scanning is currently in progress. " +
                            "The change will take effect after the current scan is completed.");
                        return;
                    }
                    Configure();
                }
            }
        }

        [SerializeField]
        private float _minimumVoxelSize = 0.01f;

        /// <summary>
        /// The near depth plane for depth range, in meters.
        /// This parameter controls the closest distance at which depth data
        /// will be integrated. Objects closer than this distance will not be
        /// visible in visualization or reconstruction.
        /// Must be set before scanning starts to take effect.
        /// </summary>
        [Tooltip("The near depth plane for depth range, in meters.\n\n" +
            "This parameter controls the closest distance at which depth data " +
            "will be integrated. Objects closer than this distance will not be " +
            "visible in visualization or reconstruction.")]
        public float NearDepth
        {
            get => _nearDepth;
            set
            {
                _nearDepth = Mathf.Clamp(value, 0.0f, _farDepth);

                if (null != subsystem)
                {
                    if (subsystem.GetState() != XRScanningState.Ready &&
                        subsystem.GetState() != XRScanningState.Stopped)
                    {
                        Log.Error("Scanning is currently in progress. " +
                            "The change will take effect after the current scan is completed.");
                        return;
                    }
                    Configure();
                }
            }
        }

        [SerializeField]
        [FormerlySerializedAs("RaycastNearDepth")]
        [FormerlySerializedAs("VisualizationNearDepth")]
        private float _nearDepth = 0.02f;

        /// <summary>
        /// The far depth plane for depth range, in meters.
        /// This parameter controls the farthest distance at which depth data
        /// will be integrated. Objects farther than this distance will not be
        /// visible in visualization or reconstruction.
        /// Must be set before scanning starts to take effect.
        /// </summary>
        [Tooltip("The far depth plane for depth range, in meters.\n\n" +
            "This parameter controls the farthest distance at which depth data " +
            "will be integrated. Objects farther than this distance will not be " +
            "visible in visualization or reconstruction.")]
        public float FarDepth
        {
            get => _farDepth;
            set
            {
                _farDepth = value;

                if (null != subsystem)
                {
                    if (subsystem.GetState() != XRScanningState.Ready &&
                        subsystem.GetState() != XRScanningState.Stopped)
                    {
                        Log.Error("Scanning is currently in progress. " +
                            "The change will take effect after the current scan is completed.");
                        return;
                    }
                    Configure();
                }
            }
        }

        [SerializeField]
        [FormerlySerializedAs("RaycastFarDepth")]
        [FormerlySerializedAs("VisualizationFarDepth")]
        private float _farDepth = 5.0f;

        /// <summary>
        /// The Raycast Color Buffer for Scan Visualization.
        /// </summary>
        /// <value>
        /// The Raycast Color Buffer for Scan Visualization.
        /// </value>
        private LightshipExternalTexture _raycastColorTextureInfo;

        /// <summary>
        /// The Raycast Normal Buffer for Scan Visualization.
        /// </summary>
        /// <value>
        /// The Raycast Normal Buffer for Scan Visualization.
        /// </value>
        private LightshipExternalTexture _raycastNormalTextureInfo;

        /// <summary>
        /// The Raycast Position Buffer for Scan Visualization.
        /// </summary>
        /// <value>
        /// The Raycast Position Buffer for Scan Visualization.
        /// </value>
        private LightshipExternalTexture _raycastPositionTextureInfo;

        /// <summary>
        /// The positions of voxels scanned with the camera.
        /// Each entry in the array corresponds to an entry in <see cref="VoxelColors"/> at the same index.
        /// These values can only be updated when <see cref="EnableVoxelVisualization"/> is true and scanning is
        /// in the <see cref="XRScanningState.Started"/> state. Call <see cref="RequestVoxelUpdate"/> to update the
        /// underlying map, and then call <see cref="TryGetVoxelBuffer"/> to populate with the latest values.
        /// </summary>
        public NativeArray<Vector3> VoxelPositions => _voxelPositions;

        private NativeArray<Vector3> _voxelPositions;

        /// <summary>
        /// The color of each voxel.
        /// Each entry in the array corresponds to an entry in <see cref="VoxelPositions"/> at the same index.
        /// These values can only be updated when <see cref="EnableVoxelVisualization"/> is true and scanning is
        /// in the <see cref="XRScanningState.Started"/> state. Call <see cref="RequestVoxelUpdate"/> to update the
        /// underlying map, and then call <see cref="TryGetVoxelBuffer"/> to populate with the latest values.
        /// </summary>
        public NativeArray<Color32> VoxelColors => _voxelColors;

        private NativeArray<Color32> _voxelColors;

        /// <summary>
        /// The normal vector of each voxel.
        /// Each entry in the array corresponds to an entry in <see cref="VoxelPositions"/> at the same index.
        /// These values can only be updated when <see cref="EnableVoxelVisualization"/> is true and scanning is
        /// in the <see cref="XRScanningState.Started"/> state. Call <see cref="RequestVoxelUpdate"/> to update the
        /// underlying map, and then call <see cref="TryGetVoxelBuffer"/> to populate with the latest values.
        /// </summary>
        public NativeArray<Vector3> VoxelNormals => _voxelNormals;

        private NativeArray<Vector3> _voxelNormals;

        /// <summary>
        /// The size of the voxels in <see cref="VoxelPositions"/>, in meters.
        /// The voxel visualizer attempts to use the voxel size requested by the <see cref="MinimumVoxelSize"/> parameter.
        /// As the number of voxels grows, the voxel visualizer periodically doubles the voxel size to keep memory use
        /// in check. This value should be used for rendering the voxels with the correct dimensions.
        /// </summary>
        public float LatestVoxelSize => _latestVoxelSize;

        private float _latestVoxelSize;

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

        private static Texture2D GetRaycastTexture(LightshipExternalTexture texture)
        {
            if (texture == null)
            {
                return null;
            }

            if (texture is not LightshipExternalTexture2D externalTexture2D)
            {
                Log.Info("Scanning Raycast texture needs to be a Texture2D, but instead is "
                    + $"{texture.Descriptor.dimension.ToString()}.");
                return null;
            }

            return externalTexture2D.Texture as Texture2D;
        }

        protected override void OnBeforeStart()
        {
            ResetTextureInfos();
            Configure();
        }

        private void Configure()
        {
            if (subsystem == null)
            {
                return;
            }

            var config = subsystem.CurrentConfiguration;
            config.ScanBasePath = GetScanBasePath();
            config.ScanTargetId = ScanTargetId;
            config.FullResolutionEnabled = FullResolutionEnabled;
            config.FullResolutionFramerate = FullResolutionFramerate;
            config.RaycasterVisualizationEnabled = EnableRaycastVisualization;
            config.VoxelVisualizationEnabled = EnableVoxelVisualization;
            config.UseEstimatedDepth = UseEstimatedDepth;
            config.VoxelSize = MinimumVoxelSize;
            config.NearDepth = NearDepth;
            config.FarDepth = FarDepth;
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
            _raycastColorTextureInfo?.Dispose();
            _raycastColorTextureInfo = null;

            _raycastNormalTextureInfo?.Dispose();
            _raycastNormalTextureInfo = null;

            _raycastPositionTextureInfo?.Dispose();
            _raycastPositionTextureInfo = null;

            if (_voxelPositions.IsCreated)
            {
                _voxelPositions.Dispose();
                _voxelPositions = new NativeArray<Vector3>();
            }

            if (_voxelColors.IsCreated)
            {
                _voxelColors.Dispose();
                _voxelColors = new NativeArray<Color32>();
            }

            if (_voxelNormals.IsCreated)
            {
                _voxelNormals.Dispose();
                _voxelNormals = new NativeArray<Vector3>();
            }
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
            if (EnableRaycastVisualization && subsystem.TryGetRaycastBuffer(out var raycastBufferDescriptor,
                    out var normalBufferDescriptor, out var positionTextureDescriptor))
            {
                LightshipExternalTexture.CreateOrUpdate(ref _raycastColorTextureInfo, raycastBufferDescriptor);
                LightshipExternalTexture.CreateOrUpdate(ref _raycastNormalTextureInfo, normalBufferDescriptor);
                LightshipExternalTexture.CreateOrUpdate(ref _raycastPositionTextureInfo, positionTextureDescriptor);
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

        public void RequestVoxelUpdate()
        {
            if (!EnableVoxelVisualization)
                return;

            // Async request
            subsystem.ComputeVoxels();
        }

        public bool TryGetVoxelBuffer()
        {
            if (!EnableVoxelVisualization)
            {
                return false;
            }

            if (!subsystem.TryGetVoxelBuffer(out var voxelData))
            {
                Log.Warning("Failed to get new voxel buffer");
                return false;
            }

            if (voxelData.Positions.Length > 0 && voxelData.Colors.Length > 0)
            {
                if (VoxelPositions.IsCreated)
                    VoxelPositions.Dispose();
                if (VoxelColors.IsCreated)
                    VoxelColors.Dispose();
                if (VoxelNormals.IsCreated)
                    VoxelNormals.Dispose();

                _voxelPositions = new NativeArray<Vector3>(voxelData.Positions, Allocator.Persistent);
                _voxelColors = new NativeArray<Color32>(voxelData.Colors, Allocator.Persistent);
                _voxelNormals = new NativeArray<Vector3>(voxelData.Normals, Allocator.Persistent);
                _latestVoxelSize = subsystem.GetVoxelSize();
            }

            subsystem.DisposeVoxelBuffer(voxelData);
            return true;
        }
    }
}
