// Copyright 2022-2024 Niantic.

using System;
using System.Runtime.InteropServices;
using Niantic.Lightship.AR.Core;
using AOT;

namespace Niantic.Lightship.AR.Utilities.DeterministicPlayback
{
    internal sealed class ComputeMonitor : IDisposable
    {
        public delegate void ComputeMonitorCallbackDelegate();

        private ComputeMonitorCallbackDelegate _callback;

        private IntPtr _componentManagerHandle;
        private IntPtr _nativeHandle;

        public ComputeMonitor(IntPtr unityContext)
        {
            var coreContext = LightshipUnityContext.GetCoreContext(unityContext);
            _componentManagerHandle = PAM.NativeApi.ARDK_CoreContext_GetComponentManagerHandle(coreContext);
            _nativeHandle = Native.ARDK_ComponentManager_ComputeMonitorHandle_Acquire(_componentManagerHandle);
        }

        ~ComputeMonitor()
        {
            Dispose();
        }

        [MonoPInvokeCallback(typeof(ComputeMonitorCallbackDelegate))]
        private void ComputeMonitorCallback()
        {
            // Check if the instance's callback is not null, then invoke it
            if (_callback != null) _callback.Invoke();
        }

        public void SetCallback(ComputeMonitorCallbackDelegate callbackDelegate)
        {
            _callback = callbackDelegate;
            Native.ARDK_ComputeMonitor_SetCallback(_componentManagerHandle, ComputeMonitorCallback);
        }

        public void Dispose()
        {
            if (!_nativeHandle.IsValidHandle()) return;
            Native.ARDK_ComponentManager_ComputeMonitorHandle_Release(_nativeHandle);
            _nativeHandle = IntPtr.Zero;
            GC.SuppressFinalize(this);
        }

        private static class Native
        {
            [DllImport(LightshipPlugin.Name)]
            public static extern IntPtr ARDK_ComponentManager_ComputeMonitorHandle_Acquire(
                IntPtr componentManagerHandle);

            [DllImport(LightshipPlugin.Name)]
            public static extern void ARDK_ComponentManager_ComputeMonitorHandle_Release(IntPtr computeMonitorHandle);

            [DllImport(LightshipPlugin.Name)]
            public static extern bool ARDK_ComputeMonitor_SetCallback(IntPtr componentManagerHandle,
                ComputeMonitorCallbackDelegate callbackDelegate);
        }
    }
}