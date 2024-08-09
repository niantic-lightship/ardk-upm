// Copyright 2022-2024 Niantic.
using System;

namespace Niantic.Lightship.AR.Utilities.Profiling
{
    internal class MockProfiler : IProfiler
    {
        public bool Initialize()
        {
            return false;
        }

        public void Shutdown()
        {
        }

        public void EventBegin(string category, string name)
        {
        }

        public void EventBegin(string category, string name, string arg1_name, string arg1_val)
        {
        }

        public void EventBegin(string category, string name, string arg1_name, string arg1_val, string arg2_name, string arg2_val)
        {
        }

        public void EventEnd(string category, string name)
        {
        }

        public void EventEnd(string category, string name, string arg1_name, string arg1_val)
        {
        }

        public void EventEnd(string category, string name, string arg1_name, string arg1_val, string arg2_name, string arg2_val)
        {
        }

        public void EventAsyncBegin(string category, string name, ulong id)
        {
        }

        public void EventAsyncBegin(string category, string name, ulong id, string arg1_name, string arg1_val)
        {
        }

        public void EventAsyncBegin(string category, string name, ulong id, string arg1_name, string arg1_val, string arg2_name, string arg2_val)
        {
        }

        public void EventAsyncStep(string category, string name, ulong id, string step)
        {
        }

        public void EventAsyncStep(string category, string name, ulong id, string step, string arg1_name, string arg1_val)
        {
        }

        public void EventAsyncEnd(string category, string name, ulong id)
        {
        }

        public void EventAsyncEnd(string category, string name, ulong id, string arg1_name, string arg1_val)
        {
        }

        public void EventAsyncEnd(string category, string name, ulong id, string arg1_name, string arg1_val, string arg2_name, string arg2_val)
        {
        }

        public void EventInstance(string category, string name)
        {
        }

        public void EventInstance(string category, string name, string arg1_name, string arg1_val)
        {
        }

        public void EventInstance(string category, string name, string arg1_name, string arg1_val, string arg2_name, string arg2_val)
        {
        }
    }
}
