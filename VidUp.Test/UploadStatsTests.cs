using System;
using System.Threading;
using Drexel.VidUp.Youtube;
using NUnit.Framework;

namespace Drexel.VidUp.Test
{
    //Tests for BUG-06: UploadStats.Update must always set the reset event again,
    //even when the update itself throws, or every WaitOne in the upload service
    //blocks forever.
    [TestFixture]
    public class UploadStatsTests
    {
        [Test]
        public void Update_WhenUpdateThrows_StillSetsResetEvent()
        {
            AutoResetEvent resetEvent = new AutoResetEvent(true);
            UploadStats uploadStats = new UploadStats(resetEvent);

            //no Initialize call, so the update fails on the missing upload list
            Assert.Throws<NullReferenceException>(() => uploadStats.Update());

            //the WaitOne at the start consumed the initial signal, only the
            //finally Set can have signaled the event again
            Assert.That(resetEvent.WaitOne(0), Is.True, "The reset event was not set again after the failed update.");
        }
    }
}
