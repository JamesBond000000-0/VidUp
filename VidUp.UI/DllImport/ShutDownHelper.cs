using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Drexel.VidUp.Utils;

namespace Drexel.VidUp.UI.DllImport
{
    class ShutDownHelper
    {

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct TokPriv1Luid
        {
            public int Count;
            public long Luid;
            public int Attr;
        }

        [DllImport("kernel32.dll", ExactSpelling = true)]
        private static extern IntPtr GetCurrentProcess();

        [DllImport("advapi32.dll", ExactSpelling = true, SetLastError = true)]
        private static extern bool OpenProcessToken(IntPtr h, int acc, ref IntPtr phtok);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool LookupPrivilegeValue(string host, string name, ref long pluid);

        [DllImport("advapi32.dll", ExactSpelling = true, SetLastError = true)]
        private static extern bool AdjustTokenPrivileges(IntPtr htok, bool disall,
        ref TokPriv1Luid newst, int len, IntPtr prev, IntPtr relen);

        [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
        private static extern bool ExitWindowsEx(ExitWindows flg, ShutdownReason rea);

        [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        private const int SE_PRIVILEGE_ENABLED = 0x00000002;
        private const int TOKEN_QUERY = 0x00000008;
        private const int TOKEN_ADJUST_PRIVILEGES = 0x00000020;
        private const string SE_SHUTDOWN_NAME = "SeShutdownPrivilege";

        public static void ExitWin(ExitWindows exitWindows, ShutdownReason reason)
        {
            //every step's result is checked and failures are traced: previously
            //all return values were ignored, so a missing shutdown privilege
            //made the post upload shutdown silently do nothing, and the token
            //handle was never closed (BUG-31)
            IntPtr htok = IntPtr.Zero;
            try
            {
                IntPtr hproc = GetCurrentProcess();
                if (!OpenProcessToken(hproc, TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, ref htok))
                {
                    ShutDownHelper.logFailure(nameof(OpenProcessToken));
                    return;
                }

                TokPriv1Luid tp;
                tp.Count = 1;
                tp.Luid = 0;
                tp.Attr = SE_PRIVILEGE_ENABLED;
                if (!LookupPrivilegeValue(null, SE_SHUTDOWN_NAME, ref tp.Luid))
                {
                    ShutDownHelper.logFailure(nameof(LookupPrivilegeValue));
                    return;
                }

                //AdjustTokenPrivileges also returns true when the privilege
                //could not be assigned; only error 0 means full success
                if (!AdjustTokenPrivileges(htok, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero) || Marshal.GetLastWin32Error() != 0)
                {
                    ShutDownHelper.logFailure(nameof(AdjustTokenPrivileges));
                    return;
                }

                if (!ExitWindowsEx(exitWindows, reason))
                {
                    ShutDownHelper.logFailure(nameof(ExitWindowsEx));
                }
            }
            finally
            {
                if (htok != IntPtr.Zero)
                {
                    CloseHandle(htok);
                }
            }
        }

        private static void logFailure(string apiName)
        {
            Win32Exception error = new Win32Exception(Marshal.GetLastWin32Error());
            Tracer.Write($"ShutDownHelper.ExitWin: {apiName} failed: {error.Message} The shutdown will not happen.");
        }
    }
}
