using System;
using System.IO;
using Drexel.VidUp.Business;
using NUnit.Framework;

namespace Drexel.VidUp.Test
{
    //BUG-26: VerifyForUpload read the fileLength field directly, which is only
    //filled by the lazy initialization in the FileLength getter, so the size
    //check compared 0 and never rejected an oversized file.
    //BUG-27: SetPublishAtTime dereferenced PublishAt.Value unconditionally and
    //threw a NullReferenceException for uploads without a publish date.
    [TestFixture]
    public class UploadVerifyTests
    {
        private string filePath;

        [SetUp]
        public void SetUp()
        {
            this.filePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".bin");
        }

        [TearDown]
        public void TearDown()
        {
            if (File.Exists(this.filePath))
            {
                File.Delete(this.filePath);
            }
        }

        private Upload createUpload(long length)
        {
            using (FileStream stream = new FileStream(this.filePath, FileMode.Create, FileAccess.Write))
            {
                stream.SetLength(length);
            }

            return new Upload(this.filePath, new YoutubeAccount());
        }

        [Test]
        public void VerifyForUpload_OversizedFile_ReturnsFalse()
        {
            Upload upload = this.createUpload(YoutubeLimits.FileSizeLimit + 1);

            Assert.That(upload.VerifyForUpload(), Is.False);
        }

        [Test]
        public void VerifyForUpload_SmallFile_ReturnsTrue()
        {
            Upload upload = this.createUpload(1024);

            Assert.That(upload.VerifyForUpload(), Is.True);
        }

        [Test]
        public void SetPublishAtTime_WithoutPublishAt_DoesNotThrowAndStaysNull()
        {
            Upload upload = this.createUpload(1024);

            Assert.That(upload.PublishAt, Is.Null);
            Assert.DoesNotThrow(() => upload.SetPublishAtTime(new TimeSpan(10, 30, 0)));
            Assert.That(upload.PublishAt, Is.Null);
        }

        [Test]
        public void SetPublishAtTime_WithPublishAt_SetsTimeKeepsDate()
        {
            Upload upload = this.createUpload(1024);
            upload.PublishAt = new DateTime(2026, 3, 4, 0, 0, 0);

            upload.SetPublishAtTime(new TimeSpan(10, 30, 0));

            Assert.That(upload.PublishAt, Is.EqualTo(new DateTime(2026, 3, 4, 10, 30, 0)));
        }
    }
}
