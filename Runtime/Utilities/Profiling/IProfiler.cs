// Copyright 2022-2024 Niantic.
using System;
using System.Diagnostics;

namespace Niantic.Lightship.AR.Utilities.Profiling
{
    internal interface IProfiler
    {
        public bool Initialize();
        public void Shutdown();

        // synchronous events
        public void EventBegin(string category, string name);
        public void EventBegin(string category, string name, string arg1_name, string arg1_val);

        public void EventBegin
        (
            string category,
            string name,
            string arg1_name,
            string arg1_val,
            string arg2_name,
            string arg2_val
        );

        public void EventEnd(string category, string name);
        public void EventEnd(string category, string name, string arg1_name, string arg1_val);

        public void EventEnd
        (
            string category,
            string name,
            string arg1_name,
            string arg1_val,
            string arg2_name,
            string arg2_val
        );

        // asynchronous events
        public void EventAsyncBegin(string category, string name, ulong id);
        public void EventAsyncBegin(string category, string name, ulong id, string arg1_name, string arg1_val);

        public void EventAsyncBegin
        (
            string category,
            string name,
            ulong id,
            string arg1_name,
            string arg1_val,
            string arg2_name,
            string arg2_val
        );

        public void EventAsyncStep(string category, string name, ulong id, string step);
        public void EventAsyncStep(string category, string name, ulong id, string step, string arg1_name, string arg1_val);

        public void EventAsyncEnd(string category, string name, ulong id);
        public void EventAsyncEnd(string category, string name, ulong id, string arg1_name, string arg1_val);

        public void EventAsyncEnd
        (
            string category,
            string name,
            ulong id,
            string arg1_name,
            string arg1_val,
            string arg2_name,
            string arg2_val
        );

        // instantaneous events
        public void EventInstance(string category, string name);
        public void EventInstance(string category, string name, string arg1_name, string arg1_val);

        public void EventInstance
        (
            string category,
            string name,
            string arg1_name,
            string arg1_val,
            string arg2_name,
            string arg2_val
        );
    }
}
