using System;
using System.Runtime.InteropServices;

namespace Niantic.Lightship.AR.Subsystems
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct ARDK_String
    {
        public IntPtr data;
        public UInt32 length;
    }

    internal static class ARDK_StringExtensions
    {
        public static ARDK_String ToARDKString(string str)
        {
            if (string.IsNullOrEmpty(str))
            {
                return new ARDK_String { data = IntPtr.Zero, length = 0 };
            }

            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(str);
            IntPtr ptr = Marshal.AllocHGlobal(bytes.Length);
            Marshal.Copy(bytes, 0, ptr, bytes.Length);

            return new ARDK_String { data = ptr, length = (uint)bytes.Length };
        }

        public static void Free(ARDK_String str)
        {
            if (str.data != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(str.data);
            }
        }
    }
}
