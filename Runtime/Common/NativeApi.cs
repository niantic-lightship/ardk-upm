// Copyright 2022-2025 Niantic.
using System;
using System.Runtime.InteropServices;
using Niantic.Lightship.AR.Core;
using Niantic.Lightship.AR.PAM;

namespace Niantic.Lightship.AR.Common
{
    internal class LightshipImageApi
    {
        public static int Convert(ImageFormatCEnum sourceType, IntPtr sourceData, int sourceSize, int width, int height, ImageFormatCEnum destType, out byte[] destData, out int destSize)
        {
            destSize = 0;
            GCHandle sizeHandle = GCHandle.Alloc(destSize, GCHandleType.Pinned);

            destData = new byte[width * height * 4];  // Max size of ARGB32
            GCHandle dataHandle = GCHandle.Alloc(destData, GCHandleType.Pinned);

            int res = 0;
            try
            {
                IntPtr destSizePtr = sizeHandle.AddrOfPinnedObject();
                IntPtr destDataPtr = dataHandle.AddrOfPinnedObject();
                res = Native.Lightship_ARDK_Unity_Image_Convert(sourceType, sourceData, sourceSize, width,
                    height, destType, destDataPtr, destSizePtr);
            }
            finally
            {
                sizeHandle.Free();
                dataHandle.Free();
            }

            return res;
        }

        private static class Native
        {
            [DllImport(LightshipPlugin.Name)]
            public static extern int Lightship_ARDK_Unity_Image_Convert(ImageFormatCEnum sourceType, IntPtr sourceData, int sourceSize, int width, int height, ImageFormatCEnum destType, IntPtr destData, IntPtr destSize);
        }
    }
}  // namespace Niantic.Lightship.AR.Common
