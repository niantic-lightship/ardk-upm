// Copyright 2022-2024 Niantic.
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Niantic.Protobuf;
using Niantic.ARDK.AR.Protobuf;
using Niantic.Lightship.AR.Utilities;

namespace Niantic.ARDK.AR.Scanning
{
    [PublicAPI]
    public class ScanStore
    {
        private string _scanBasePath;

        public string ScanBasePath
        {
            get => _scanBasePath;
        }
        /// <summary>
        /// Create a ScanStore given the base path for all your scans.
        /// The path should match the ScanBasePath of XRScanningConfiguration.
        /// </summary>
        /// <param name="basePath"></param>
        public ScanStore(string basePath)
        {
            _scanBasePath = ScanPaths.GetBasePath(basePath);
        }

        /// <summary>
        /// Delete the given saved scan from disk.
        /// This must not be a scan that is currently in progress. Deleting a scan in progress is undefined behavior.
        /// The scan is invalid after deletion.
        /// </summary>
        /// <param name="scan">The scan to delete.</param>
        public void DeleteScan(SavedScan scan)
        {
            Directory.Delete(scan.ScanPath, true);
        }

        /// <summary>
        /// Delete the scan in an async way. <seealso cref="DeleteScan"/>.
        /// </summary>
        /// <param name="scan"></param>
        /// <returns></returns>
        public Task DeleteScanAsync(SavedScan scan)
        {
            return new Task(() => DeleteScan(scan));
        }

        /// <summary>
        /// Return the list of scans currently saved. This will include the current active
        /// scan if called with a scan in-progress.
        /// </summary>
        /// <returns>A List of scans that are currently on-disk</returns>
        public List<SavedScan> GetSavedScans()
        {
            if (!Directory.Exists(_scanBasePath))
            {
                return new List<SavedScan>();
            }
            string[] scans = Directory.GetDirectories(_scanBasePath);
            List<SavedScan> result = new List<SavedScan>();
            foreach (var scanFolder in scans)
            {
                // TODO(senchang): Correctly filter out scans that aren't valid.
                result.Add(new SavedScan(scanFolder));
            }
            return result;
        }

        [PublicAPI]
        public class SavedScan
        {
            public string ScanPath;
            public string ScanId;

            public SavedScan(string scanPath)
            {
                ScanPath = scanPath;
                ScanId = new DirectoryInfo(scanPath).Name;
            }

            //EXT-REMOVALSTART
            public ScanMetadataProto GetScanMetadata()
            {
                using FileStream stream = File.OpenRead(ScanPaths.GetScanMetadataPath(ScanPath));
                return ScanMetadataProto.Parser.ParseFrom(stream);
            }

            public void SetScanMetadata(ScanMetadataProto proto)
            {
                File.WriteAllBytes(ScanPaths.GetScanMetadataPath(ScanPath), proto.ToByteArray());
            }

            public FramesProto GetScanFrames()
            {
                using FileStream stream = File.OpenRead(ScanPaths.GetScanFramesPath(ScanPath));
                return FramesProto.Parser.ParseFrom(stream);
            }
            //EXT-REMOVALEND

        }
    }
}
