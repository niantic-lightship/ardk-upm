// Copyright 2022-2024 Niantic.
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Niantic.Lightship.AR.Core;

namespace Niantic.Lightship.AR.Utilities.Profiling
{
    internal class CTraceProfiler : IProfiler
    {
        private Dictionary<string, ulong> _ids;
        private Random _random;

        public bool Initialize()
        {
            _ids = new Dictionary<string, ulong>();
            _random = new Random();

            return Native.Lightship_ARDK_Unity_InitializeCtrace();
        }

        public void Shutdown()
        {
            Native.Lightship_ARDK_Unity_ShutdownCtrace();
        }

        private ulong GetOrAddId(string category, string name)
        {
            var key = $"{category}|{name}";
            if (!_ids.TryGetValue(key, out ulong id))
            {
                var buffer = new byte[sizeof(ulong)];
                _random.NextBytes(buffer);
                id = BitConverter.ToUInt64(buffer, 0);
                _ids.Add(key, id);
            }

            return id;
        }

        public void EventBegin(string category, string name)
        {
            Native.Lightship_ARDK_Unity_TraceEventAsyncBegin0(category, name, GetOrAddId(category, name));
        }

        public void EventBegin(string category, string name, string arg1_name, string arg1_val)
        {
            var id = GetOrAddId(category, name);
            Native.Lightship_ARDK_Unity_TraceEventAsyncBegin1(category, name, id, arg1_name, arg1_val);
        }

        public void EventBegin
        (
            string category,
            string name,
            string arg1Name,
            string arg1Val,
            string arg2Name,
            string arg2Val
        )
        {
            var id = GetOrAddId(category, name);
            Native.Lightship_ARDK_Unity_TraceEventAsyncBegin2(category, name, id, arg1Name, arg1Val, arg2Name, arg2Val);
        }

        public void EventStep(string category, string name, string step)
        {
            var id = GetOrAddId(category, name);
            Native.Lightship_ARDK_Unity_TraceEventAsyncStep0(category, name, id, step);
        }

        public void EventStep(string category, string name, string step, string arg1_name, string arg1_val)
        {
            var id = GetOrAddId(category, name);
            Native.Lightship_ARDK_Unity_TraceEventAsyncStep1(category, name, id, step, arg1_name, arg1_val);
        }

        public void EventEnd(string category, string name)
        {
            var id = GetOrAddId(category, name);
            Native.Lightship_ARDK_Unity_TraceEventAsyncEnd0(category, name, id);
        }

        public void EventEnd(string category, string name, string arg1Name, string arg1Val)
        {
            var id = GetOrAddId(category, name);
            Native.Lightship_ARDK_Unity_TraceEventAsyncEnd1(category, name, id, arg1Name, arg1Val);
        }

        public void EventEnd
        (
            string category,
            string name,
            string arg1Name,
            string arg1Val,
            string arg2Name,
            string arg2Val
        )
        {
            var id = GetOrAddId(category, name);
            Native.Lightship_ARDK_Unity_TraceEventAsyncEnd2(category, name, id, arg1Name, arg1Val, arg2Name, arg2Val);
        }

        public void EventInstance(string category, string name)
        {
            Native.Lightship_ARDK_Unity_TraceEventInstance0(category, name);
        }

        public void EventInstance(string category, string name, string arg1Name, string arg1Val)
        {
            Native.Lightship_ARDK_Unity_TraceEventInstance1(category, name, arg1Name, arg1Val);
        }

        public void EventInstance
        (
            string category,
            string name,
            string arg1Name,
            string arg1Val,
            string arg2Name,
            string arg2Val
        )
        {
            Native.Lightship_ARDK_Unity_TraceEventInstance2(category, name, arg1Name, arg1Val, arg2Name, arg2Val);
        }

        private static class Native
        {
            [DllImport(LightshipPlugin.Name)]
            public static extern bool Lightship_ARDK_Unity_InitializeCtrace();

            [DllImport(LightshipPlugin.Name)]
            public static extern void Lightship_ARDK_Unity_ShutdownCtrace();

            [DllImport(LightshipPlugin.Name)]
            public static extern void Lightship_ARDK_Unity_TraceEventAsyncBegin0
            (
                string category,
                string name,
                UInt64 id
            );

            [DllImport(LightshipPlugin.Name)]
            public static extern void Lightship_ARDK_Unity_TraceEventAsyncBegin1
            (
                string category,
                string name,
                UInt64 id,
                string arg1Name,
                string arg1Val
            );

            [DllImport(LightshipPlugin.Name)]
            public static extern void Lightship_ARDK_Unity_TraceEventAsyncBegin2
            (
                string category,
                string name,
                UInt64 id,
                string arg1Name,
                string arg1Val,
                string arg2Name,
                string arg2Val
            );

            [DllImport(LightshipPlugin.Name)]
            public static extern void Lightship_ARDK_Unity_TraceEventAsyncStep0
            (
                string category,
                string name,
                UInt64 id,
                string step
            );

            [DllImport(LightshipPlugin.Name)]
            public static extern void Lightship_ARDK_Unity_TraceEventAsyncStep1
            (
                string category,
                string name,
                UInt64 id,
                string step,
                string arg1Name,
                string arg1Val
            );

            [DllImport(LightshipPlugin.Name)]
            public static extern void Lightship_ARDK_Unity_TraceEventAsyncEnd0(string category, string name, UInt64 id);

            [DllImport(LightshipPlugin.Name)]
            public static extern void Lightship_ARDK_Unity_TraceEventAsyncEnd1
            (
                string category,
                string name,
                UInt64 id,
                string arg1Name,
                string arg1Val
            );

            [DllImport(LightshipPlugin.Name)]
            public static extern void Lightship_ARDK_Unity_TraceEventAsyncEnd2
            (
                string category,
                string name,
                UInt64 id,
                string arg1Name,
                string arg1Val,
                string arg2Name,
                string arg2Val
            );

            [DllImport(LightshipPlugin.Name)]
            public static extern void Lightship_ARDK_Unity_TraceEventInstance0(string category, string name);

            [DllImport(LightshipPlugin.Name)]
            public static extern void Lightship_ARDK_Unity_TraceEventInstance1
            (
                string category,
                string name,
                string arg1Name,
                string arg1Val
            );

            [DllImport(LightshipPlugin.Name)]
            public static extern void Lightship_ARDK_Unity_TraceEventInstance2
            (
                string category,
                string name,
                string arg1Name,
                string arg1Val,
                string arg2Name,
                string arg2Val
            );
        }
    }
}
