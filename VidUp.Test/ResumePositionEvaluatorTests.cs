using System.Net;
using Drexel.VidUp.Youtube.VideoUploadService;
using NUnit.Framework;

namespace Drexel.VidUp.Test
{
    //Tests for BUG-05: a status query for a resumable upload must treat 200/201
    //as "already uploaded" instead of failing the upload and risking a duplicate.
    [TestFixture]
    public class ResumePositionEvaluatorTests
    {
        [Test]
        public void Redirect308_WithRange_ResumesAfterLastReceivedByte()
        {
            ResumeCheckResult result = ResumePositionEvaluator.Evaluate(
                HttpStatusCode.PermanentRedirect, "bytes=0-999", null, 5000, out long bytesSent, out string videoId);

            Assert.That(result, Is.EqualTo(ResumeCheckResult.Resume));
            Assert.That(bytesSent, Is.EqualTo(1000));
            Assert.That(videoId, Is.Null);
        }

        [Test]
        public void Redirect308_WithoutRange_ResumesFromBeginning()
        {
            ResumeCheckResult result = ResumePositionEvaluator.Evaluate(
                HttpStatusCode.PermanentRedirect, null, null, 5000, out long bytesSent, out string videoId);

            Assert.That(result, Is.EqualTo(ResumeCheckResult.Resume));
            Assert.That(bytesSent, Is.EqualTo(0));
            Assert.That(videoId, Is.Null);
        }

        [Test]
        public void Redirect308_WithMalformedRange_ResumesFromBeginning()
        {
            ResumeCheckResult result = ResumePositionEvaluator.Evaluate(
                HttpStatusCode.PermanentRedirect, "garbage", null, 5000, out long bytesSent, out string videoId);

            Assert.That(result, Is.EqualTo(ResumeCheckResult.Resume));
            Assert.That(bytesSent, Is.EqualTo(0));
        }

        [Test]
        public void Ok200_WithVideoResource_IsCompleted()
        {
            ResumeCheckResult result = ResumePositionEvaluator.Evaluate(
                HttpStatusCode.OK, null, "{\"id\": \"abc123\"}", 5000, out long bytesSent, out string videoId);

            Assert.That(result, Is.EqualTo(ResumeCheckResult.Completed));
            Assert.That(bytesSent, Is.EqualTo(5000));
            Assert.That(videoId, Is.EqualTo("abc123"));
        }

        [Test]
        public void Created201_WithVideoResource_IsCompleted()
        {
            ResumeCheckResult result = ResumePositionEvaluator.Evaluate(
                HttpStatusCode.Created, null, "{\"id\": \"xyz789\"}", 5000, out long bytesSent, out string videoId);

            Assert.That(result, Is.EqualTo(ResumeCheckResult.Completed));
            Assert.That(bytesSent, Is.EqualTo(5000));
            Assert.That(videoId, Is.EqualTo("xyz789"));
        }

        [Test]
        public void Ok200_WithoutVideoId_IsFailed()
        {
            ResumeCheckResult result = ResumePositionEvaluator.Evaluate(
                HttpStatusCode.OK, null, "{}", 5000, out long bytesSent, out string videoId);

            Assert.That(result, Is.EqualTo(ResumeCheckResult.Failed));
            Assert.That(videoId, Is.Null);
        }

        [Test]
        public void Ok200_WithMalformedContent_IsFailed()
        {
            ResumeCheckResult result = ResumePositionEvaluator.Evaluate(
                HttpStatusCode.OK, null, "not json", 5000, out long bytesSent, out string videoId);

            Assert.That(result, Is.EqualTo(ResumeCheckResult.Failed));
        }

        [Test]
        public void ErrorStatus_IsFailed()
        {
            ResumeCheckResult result = ResumePositionEvaluator.Evaluate(
                HttpStatusCode.NotFound, null, "session gone", 5000, out long bytesSent, out string videoId);

            Assert.That(result, Is.EqualTo(ResumeCheckResult.Failed));
        }
    }
}
