using System;
using System.Net;
using Drexel.VidUp.Youtube.VideoUploadService.Data;
using Newtonsoft.Json;

namespace Drexel.VidUp.Youtube.VideoUploadService
{
    public enum ResumeCheckResult
    {
        Failed,
        Resume,
        Completed
    }

    //Classifies the answer to a resumable upload status query (BUG-05).
    //Kept free of HTTP and upload state so the classification can be unit tested:
    //308 means "upload incomplete, resume at the reported range", while 200/201 mean
    //"the server already received the whole file" and carry the video resource.
    public static class ResumePositionEvaluator
    {
        public static ResumeCheckResult Evaluate(HttpStatusCode statusCode, string rangeHeader, string content, long fileLength, out long bytesSent, out string videoId)
        {
            bytesSent = 0;
            videoId = null;

            if (statusCode == HttpStatusCode.OK || statusCode == HttpStatusCode.Created)
            {
                YoutubeVideoPostResponse response = null;
                try
                {
                    response = JsonConvert.DeserializeObject<YoutubeVideoPostResponse>(content);
                }
                catch (Exception)
                {
                    //not a video resource, handled as a failed query below
                }

                if (response != null && !string.IsNullOrWhiteSpace(response.Id))
                {
                    videoId = response.Id;
                    bytesSent = fileLength;
                    return ResumeCheckResult.Completed;
                }

                return ResumeCheckResult.Failed;
            }

            if (statusCode == HttpStatusCode.PermanentRedirect)
            {
                if (!string.IsNullOrWhiteSpace(rangeHeader))
                {
                    string[] parts = rangeHeader.Split('-');
                    long lastReceivedByte;
                    if (parts.Length == 2 && long.TryParse(parts[1], out lastReceivedByte))
                    {
                        bytesSent = lastReceivedByte + 1;
                    }
                }

                return ResumeCheckResult.Resume;
            }

            return ResumeCheckResult.Failed;
        }
    }
}
