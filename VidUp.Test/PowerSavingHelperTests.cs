using System.Reflection;
using Drexel.VidUp.UI.DllImport;
using NUnit.Framework;

namespace Drexel.VidUp.Test
{
    //Smoke test for BUG-02: disabling and re-enabling power saving must run
    //without errors (the calls log failures to the Tracer instead of ignoring
    //the return value silently). The thread affinity of the restore is handled
    //in UploadRibbonViewModel and covered by code review.
    [TestFixture]
    public class PowerSavingHelperTests
    {
        [Test]
        public void DisableAndEnable_DoNotThrow()
        {
            Assert.DoesNotThrow(() => PowerSavingHelper.DisablePowerSaving());

            //EnablePowerSaving is internal, call it via reflection
            MethodInfo enable = typeof(PowerSavingHelper).GetMethod("EnablePowerSaving", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(enable, Is.Not.Null);
            Assert.DoesNotThrow(() => enable.Invoke(null, null));
        }
    }
}
