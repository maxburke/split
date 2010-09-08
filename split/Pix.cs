#if !FINAL
#define ENABLE_PIX
#endif

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;

namespace Split
{
    class Pix
    {
#if (XBOX || !ENABLE_PIX)
        public static int BeginEvent(uint col, string wszName)
        {
            return 0;
        }

        public static int BeginEvent(string name)
        {
            return 0;
        }

        public static int EndEvent()
        {
            return 0;
        }

        public static void SetMarker(uint col, string wszName)
        {

        }
#else
        [System.Security.SuppressUnmanagedCodeSecurity]
        [DllImport("d3d9.dll",
            EntryPoint = "D3DPERF_BeginEvent",
            CharSet = CharSet.Auto,
            CallingConvention = CallingConvention.Winapi)]
        public static extern int BeginEvent(uint col, string wszName);

        public static int BeginEvent(string name)
        {
            return BeginEvent(0xffffffff, name);
        }

        [System.Security.SuppressUnmanagedCodeSecurity]
        [DllImport("d3d9.dll",
            EntryPoint = "D3DPERF_EndEvent",
            CallingConvention = CallingConvention.Winapi)]
        public static extern int EndEvent();

        [System.Security.SuppressUnmanagedCodeSecurity]
        [DllImport("d3d9.dll",
            EntryPoint = "D3DPERF_SetMarker",
            CharSet = CharSet.Auto,
            CallingConvention = CallingConvention.Winapi)]
        public static extern void SetMarker(uint col, string wszName);
#endif
    }
}