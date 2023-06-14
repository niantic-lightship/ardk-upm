// Copyright 2023 Niantic, Inc. All Rights Reserved.

using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Niantic.Lightship.AR.Subsystems
{
    /// <summary>
    /// Configuration for scanning.
    /// </summary>
    /// <seealso cref="XRScanningConfiguration"/>
    [StructLayout(LayoutKind.Sequential)]
    public class XRScanningConfiguration
    {
        private const int DEFAULT_FRAMERATE = 15;
        private const bool DEFAULT_RAYCASTER_VIS_ENABLED = true;
        private const int DEFAULT_RAYCASTER_VIS_WIDTH = 256;
        private const int DEFAULT_RAYCASTER_VIS_HEIGHT = 144;
        private const bool DEFAULT_VOXEL_VIS_ENABLED = true;
        private const float DEFAULT_MAX_SCANNING_DISTANCE = 5.0f;
        private const int MIN_FRAMERATE = 1;
        private const int MAX_FRAMERATE = 15;
        private const int MIN_RAYCASTER_VIS_RESOLUTION = 10;
        private const int MAX_RAYCASTER_VIS_RESOLUTION = 1024;
        private const float MIN_MAX_SCANNING_DISTANCE = 0.1f;
        private const float MAX_MAX_SCANNING_DISTANCE = 5.0f;

        private int _framerate;
        private bool _raycasterVisualizationEnabled;
        private Vector2 _raycasterVisualizationResolution;
        private bool _voxelVisualizationEnabled;
        private float _maxScanningDistance;
        private string _scanBasePath;

        public int Framerate
        {
            get => _framerate;
            set
            {
                if (value < MIN_FRAMERATE || value > MAX_FRAMERATE)
                {
                    Debug.LogWarning($"Scan record FPS must be between 1 and 15, but got: {value}");
                }

                _framerate = Mathf.Clamp(value, MIN_FRAMERATE, MAX_FRAMERATE);
            }
        }

        public bool RaycasterVisualizationEnabled
        {
            get => _raycasterVisualizationEnabled;
            set => _raycasterVisualizationEnabled = value;
        }

        public bool VoxelVisualizationEnabled
        {
            get => _voxelVisualizationEnabled;
            set => _voxelVisualizationEnabled = value;
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
                    Debug.LogWarning(
                        $"Scan raycaster resolution width and height are within 10 and 1024, but got: [{value.x}, {value.y}]");
                }

                _raycasterVisualizationResolution.x = Mathf.Clamp(
                    (int)value.x, MIN_RAYCASTER_VIS_RESOLUTION, MAX_RAYCASTER_VIS_RESOLUTION);
                _raycasterVisualizationResolution.y = Mathf.Clamp(
                    (int)value.y, MIN_RAYCASTER_VIS_RESOLUTION, MAX_RAYCASTER_VIS_RESOLUTION);
            }
        }

        public float MaxScanningDistance
        {
            get => _maxScanningDistance;
            set
            {
                if (value > MAX_MAX_SCANNING_DISTANCE || value < MIN_MAX_SCANNING_DISTANCE)
                {
                    Debug.LogWarning($"Scan max distance must be between 0.1f and 5.0f, but got: {value}");
                }

                _maxScanningDistance = Mathf.Clamp(
                    value, MIN_MAX_SCANNING_DISTANCE, MAX_MAX_SCANNING_DISTANCE);
            }
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

        /// <summary>
        /// Default constructor for the XRScanningConfiguration.
        /// </summary>
        public XRScanningConfiguration()
        {
            _framerate = DEFAULT_FRAMERATE;
            _raycasterVisualizationEnabled = DEFAULT_RAYCASTER_VIS_ENABLED;
            _raycasterVisualizationResolution = new Vector2(DEFAULT_RAYCASTER_VIS_WIDTH, DEFAULT_RAYCASTER_VIS_HEIGHT);
            _voxelVisualizationEnabled = DEFAULT_VOXEL_VIS_ENABLED;
            _maxScanningDistance = DEFAULT_MAX_SCANNING_DISTANCE;
            _scanBasePath = Application.persistentDataPath;
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
                _maxScanningDistance.Equals(other.MaxScanningDistance) &&
                string.Equals(_scanBasePath, other._scanBasePath);
        }
    }
}
