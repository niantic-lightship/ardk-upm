// Copyright 2022-2025 Niantic.

using System;
using System.Runtime.InteropServices;

using Niantic.Lightship.AR.Core;
using Niantic.Lightship.AR.XRSubsystems;

using UnityEngine;

namespace Niantic.Lightship.AR.Subsystems.WorldPositioning
{
    public class NativeApi : IApi
    {
        public IntPtr Construct(IntPtr unityContext)
        {
            return Native.Create(unityContext);
        }

        public void Start(IntPtr providerHandle)
        {
            Native.Start(providerHandle);
        }

        public void Stop(IntPtr providerHandle)
        {
            Native.Stop(providerHandle);
        }

        /// <summary>
        /// Defined in ardk_wps_configuration.h file.
        /// Note: We need this because passing configurations as arguments breaks down
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct WpsConfigurationCStruct
        {
            [MarshalAs(UnmanagedType.U1)]
            public bool smoothingEnabled;

            public int framerate;

            [MarshalAs(UnmanagedType.U1)]
            public bool bevEnabled;

            public int bevFramerate;
        }

        public void Configure(IntPtr providerHandle, int framerate, bool smoothing_enabled, bool bev_enabled, int bev_framerate)
        {
            var wpsConfiguration = new WpsConfigurationCStruct();
            wpsConfiguration.smoothingEnabled = smoothing_enabled;
            wpsConfiguration.framerate = framerate;
            wpsConfiguration.bevEnabled = bev_enabled;
            wpsConfiguration.bevFramerate = bev_framerate;
            Native.Configure(providerHandle, wpsConfiguration);
        }

        public void Destruct(IntPtr providerHandle)
        {
            Native.Release(providerHandle);
        }

        public WorldPositioningStatus TryGetXRToWorld
        (
            IntPtr providerHandle,
            out Matrix4x4 arToWorld,
            out double originLatitude,
            out double originLongitude,
            out double originAltitude
        )
        {
            float[] poseArray = new float[16];

            WorldPositioningStatus status = (WorldPositioningStatus)Native.GetLatestTransform
            (
                providerHandle,
                poseArray,
                out originLatitude,
                out originLongitude,
                out originAltitude
            );

            if (status == WorldPositioningStatus.Available)
            {
                arToWorld = Matrix4x4.zero;

                for (int col = 0; col < 4; col++)
                {
                    for (int row = 0; row < 4; row++)
                    {
                        arToWorld[row, col] = poseArray[row + (col * 4)];
                    }
                }
            }
            else
            {
                arToWorld = Matrix4x4.identity;
            }

            return status;
        }

        private static class Native
        {
            [DllImport(LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_WPS_Create")]
            public static extern IntPtr Create(IntPtr unity_context);

            // Release WPS Feature
            [DllImport(LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_WPS_Release")]
            public static extern void Release(IntPtr feature_handle);

            // Start WPS data processing
            [DllImport(LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_WPS_Start")]
            public static extern void Start(IntPtr feature_handle);

            // Stop WPS data processing
            [DllImport(LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_WPS_Stop")]
            public static extern void Stop(IntPtr feature_handle);

            // Configure WPS
            [DllImport(LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_WPS_Configure")]
            public static extern void Configure(IntPtr handle, WpsConfigurationCStruct config);

            [DllImport
                (LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_WPS_GetLatestTransform")]
            public static extern uint GetLatestTransform
            (
                IntPtr handle,
                [Out] float[] transform_out,
                out double origin_lat_out,
                out double origin_lon_out,
                out double origin_alt_out
            );
        }
    }
}
