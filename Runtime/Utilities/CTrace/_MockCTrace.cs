using System;

namespace Niantic.Lightship.AR.Utilities.CTrace
{
    internal class _MockCTrace : _ICTrace
    {
        public bool InitializeCtrace()
        {
            return false;
        }

        public void ShutdownCtrace()
        {
        }

        public void TraceEventAsyncBegin0(string category, string name, UInt64 id)
        {
        }

        public void TraceEventAsyncBegin1(string category, string name, UInt64 id, string arg1_name, UInt64 arg1_val)
        {
        }

        public void TraceEventAsyncBegin2(string category, string name, UInt64 id, string arg1_name, UInt64 arg1_val,
            string arg2_name, UInt64 arg2_val)
        {
        }

        public void TraceEventAsyncStep0(string category, string name, UInt64 id, string step)
        {
        }

        public void TraceEventAsyncStep1(string category, string name, UInt64 id, string step, string arg1_name,
            UInt64 arg1_val)
        {
        }

        public void TraceEventAsyncEnd0(string category, string name, UInt64 id)
        {
        }

        public void TraceEventAsyncEnd1(string category, string name, UInt64 id, string arg1_name, UInt64 arg1_val)
        {
        }

        public void TraceEventAsyncEnd2(string category, string name, UInt64 id, string arg1_name, UInt64 arg1_val,
            string arg2_name, UInt64 arg2_val)
        {
        }

        public void TraceEventInstance0(string category, string name)
        {
        }

        public void TraceEventInstance1(string category, string name, string arg1_name, UInt64 arg1_val)
        {
        }

        public void TraceEventInstance2(string category, string name, string arg1_name, UInt64 arg1_val,
            string arg2_name, UInt64 arg2_val)
        {
        }
    }
}
