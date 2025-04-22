// Copyright 2022-2025 Niantic.
using System.IO;
using System.Linq;
using Niantic.Lightship.AR.Utilities.Logging;
using Niantic.Lightship.AR.Utilities;
using Niantic.Lightship.Utilities.UnityAssets;
using UnityEngine;

namespace Niantic.Lightship.AR.Subsystems.Playback
{
    internal static class PlaybackDatasetLoader
    {
        private const string StreamingAssetsDirName = "StreamingAssets";
        private const string CaptureFileName = "capture.json";

        public static PlaybackDataset Load(string datasetPath)
        {
            Log.Debug($"Attempting to load dataset from {datasetPath}");

            if (string.IsNullOrEmpty(datasetPath))
            {
                Log.Error($"Parameter '{nameof(datasetPath)}' is empty.");
                return null;
            }

            if (Path.HasExtension(datasetPath))
            {
                Log.Error($"Parameter '{nameof(datasetPath)}' must be a directory.");
                return null;
            }

            // If run through the Unity Editor, FileUtilities is able to handle any absolute or project relative path.
            // However, once on device, we want to turn StreamingAssets paths (the only ones currently valid)
            // into the device-specific absolute path.
#if !UNITY_EDITOR
            if (datasetPath.Contains(StreamingAssetsDirName))
            {
                datasetPath = GetAbsoluteStreamingAssetsPath(datasetPath);
            }
#endif

            // File.Exists(filePath); and Directory.Exists(filePath) returns false for android even when the file or directory exists.
#if !UNITY_ANDROID
            if (!Directory.Exists(datasetPath))
            {
                Log.Error($"The dataset directory does not exist at {datasetPath}.");
                return null;
            }
#endif

            var filePath = Path.Combine(datasetPath, CaptureFileName);

#if !UNITY_ANDROID
            if (!File.Exists(filePath))
            {
                Log.Error($"No dataset found at {filePath}");
                return null;
            }
#endif

            var content = FileUtilities.GetAllText(filePath);

            if (!string.IsNullOrEmpty(content))
            {
                var dataset = new PlaybackDataset(content, datasetPath);
                if (dataset.FrameCount == 0)
                {
                    Log.Error($"Loaded dataset from {datasetPath} but found 0 frames.");
                }
                else
                {
                    var datasetOrientation = dataset.Frames[0].Orientation;
                    var liveOrientation =
                        Application.isEditor ? GameViewUtils.GetEditorScreenOrientation() : Screen.orientation;

                    if (datasetOrientation != liveOrientation)
                    {
                        Log.Warning
                        (
                            $"The dataset was recorded in {datasetOrientation} orientation but the game display " +
                            $"is currently in {liveOrientation}. This may result in visual discrepancies."
                        );
                    }

                    Log.Info($"Successfully loaded dataset with {dataset.FrameCount} frames from {datasetPath}");
                    return dataset;
                }
            }

            Log.Error($"Failed to load dataset from {filePath} because the content was empty.");
            return null;
        }

        private static string GetAbsoluteStreamingAssetsPath(string path)
        {
            var relativePath = path.Split(StreamingAssetsDirName + Path.DirectorySeparatorChar).Last();
             return Path.Combine(Application.streamingAssetsPath, relativePath);
        }
    }
}
