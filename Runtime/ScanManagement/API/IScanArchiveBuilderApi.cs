// Copyright 2022-2024 Niantic.
using System;
using Unity.Collections;

namespace Niantic.Lightship.AR.Scanning
{
    internal interface IScanArchiveBuilderApi
    {
        public IntPtr Create(IntPtr unityContext, string basePath, string scanId, string userDataStr, int maxFramesPerChunk);

        public void Release(IntPtr handle);

        public bool HasMoreChunks(IntPtr handle);

        public bool IsValid(IntPtr handle);

        public string GetNextChunk(IntPtr handle);

        public string GetNextChunkUuid(IntPtr handle);

        public string GetScanTargetId(IntPtr handle);

        public void CancelGetNextChunk(IntPtr handle);
    }
}
