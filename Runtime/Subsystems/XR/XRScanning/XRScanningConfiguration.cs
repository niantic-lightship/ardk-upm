// Copyright 2022-2025 Niantic.

using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Niantic.Lightship.AR.Utilities;
using Niantic.Lightship.AR.Utilities.Logging;
using UnityEngine;

namespace Niantic.Lightship.AR.XRSubsystems
{
    /// <summary>
    /// Configuration for scanning.
    /// </summary>
    /// <seealso cref="XRScanningConfiguration"/>
    [PublicAPI]
    [StructLayout(LayoutKind.Sequential)]
    public class XRScanningConfiguration
    {
        private const int DEFAULT_FRAMERATE = 15;
        private const bool DEFAULT_RAYCASTER_VIS_ENABLED = false;
        private const int DEFAULT_RAYCASTER_VIS_WIDTH = 256;
        private const int DEFAULT_RAYCASTER_VIS_HEIGHT = 144;
        private const bool DEFAULT_VOXEL_VIS_ENABLED = false;
        private const bool DEFAULT_FULL_RESOLUTION_ENABLED = false;
        private const int DEFAULT_FULL_RESOLUTION_FRAMERATE = 2;
        private const int MIN_FRAMERATE = 1;
        private const int MAX_FRAMERATE = 30;
        private const int MIN_RAYCASTER_VIS_RESOLUTION = 10;
        private const int MAX_RAYCASTER_VIS_RESOLUTION = 1024;
        private const bool DEFAULT_USE_ESTIMATED_DEPTH = true;
        private const float DEFAULT_VOXEL_SIZE = 0.04f;
        private const float DEFAULT_NEAR_DEPTH = 0.02f;
        private const float DEFAULT_FAR_DEPTH = 5.0f;

        private int _framerate;
        private bool _raycasterVisualizationEnabled;
        private Vector2 _raycasterVisualizationResolution;
        private bool _voxelVisualizationEnabled;
        private string _scanBasePath;
        private string _scanTargetId;
        private bool _useEstimatedDepth;
        internal string RawScanTargetId { get; private set; }
        private bool _fullResolutionEnabled;
        private int _fullResolutionFramerate;
        private float _voxelSize;
        private float _nearDepth;
        private float _farDepth;

        public int Framerate
        {
            get => _framerate;
            set
            {
                if (value < MIN_FRAMERATE || value > MAX_FRAMERATE)
                {
                    Log.Warning($"Scan record FPS must be between 1 and 30, but got: {value}");
                }

                _framerate = Mathf.Clamp(value, MIN_FRAMERATE, MAX_FRAMERATE);
            }
        }

        public bool RaycasterVisualizationEnabled
        {
            get => _raycasterVisualizationEnabled;
            set => _raycasterVisualizationEnabled = value;
        }

        /// The resolution of the raycast visualization's output images. The output quality is bound by both this resolution
        /// as well as the quality of the underlying 3D reconstruction data. On devices without native depth support, the
        /// underlying data is unlikely to be good enough to support resolution larger than 256x144.
        public Vector2 RaycasterVisualizationResolution
        {
            get => _raycasterVisualizationResolution;
            set
            {
                if (value.x < MIN_RAYCASTER_VIS_RESOLUTION ||
                    value.x > MAX_RAYCASTER_VIS_RESOLUTION ||
                    value.y < MIN_RAYCASTER_VIS_RESOLUTION ||
                    value.y > MAX_RAYCASTER_VIS_RESOLUTION)
                {
                    Log.Warning(
                        $"Scan raycaster resolution width and height are within 10 and 1024, but got: [{value.x}, {value.y}]");
                }

                _raycasterVisualizationResolution.x = Mathf.Clamp(
                    (int)value.x, MIN_RAYCASTER_VIS_RESOLUTION, MAX_RAYCASTER_VIS_RESOLUTION);
                _raycasterVisualizationResolution.y = Mathf.Clamp(
                    (int)value.y, MIN_RAYCASTER_VIS_RESOLUTION, MAX_RAYCASTER_VIS_RESOLUTION);
            }
        }

        public float NearDepth
        {
            get => _nearDepth;
            set => _nearDepth = value;
        }

        public float FarDepth
        {
            get => _farDepth;
            set => _farDepth = value;
        }

        public bool VoxelVisualizationEnabled
        {
            get => _voxelVisualizationEnabled;
            set => _voxelVisualizationEnabled = value;
        }

