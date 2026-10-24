using System.IO;
using System.Threading;
using Drexel.VidUp.Business;
using Drexel.VidUp.Utils;
using Drexel.VidUp.Youtube;
using NUnit.Framework;

namespace Drexel.VidUp.Test
{
    //BUG-24: Constants.ByteMegaByteFactor was 1048567 instead of 1048576 (2^20),
    //which skewed every MB display in the app slightly.
    [TestFixture]
    public class ConstantsTests
    {
        [Test]
        public void ByteMegaByteFactor_IsTwoToThePowerOfTwenty()
        {
            Assert.That(Constants.ByteMegaByteFactor, Is.EqualTo(1024 * 1024));
        }

        [Test]
        public void CurrentFileMbLeft_UsesCorrectFactor()
        {
            //1073740808 bytes are 1024 MiB minus 768 bytes: with the correct
            //factor the display is 1023 MB, the typo factor showed 1024 MB
            string filePath = Path.Combine(Path.GetTempPath(), "vidup-mb-test.bin");
            try
            {
                using (FileStream stream = new FileStream(filePath, FileMode.Create, FileAccess.Write))
                {
                    stream.SetLength(1073740808L);
                }

                Upload upload = new Upload(filePath, new YoutubeAccount());
                UploadStats uploadStats = new UploadStats(new SemaphoreSlim(1, 1));
                uploadStats.NewUpload(upload);

                Assert.That(uploadStats.CurrentFileMbLeft, Is.EqualTo(1023));
            }
            finally
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
        }
    }
}
