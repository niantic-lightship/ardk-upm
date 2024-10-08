// Copyright 2022-2024 Niantic.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Niantic.Lightship.AR.Utilities.Logging;
using Niantic.Lightship.AR;
using Niantic.Lightship.AR.Core;
using Niantic.Lightship.AR.Scanning;
using UnityEngine;

namespace Niantic.ARDK.AR.Scanning
{
    /// Additional metadata relating to an uploaded scan.
    [Serializable]
    public class UploadUserInfo
    {
        /// A list of labels labels to associate with the scan.
        public List<string> ScanLabels;

        /// An optional note describing the scan.
        public string Note;
    }

    public class ScanArchiveBuilder : IDisposable
    {
        private readonly IScanArchiveBuilderApi _api;
        private IntPtr _nativeHandle = IntPtr.Zero;

        /// <summary>
        /// Creates an ScanArchiveBuilder for getting scan archive paths for async uploading.
        /// </summary>
        /// <param name="scan">A SavedScan instance.</param>
        /// <param name="uploadUserInfo">Scan Metadata.</param>
        /// <param name="maxFramesPerChunk">Max number of frames per output file.</param>
        /// <returns>An instance of ScanArchiveBuilder. Only one should be used at a time.</returns>
        public ScanArchiveBuilder(ScanStore.SavedScan scan, UploadUserInfo uploadUserInfo, int maxFramesPerChunk = 900)
        {
            _api = new NativeScanArchiveBuilderApi();
#if NIANTIC_LIGHTSHIP_AR_LOADER_ENABLED
            _nativeHandle = _api.Create(LightshipUnityContext.UnityContextHandle, scan.ScanPath, scan.ScanId, JsonUtility.ToJson(uploadUserInfo), maxFramesPerChunk);
#endif
        }

        ~ScanArchiveBuilder()
        {
            if (_nativeHandle != IntPtr.Zero)
            {
                Log.Error("A Scan Archive Builder was not disposed, native resources will leak");
            }

            Dispose(false);
        }

        /// <summary>
        /// Dispose the object and its internal resources.
        /// User MUST call this function to explicitly dispose the resources otherwise memories
        /// will be leaking.
        /// </summary>
        public void Dispose() => Dispose(true);

        /// <summary>
        /// Return if the ScanArchiveBuilder class is valid.
        /// Users must called this function and only use the other ScanArchiveBuilder APIs when it
        /// return true.
        /// </summary>
        public bool IsValid()
        {
            return _api.IsValid(_nativeHandle);
        }

        /// <returns>true if the scan has more chunks to be uploaded.</returns>
        public bool HasMoreChunks()
        {
            return _api.HasMoreChunks(_nativeHandle);
        }

        /// <summary>
        /// Creates a task for packing the next chunk. Must only be called if HasMoreChunks is true, and
        /// no other task is currently running.
        ///
        /// The task is not started automatically. The caller of this method should start the task. The
        /// task does not block.
        /// </summary>
        /// <returns></returns>
        public Task<string> CreateTaskToGetNextChunk()
        {
            return new Task<string>(() => _api.GetNextChunk(_nativeHandle));
        }

        /// <summary>
        /// Cancel the current in-progress task from CreateTaskToGetNextChunk.
        /// Notice that it is possible for the chunk building to succeed before cancellation occurs.
        /// If cancelled, the task will be in "failed" state.
        /// </summary>
        public void CancelGetNextChunkTask()
        {
            _api.CancelGetNextChunk(_nativeHandle);
        }

        /// Following interfaces are used internally only.
        /// <summary>
        /// Return the UUID of the next chunk of scan.
        /// </summary>
        internal string GetNextChunkUuid()
        {
            return _api.GetNextChunkUuid(_nativeHandle);
        }

        /// <summary>
        /// Return the Scan target ID.
        /// </summary>
        internal string GetScanTargetId()
        {
            return _api.GetScanTargetId(_nativeHandle);
        }

        protected virtual void Dispose(bool disposing)
        {
            GC.SuppressFinalize(this);
            if (_nativeHandle != IntPtr.Zero)
            {
                if (disposing)
                {
                    _api.Release(_nativeHandle);
                }

                _nativeHandle = IntPtr.Zero;
            }
        }
    }
}
