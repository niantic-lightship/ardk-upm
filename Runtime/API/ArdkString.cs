using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Niantic.Lightship.AR.API
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct ArdkString
    {
        public IntPtr data;
        public UInt32 length;
    }

    internal class ManagedArdkString : IDisposable
    {
        private readonly IntPtr _data;
        private readonly uint _length;
        private bool _disposed;

        public ManagedArdkString(string str)
        {
            if (string.IsNullOrEmpty(str))
            {
                _data = IntPtr.Zero;
                _length = 0;
            }
            else
            {
                byte[] bytes = Encoding.UTF8.GetBytes(str);
                _data = Marshal.AllocHGlobal(bytes.Length);
                Marshal.Copy(bytes, 0, _data, bytes.Length);
                _length = (uint)bytes.Length;
            }
        }

        void IDisposable.Dispose()
        {
            Dispose();
            GC.SuppressFinalize(this);
        }

        ~ManagedArdkString()
        {
            Dispose();
        }

        public ArdkString ToArdkString() => new() { data = _data, length = _length };

        private void Dispose()
        {
            if (!_disposed)
            {
                if (_data != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(_data);
                }

                _disposed = true;
            }
        }
    }
}
