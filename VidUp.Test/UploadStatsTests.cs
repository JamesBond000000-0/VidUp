using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Drexel.VidUp.Business;
using Drexel.VidUp.Utils;
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
        [OneTimeSetUp]
        public void Initialize()
        {
            //YoutubeAccount.ActiveRefreshToken reads the app settings, which the
            //main window creates in the app; tests have to provide it themselves
            if (Settings.Instance == null)
            {
                Settings.Instance = new Settings("Test", "UploadStatsTests");
            }
        }

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

        //BUG-28: a failed upload was credited with its full file length in the
        //total progress. Only the bytes actually sent may count.
        [Test]
        public void TotalProgressPercentage_FailedUpload_CountsOnlySentBytes()
        {
            string folder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                YoutubeAccount account = new YoutubeAccount();
                account.RefreshToken = "token";
                Upload uploadA = this.createSparseUpload(folder, "a.bin", 1000, account);
                Upload uploadB = this.createSparseUpload(folder, "b.bin", 1000, account);

                PlaylistList playlistList = new PlaylistList(new List<Playlist>());
                UploadList uploadList = new UploadList(
                    new List<Upload> { uploadA, uploadB },
                    new TemplateList(new List<Template>(), playlistList),
                    playlistList,
                    folder);

                UploadStats uploadStats = new UploadStats(new SemaphoreSlim(1, 1));
                uploadStats.Initialize(uploadList, false);

                uploadStats.NewUpload(uploadA);
                uploadA.BytesSent = 300;
                uploadStats.NewUpload(uploadB);

                //300 of 2000 bytes actually sent; the old code credited the
                //full 1000 bytes of the failed upload and showed 0.5
                Assert.That(uploadStats.TotalProgressPercentage, Is.EqualTo(0.15f).Within(0.001f));
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        [Test]
        public void TotalProgressPercentage_WithoutUploads_IsZeroNotNaN()
        {
            UploadStats uploadStats = new UploadStats(new SemaphoreSlim(1, 1));

            Assert.That(uploadStats.TotalProgressPercentage, Is.EqualTo(0f));
            Assert.That(uploadStats.CurrentFilePercent, Is.EqualTo(0));
            Assert.That(uploadStats.CurrentFileMbLeft, Is.EqualTo(0));
            Assert.That(uploadStats.CurrentFileTimeLeft, Is.Null);
        }

        [Test]
        public void CurrentFilePercent_ZeroByteFile_Is100NotNaN()
        {
            string filePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".bin");
            try
            {
                File.WriteAllBytes(filePath, new byte[0]);
                Upload upload = new Upload(filePath, new YoutubeAccount());
                UploadStats uploadStats = new UploadStats(new SemaphoreSlim(1, 1));
                uploadStats.NewUpload(upload);

                Assert.That(uploadStats.CurrentFilePercent, Is.EqualTo(100));
            }
            finally
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
        }

        private Upload createSparseUpload(string folder, string name, long length, YoutubeAccount account)
        {
            string filePath = Path.Combine(folder, name);
            using (FileStream stream = new FileStream(filePath, FileMode.Create, FileAccess.Write))
            {
                stream.SetLength(length);
            }

            return new Upload(filePath, account);
        }
    }
}
