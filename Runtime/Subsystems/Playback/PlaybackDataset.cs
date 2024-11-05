// Copyright 2022-2024 Niantic.
using System;
using System.Collections.Generic;
using System.Linq;
using Niantic.Lightship.AR.PAM;
using Niantic.Lightship.AR.Subsystems.Playback;
using Niantic.Lightship.AR.Utilities;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.XR.ARSubsystems;

namespace Niantic.Lightship.AR.Subsystems.Playback
{
    /// <summary>
    /// DataContract of the playback dataset
    /// Format declared here: AR/pages/360712457 -> no idea what this means but like, whatever
    /// </summary>
    [Serializable]
    internal class PlaybackDataset
    {
        /// <summary>
        /// the datasetPath is being provided for the Api - potentially to get the base path for images by this class' users.
        /// The content holds the contents of the json file for each playback dataset
        /// </summary>
        /// <param name="content"></param>
        /// <param name="datasetPath"></param>
        public PlaybackDataset(string content, string datasetPath)
        {
            DatasetPath = datasetPath;
            JsonUtility.FromJsonOverwrite(content, this);

            foreach (var frame in Frames)
            {
                if (frame.LocationInfo != null)
                {
                    LocationServicesEnabled = true;

                    if (frame.LocationInfo.HeadingTimestamp != 0)
                    {
                        CompassEnabled = true;

                       // If we know both LocationServices and Compass are enabled, don't need to look through
                       // any more frames.
                       break;
                    }
                }
            }

            LidarEnabled = !string.IsNullOrEmpty(depthSource) && depthSource.Equals("lidar") ||
                !string.IsNullOrEmpty(captureDepthType) && captureDepthType.Equals("lidar");

            if (resolution == null || resolution.Length == 0)
            {
                resolution = new int[] { 0, 0 };
            }
        }

        public string DatasetPath { get; }

        [SerializeField]
        private int autofocus;

        public bool AutofocusEnabled => autofocus == 1;

        [SerializeField]
        private int[] resolution;

        public Vector2Int Resolution => new(resolution[0], resolution[1]);

        [SerializeField]
        private int[] depthResolution;

        public Vector2Int DepthResolution => new(depthResolution[0], depthResolution[1]);

        [SerializeField]
        private int frameCount;

        public int FrameCount => frameCount;

        [SerializeField]
        private float framerate;

        public int FrameRate => (int)Math.Round(framerate);

        public bool LocationServicesEnabled;

        public bool CompassEnabled;

        // name in newer datasets
        [SerializeField]
        private string depthSource;

        // name in older datasets
        [SerializeField]
        private string captureDepthType;

        public bool LidarEnabled;

        [SerializeField]
        private FrameMetadata[] frames;

        private IReadOnlyList<FrameMetadata> framesList;

        public IReadOnlyList<FrameMetadata> Frames
        {
            get
            {
                if (framesList == null)
                {
                    framesList = Array.AsReadOnly(frames);
                }

                return framesList;
            }
        }

        [Serializable]
        public class FrameMetadata
        {
            // Position in the group of captures (i.e. "0" will be the first capture, "1" the second, etc)
            [SerializeField]
            private int sequence;

            public int Sequence => sequence;

            [SerializeField]
            private string image;

            public string ImagePath => image;

            [SerializeField]
            private string depth;

            public string DepthPath => depth;

            public bool HasDepth => !string.IsNullOrEmpty(depth);

            [SerializeField]
            private string depthConfidence;

            public string DepthConfidencePath => depthConfidence;

            // Frame image exposure in seconds.
            [SerializeField]
            private float exposure;

            public float Exposure => exposure;

            // Latest available location information at this frame
            [SerializeField]
            private LocationInfo location;

            public LocationInfo LocationInfo => location.PositionTimestamp == 0 ? null : location;

            //  Camera-to-world frame pose
            [SerializeField]
            private float[] pose4x4;

            public Matrix4x4 Pose => pose4x4?.FromColumnMajorArray().FromOpenGLToUnity() ?? MatrixUtils.InvalidMatrix;

            // Projection matrix for the frame, expressed as a column-major 4x4 matrix,
            // calculated from the frame's resolution with 0.001m as the near plane and 1000.0m as the far plane.
            [SerializeField]
            private float[] projection;