        public float VoxelSize
        {
            get => _voxelSize;
            set => _voxelSize = value;
        }

        public bool UseEstimatedDepth
        {
            get => _useEstimatedDepth;
            set => _useEstimatedDepth = value;
        }

        public bool FullResolutionEnabled
        {
            get => _fullResolutionEnabled;
            set => _fullResolutionEnabled = value;
        }

        public int FullResolutionFramerate
        {
            get => _fullResolutionFramerate;
            set => _fullResolutionFramerate = value;
        }

        public string ScanBasePath
        {
            get => _scanBasePath;
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    throw new ArgumentException("Scan path cannot be null or empty.");
                }
                _scanBasePath = value;
            }
        }

        public string ScanTargetId
        {
            get => _scanTargetId;
            set
            {
                _scanTargetId = value;
                if (string.IsNullOrEmpty(_scanTargetId))
                {
                    RawScanTargetId = "";
                } else
                {
                    try
                    {
                        byte[] encodedId = System.Convert.FromBase64String(_scanTargetId);
                        byte[] key = new byte[8];
                        byte[] iv = new byte[8];
                        byte[] remainder = new byte[encodedId.Length - 17];

                        Buffer.BlockCopy(encodedId, 1, key, 0, 8);
                        Buffer.BlockCopy(encodedId, 9, iv, 0, 8);
                        Buffer.BlockCopy(encodedId, 17, remainder, 0, remainder.Length);

                        SymmetricAlgorithm algorithm = DES.Create();
                        ICryptoTransform transform = algorithm.CreateDecryptor(key, iv);
                        byte[] decodedId = transform.TransformFinalBlock(remainder, 0, remainder.Length);
                        RawScanTargetId = Encoding.Unicode.GetString(decodedId);
                    }
                    catch (Exception e)
                    {
                        throw new ArgumentException(
                            "Failed to decode scan target ID. Is this ID obtained from ScanTargetClient?", e);
                    }

                }
            }
        }


        /// <summary>
        /// Default constructor for the XRScanningConfiguration.
        /// </summary>
        public XRScanningConfiguration()
        {
            _framerate = DEFAULT_FRAMERATE;
            _raycasterVisualizationEnabled = DEFAULT_RAYCASTER_VIS_ENABLED;
            _raycasterVisualizationResolution = new Vector2(DEFAULT_RAYCASTER_VIS_WIDTH, DEFAULT_RAYCASTER_VIS_HEIGHT);
            _voxelVisualizationEnabled = DEFAULT_VOXEL_VIS_ENABLED;
            _scanBasePath = Application.persistentDataPath;
            _scanTargetId = "";
            _useEstimatedDepth = DEFAULT_USE_ESTIMATED_DEPTH;
            RawScanTargetId = "";
            _fullResolutionEnabled = DEFAULT_FULL_RESOLUTION_ENABLED;
            _fullResolutionFramerate = DEFAULT_FULL_RESOLUTION_FRAMERATE;
            _voxelSize = DEFAULT_VOXEL_SIZE;
            _nearDepth = DEFAULT_NEAR_DEPTH;
            _farDepth = DEFAULT_FAR_DEPTH;
        }

        /// <summary>
        /// Tests for equality.
        /// </summary>
        /// <param name="other">The other <see cref="XRScanningConfiguration"/> to compare against.</param>
        /// <returns>`True` if every field in <paramref name="other"/> is equal to this <see cref="XRPersistentAnchor"/>, otherwise false.</returns>
        private bool Equals(XRScanningConfiguration other)
        {
            return
                _framerate.Equals(other.Framerate) &&
                _raycasterVisualizationEnabled.Equals(other.RaycasterVisualizationEnabled) &&
                _raycasterVisualizationResolution == other.RaycasterVisualizationResolution &&
                _voxelVisualizationEnabled == other.VoxelVisualizationEnabled &&
                string.Equals(_scanBasePath, other._scanBasePath) &&
                string.Equals(_scanTargetId, other._scanTargetId) &&
                _fullResolutionEnabled == other._fullResolutionEnabled &&
                _fullResolutionFramerate.Equals(other.FullResolutionFramerate) &&
                _useEstimatedDepth == other._useEstimatedDepth &&
                Mathf.Approximately(_voxelSize, other.VoxelSize) &&
                Mathf.Approximately(_nearDepth, other.NearDepth) &&
                Mathf.Approximately(_farDepth, other.FarDepth);
        }
    }
}
