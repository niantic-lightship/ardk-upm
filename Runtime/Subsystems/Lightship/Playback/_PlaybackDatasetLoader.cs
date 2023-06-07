using System.IO;
using System.Linq;
using Niantic.Lightship.Utilities;
using UnityEngine;

namespace Niantic.Lightship.AR.Playback
{
    internal static class _PlaybackDatasetLoader
    {
        private const string StreamingAssetsDirName = "StreamingAssets";
        private const string CaptureFileName = "capture.json";

        public static _PlaybackDataset Load(string datasetPath)
        {
            if (string.IsNullOrEmpty(datasetPath))
            {
                Debug.LogError($"Parameter '{nameof(datasetPath)}' is empty.");
                return null;
            }

            if (Path.HasExtension(datasetPath))
            {
                Debug.LogError($"Parameter '{nameof(datasetPath)}' must be a directory.");
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

            var filePath = Path.Combine(datasetPath, CaptureFileName);
            var content = FileUtilities.GetAllText(filePath);

            if (!string.IsNullOrEmpty(content))
            {
                var dataset = new _PlaybackDataset(content, datasetPath);
                Debug.Log($"Loaded dataset with {dataset.FrameCount} frames from {datasetPath}");

                return dataset;
            }

            return null;
        }

        private static string GetAbsoluteStreamingAssetsPath(string path)
        {
            var relativePath = path.Split(StreamingAssetsDirName + Path.DirectorySeparatorChar).Last();
             return Path.Combine(Application.streamingAssetsPath, relativePath);
        }
    }
}
