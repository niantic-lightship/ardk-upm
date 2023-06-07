using System;
using System.Runtime.InteropServices;

namespace Niantic.Lightship.AR.Utilities.CTrace
{
    internal class _NativeCTrace : _ICTrace
    {
        public bool InitializeCtrace()
        {
            return Native.Lightship_ARDK_Unity_InitializeCtrace();
        }

        public void ShutdownCtrace()
        {
            Native.Lightship_ARDK_Unity_ShutdownCtrace();
        }

        public void TraceEventAsyncBegin0(string category, string name, UInt64 id)
        {
            Native.Lightship_ARDK_Unity_TraceEventAsyncBegin0(category, name, id);
        }

        public void TraceEventAsyncBegin1(string category, string name, UInt64 id, string arg1_name, UInt64 arg1_val)
        {
            Native.Lightship_ARDK_Unity_TraceEventAsyncBegin1(category, name, id, arg1_name, arg1_val);
        }

        public void TraceEventAsyncBegin2(string category, string name, UInt64 id, string arg1_name, UInt64 arg1_val,
            string arg2_name, UInt64 arg2_val)
        {
            Native.Lightship_ARDK_Unity_TraceEventAsyncBegin2(category, name, id, arg1_name, arg1_val, arg2_name,
                arg2_val);
        }

        public void TraceEventAsyncStep0(string category, string name, UInt64 id, string step)
        {
            Native.Lightship_ARDK_Unity_TraceEventAsyncStep0(category, name, id, step);
        }

        public void TraceEventAsyncStep1(string category, string name, UInt64 id, string step, string arg1_name,
            UInt64 arg1_val)
        {
            Native.Lightship_ARDK_Unity_TraceEventAsyncStep1(category, name, id, step, arg1_name, arg1_val);
        }

        public void TraceEventAsyncEnd0(string category, string name, UInt64 id)
        {
            Native.Lightship_ARDK_Unity_TraceEventAsyncEnd0(category, name, id);
        }

        public void TraceEventAsyncEnd1(string category, string name, UInt64 id, string arg1_name, UInt64 arg1_val)
        {
            Native.Lightship_ARDK_Unity_TraceEventAsyncEnd1(category, name, id, arg1_name, arg1_val);
        }

        public void TraceEventAsyncEnd2(string category, string name, UInt64 id, string arg1_name, UInt64 arg1_val,
            string arg2_name, UInt64 arg2_val)
        {
            Native.Lightship_ARDK_Unity_TraceEventAsyncEnd2(category, name, id, arg1_name, arg1_val, arg2_name,
                arg2_val);
        }

        public void TraceEventInstance0(string category, string name)
        {
            Native.Lightship_ARDK_Unity_TraceEventInstance0(category, name);
        }

        public void TraceEventInstance1(string category, string name, string arg1_name, UInt64 arg1_val)
        {
            Native.Lightship_ARDK_Unity_TraceEventInstance1(category, name, arg1_name, arg1_val);
        }

        public void TraceEventInstance2(string category, string name, string arg1_name, UInt64 arg1_val,
            string arg2_name, UInt64 arg2_val)
        {
            Native.Lightship_ARDK_Unity_TraceEventInstance2(category, name, arg1_name, arg1_val, arg2_name, arg2_val);
        }

        private static class Native
        {
            [DllImport(_LightshipPlugin.Name)]
            public static extern bool Lightship_ARDK_Unity_InitializeCtrace();

            [DllImport(_LightshipPlugin.Name)]
            public static extern void Lightship_ARDK_Unity_ShutdownCtrace();

            [DllImport(_LightshipPlugin.Name)]
            public static extern void Lightship_ARDK_Unity_TraceEventAsyncBegin0(string category, string name,
                UInt64 id);

            [DllImport(_LightshipPlugin.Name)]
            public static extern void Lightship_ARDK_Unity_TraceEventAsyncBegin1(string category, string name,
                UInt64 id, string arg1_name, UInt64 arg1_val);

            [DllImport(_LightshipPlugin.Name)]
            public static extern void Lightship_ARDK_Unity_TraceEventAsyncBegin2(string category, string name,
                UInt64 id, string arg1_name, UInt64 arg1_val, string arg2_name, UInt64 arg2_val);

            [DllImport(_LightshipPlugin.Name)]
            public static extern void Lightship_ARDK_Unity_TraceEventAsyncStep0(string category, string name, UInt64 id,
                string step);

            [DllImport(_LightshipPlugin.Name)]
            public static extern void Lightship_ARDK_Unity_TraceEventAsyncStep1(string category, string name, UInt64 id,
                string step, string arg1_name, UInt64 arg1_val);

            [DllImport(_LightshipPlugin.Name)]
            public static extern void Lightship_ARDK_Unity_TraceEventAsyncEnd0(string category, string name, UInt64 id);

            [DllImport(_LightshipPlugin.Name)]
            public static extern void Lightship_ARDK_Unity_TraceEventAsyncEnd1(string category, string name, UInt64 id,
                string arg1_name, UInt64 arg1_val);

            [DllImport(_LightshipPlugin.Name)]
            public static extern void Lightship_ARDK_Unity_TraceEventAsyncEnd2(string category, string name, UInt64 id,
                string arg1_name, UInt64 arg1_val, string arg2_name, UInt64 arg2_val);

            [DllImport(_LightshipPlugin.Name)]
            public static extern void Lightship_ARDK_Unity_TraceEventInstance0(string category, string name);

            [DllImport(_LightshipPlugin.Name)]
            public static extern void Lightship_ARDK_Unity_TraceEventInstance1(string category, string name,
                string arg1_name, UInt64 arg1_val);

            [DllImport(_LightshipPlugin.Name)]
            public static extern void Lightship_ARDK_Unity_TraceEventInstance2(string category, string name,
                string arg1_name, UInt64 arg1_val, string arg2_name, UInt64 arg2_val);
        }
    }
}
