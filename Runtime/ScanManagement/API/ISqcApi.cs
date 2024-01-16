// Copyright 2022-2024 Niantic.
using System;
using Unity.Collections;

namespace Niantic.Lightship.AR.Scanning
{
    internal interface ISqcApi
    {
        public IntPtr SQCCreate(IntPtr unityContext);

        public void SQCRelease(IntPtr handle);

        public bool SQCRun(IntPtr handle, float framerate, string scanPath);

        public void SQCCancelCurrentRun(IntPtr handle);

        public bool SQCIsRunning(IntPtr handle);

        public float SQCGetProgress(IntPtr handle);

        public void SQCGetResult(IntPtr handle,
            string scanPath, IntPtr scores, out int scoresSize);
    }
}
