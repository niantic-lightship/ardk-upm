// Copyright 2022-2024 Niantic.
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using Niantic.Lightship.AR;
using Niantic.Lightship.AR.Core;
using UnityEngine;

namespace Niantic.ARDK.AR.Scanning
{
    internal class ScanPaths
    {
        private const int StringSize = 512;

        internal static string GetBasePath(string basePath)
        {
            StringBuilder stringBuilder = new StringBuilder(StringSize);
                GetBasePath(basePath, stringBuilder, StringSize);
                return stringBuilder.ToString();
        }

        internal static string GetScanMetadataPath(string scanPath)
        {
            StringBuilder stringBuilder = new StringBuilder(StringSize);
            GetScanMetadataPath(scanPath, stringBuilder, StringSize);
            return stringBuilder.ToString();
        }

        internal static string GetScanFramesPath(string scanPath)
        {
            StringBuilder stringBuilder = new StringBuilder(StringSize);
            GetFramesPath(scanPath, stringBuilder, StringSize);
            return stringBuilder.ToString();
        }

        [DllImport(LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_Scanner_Get_Base_Path")]
        private static extern void GetBasePath(string rootPath, StringBuilder result, int length);

        [DllImport(LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_Scanner_Get_Frame_Data_Path")]
        private static extern void GetFramesPath(string scanPath, StringBuilder result, int length);

        [DllImport(LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_Scanner_Get_Scan_Metadata_Path")]
        private static extern void GetScanMetadataPath(string scanPath, StringBuilder result, int length);
    }
}
