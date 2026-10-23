using System;
using System.Threading;
using Drexel.VidUp.Youtube;
using NUnit.Framework;

namespace Drexel.VidUp.Test
{
    //Tests for BUG-06 / BUG-09: UploadStats.Update must always release the stats
    //gate again, even when the update itself throws, or every gate wait in the
    //upload service blocks forever. The gate was an AutoResetEvent before
    //BUG-09 and is a SemaphoreSlim now.
    [TestFixture]
    public class UploadStatsTests
    {
        [Test]
        public void Update_WhenUpdateThrows_StillReleasesGate()
        {
            SemaphoreSlim statsGate = new SemaphoreSlim(1, 1);
            UploadStats uploadStats = new UploadStats(statsGate);

            //no Initialize call, so the update fails on the missing upload list
            Assert.Throws<NullReferenceException>(() => uploadStats.Update());

            //the Wait at the start acquired the gate, only the finally Release
            //can have returned it
            Assert.That(statsGate.Wait(0), Is.True, "The stats gate was not released after the failed update.");
            statsGate.Release();
        }
    }
}
