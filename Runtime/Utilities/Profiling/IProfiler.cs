// Copyright 2022-2023 Niantic.
using System;
using System.Diagnostics;

namespace Niantic.Lightship.AR.Utilities.Profiling
{
    internal interface IProfiler
    {
        public bool Initialize();
        public void Shutdown();

        public void EventBegin(string category, string name);
        public void EventBegin(string category, string name, string arg1_name, UInt64 arg1_val);

        public void EventBegin
        (
            string category,
            string name,
            string arg1_name,
            UInt64 arg1_val,
            string arg2_name,
            UInt64 arg2_val
        );

        public void EventStep(string category, string name, string step);

        public void EventStep(string category, string name, string step, string arg1_name, UInt64 arg1_val);

        public void EventEnd(string category, string name);
        public void EventEnd(string category, string name, string arg1_name, UInt64 arg1_val);

        public void EventEnd
        (
            string category,
            string name,
            string arg1_name,
            UInt64 arg1_val,
            string arg2_name,
            UInt64 arg2_val
        );

        public void EventInstance(string category, string name);
        public void EventInstance(string category, string name, string arg1_name, UInt64 arg1_val);

        public void EventInstance
        (
            string category,
            string name,
            string arg1_name,
            UInt64 arg1_val,
            string arg2_name,
            UInt64 arg2_val
        );
    }
}