            public Matrix4x4 ProjectionMatrix => projection.FromColumnMajorArray();

            public ScreenOrientation Orientation
            {
                get
                {
                    return Pose.GetScreenOrientation();
                }
            }

            // Resolution [width, height] of the frame image
            [SerializeField]
            private int[] resolution;

            // Intrinsic parameters for the frame.
            // These are consistent with the image resolution and may have been scaled accordingly
            // [fx, fy, cx, cy, s]
            [SerializeField]
            private double[] intrinsics;

            public XRCameraIntrinsics Intrinsics =>
                new XRCameraIntrinsics
                (
                    new Vector2((float)intrinsics[0], (float)intrinsics[1]),
                    new Vector2((float)intrinsics[2], (float)intrinsics[3]),
                    new Vector2Int(resolution[0], resolution[1])
                );

            public CameraIntrinsicsCStruct IntrinsicsCStruct =>
                new CameraIntrinsicsCStruct(
                    new Vector2((float)intrinsics[0], (float)intrinsics[1]),
                    new Vector2((float)intrinsics[2], (float)intrinsics[3]),
                    new Vector2Int(resolution[0], resolution[1])
                );

            public Matrix4x4 DisplayMatrix => Matrix4x4.identity;

            // Tracking quality, following the ARKit ARTrackingState enum.
            // Can be 0 (not available), 1 (limited), 2 (normal).
            [SerializeField]
            private int tracking;

            public TrackingState TrackingState => (TrackingState)tracking;

            // Reason for tracking quality, following the ARKit ARTrackingStateReason enum.
            // Can be 0 (none), 1 (initializing), 2 (relocalizing), 3 (excess motion), 4 (insufficient features).
            [SerializeField]
            private int trackingReason;

            // Device posix timestamp for the beginning of the capture, in seconds (nanosecond precision) since Jan 1 1970 UTC
            [SerializeField]
            private double timestamp;

            public double TimestampInSeconds => timestamp;

            public FrameMetadata(FrameMetadata original, double timestampOffset)
            {
                sequence = original.sequence;
                image = original.image;
                depth = original.depth;
                depthConfidence = original.depthConfidence;
                exposure = original.exposure;
                pose4x4 = original.pose4x4;
                projection = original.projection;
                resolution = original.resolution;
                intrinsics = original.intrinsics;
                tracking = original.tracking;
                trackingReason = original.trackingReason;
                timestamp = original.timestamp + timestampOffset;

                location = new LocationInfo(original.location, timestampOffset);
            }
        }

        [Serializable]
        public class LocationInfo
        {
            // Estimated GPS position latitude in WGS84 degrees.
            [SerializeField]
            private double latitude;

            public double Latitude => latitude;

            // Estimated GPS position longitude in WGS84 degrees.
            [SerializeField]
            private double longitude;

            public double Longitude => longitude;

            // Estimated GPS position error (meters)
            [SerializeField]
            private float positionAccuracy;

            public float PositionAccuracy => positionAccuracy;

            // Device posix timestamp for the GPS position, in seconds (nanosecond precision) since Jan 1 1970 UTC
            [SerializeField]
            private double positionTimestamp;

            public double PositionTimestamp => positionTimestamp;

            //  Estimated altitude above sea level (meters)
            [SerializeField]
            private double altitude;

            public double Altitude => altitude;

            // Estimated altitude error (meters)
            [SerializeField]
            private double altitudeAccuracy;

            public double AltitudeAccuracy => altitudeAccuracy;

            [SerializeField]
            private float heading;

            public float Heading => heading;

            [SerializeField]
            private float headingAccuracy;

            public float HeadingAccuracy => headingAccuracy;

            [SerializeField]
            private double headingTimestamp;

            public double HeadingTimestamp => headingTimestamp;

            public LocationInfo(LocationInfo original, double timestampOffset)
            {
                latitude = original.latitude;
                longitude = original.longitude;
                positionAccuracy = original.positionAccuracy;
                positionTimestamp = original.positionTimestamp + timestampOffset;
                altitude = original.altitude;
                altitudeAccuracy = original.altitudeAccuracy;
                heading = original.heading;
                headingAccuracy = original.headingAccuracy;
                headingTimestamp = original.headingTimestamp + timestampOffset;
            }
        }
    }
}
