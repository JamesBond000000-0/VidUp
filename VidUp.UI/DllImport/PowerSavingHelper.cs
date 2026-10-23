#region

using System.Runtime.InteropServices;
using Drexel.VidUp.Utils;

#endregion

namespace Drexel.VidUp.UI.DllImport
{
    public class PowerSavingHelper
    {
        //to prevent the computer going into sleep mode.
        //SetThreadExecutionState applies to the calling thread only, so disabling and
        //re-enabling must happen on the same thread (BUG-02).
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern ExecutionState SetThreadExecutionState(ExecutionState esFlags);

        public static void DisablePowerSaving()
        {
            ExecutionState previousState = SetThreadExecutionState(ExecutionState.EsContinous | ExecutionState.EsSystemRequired | ExecutionState.EsAwayModeRequired);
            if (previousState == 0)
            {
                //a return value of 0 (NULL) means the call failed, this was silently ignored before
                Tracer.Write($"PowerSavingHelper.DisablePowerSaving: SetThreadExecutionState failed, error code: {Marshal.GetLastWin32Error()}.");
            }
        }

        internal static void EnablePowerSaving()
        {
            ExecutionState previousState = SetThreadExecutionState(ExecutionState.EsContinous);
            if (previousState == 0)
            {
                Tracer.Write($"PowerSavingHelper.EnablePowerSaving: SetThreadExecutionState failed, error code: {Marshal.GetLastWin32Error()}.");
            }
        }
    }
}
