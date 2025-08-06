// Copyright 2022-2025 Niantic.

using System;
using System.Collections.Generic;
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

            // If run through the Unity Editor, FileUtilities is able to handle any absolute or project relative path.
            // We need to create the absolute path on-device.
            if (!Application.isEditor)
            {
                if (datasetPath.Contains(StreamingAssetsDirName))
                {
                    datasetPath = GetAbsoluteStreamingAssetsPath(datasetPath);
                    Log.Info($"Attempting to load dataset from StreamingAssets directory with absolute path {datasetPath}");
                }
                else
                {
                    datasetPath = Path.Combine(Application.persistentDataPath, datasetPath);
                    Log.Info($"Attempting to load dataset from persistent data directory with absolute path {datasetPath}");
                }
            }

            string extension = Path.GetExtension(datasetPath);
            bool isSourceCompressed = false;
            if (extension.Equals(".zip", StringComparison.OrdinalIgnoreCase))
            {
                Log.Info($"Extracting compressed dataset at path {datasetPath}");
                string destination = Path.Combine(Path.Combine(Application.persistentDataPath, "PlaybackDatasets"), Path.GetFileNameWithoutExtension(datasetPath));
                FileUtilities.ExtractToDirectory(datasetPath, destination, true);
                if (!TryGetDataSetPathFromDecompressedDirectory(destination, out datasetPath))
                {
                    return null;
                }
                isSourceCompressed = true;
            }
            else if (!string.IsNullOrEmpty(extension))
            {
                Log.Error($"Parameter '{nameof(datasetPath)}' must be a directory or a zip file.");
                return null;
            }

            if (TryGetDataSetContent(datasetPath, out string content))
            {
                var dataset = new PlaybackDataset(content, datasetPath, isSourceCompressed);
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

            return null;
        }

        private static bool TryGetDataSetPathFromDecompressedDirectory(string destination, out string datasetPath)
        {
            var captureFile = Directory.EnumerateFiles(destination, CaptureFileName, SearchOption.AllDirectories).ToList();
            datasetPath = string.Empty;
            if (!captureFile.Any())
            {
                Log.Error($"No {CaptureFileName} found at {destination}");
                return false;
            }
            else if (captureFile.Count() > 1)
            {
                Log.Error($"Multiple {CaptureFileName} found at {destination}, please ensure the zipped dataset path contains only one set of data");
                return false;
            }

            datasetPath = Path.GetDirectoryName(captureFile.First());
            return true;
        }

        private static bool TryGetDataSetContent(string datasetPath, out string content)
        {
            // File.Exists(filePath); and Directory.Exists(filePath) returns false for android even when the file or directory exists within the StreamingAssets folder
            // this is because the streaming assets folder is packed into the apk and treated as a readonly resource
            // the persistent data path is not treated this way, so we should continue with the file checks
            bool skipFileChecks = Application.platform == RuntimePlatform.Android && datasetPath.Contains(Application.streamingAssetsPath);
            content = String.Empty;

            if (!skipFileChecks && !Directory.Exists(datasetPath))
            {
                Log.Error($"The dataset directory does not exist at {datasetPath}.");
                return false;
            }

            var filePath = Path.Combine(datasetPath, CaptureFileName);

            if (!skipFileChecks && !File.Exists(filePath))
            {
                Log.Error($"No dataset found at {filePath}");
                return false;
            }

            content = FileUtilities.GetAllText(filePath);

            if (string.IsNullOrEmpty(content))
            {
                Log.Error($"Failed to load dataset from {filePath} because the content was empty.");
                return false;
            }

            return true;
        }

        private static string GetAbsoluteStreamingAssetsPath(string path)
        {
            var relativePath = path.Split(StreamingAssetsDirName + Path.DirectorySeparatorChar).Last();
            return Path.Combine(Application.streamingAssetsPath, relativePath);
        }
    }
}
