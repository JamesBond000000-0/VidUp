using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using Drexel.VidUp.Business;
using Drexel.VidUp.Utils;
using Drexel.VidUp.Youtube.AuthenticationService;

namespace Drexel.VidUp.Youtube.Http
{
    public static class HttpHelper
    {
        private static HttpClient standardClient;
        private static HttpClient uploadClient;

        //accessed from parallel flows (upload, playlist retrieval, settings), so the
        //storage is concurrent and the check-and-fetch in ensureAccessTokenAsync is
        //serialized with a semaphore (BUG-16: the plain Dictionary raced between
        //check and store and could corrupt or throw KeyNotFoundException)
        private static ConcurrentDictionary<YoutubeAccount, AccessToken> youtubeAccessTokenExpiryByAccount = new ConcurrentDictionary<YoutubeAccount, AccessToken>();
        private static SemaphoreSlim accessTokenSemaphore = new SemaphoreSlim(1, 1);
        private static TimeSpan oneMinute = new TimeSpan(0, 1, 0);

        //buffer is set to constant value like 1024, 4096, 16384 etc.
        //eg. if set to 10240 buffer will be 16384.
        //default package requested by http client on Stream.Read ist 128kByte.
        private static int minBufferSize = 32 * 1024;

        static HttpHelper()
        {
            HttpHelper.standardClient  = new HttpClient();

            HttpHelper.uploadClient = new HttpClient();
            HttpHelper.uploadClient.Timeout = Timeout.InfiniteTimeSpan;
        }

        public static HttpClient StandardClient
        {
            get => HttpHelper.standardClient;
        }

        public static HttpClient UploadClient
        {
            get => HttpHelper.uploadClient;
        }

        public static void ClearAccessTokens()
        {
            HttpHelper.youtubeAccessTokenExpiryByAccount.Clear();
        }

        public static void RemoveAccessToken(YoutubeAccount youtubeAccount)
        {
            if (youtubeAccount != null)
            {
                AccessToken removedToken;
                HttpHelper.youtubeAccessTokenExpiryByAccount.TryRemove(youtubeAccount, out removedToken);
            }
        }

        public static async Task<HttpRequestMessage> GetAuthenticatedRequestMessageAsync(YoutubeAccount youtubeAccount, HttpMethod method, string uri)
        {
            Tracer.Write($"HttpHelper.GetAuthenticatedRequestMessageAsync: Start with uri.");
            AccessToken accessToken = await HttpHelper.ensureAccessTokenAsync(youtubeAccount).ConfigureAwait(false);
            HttpRequestMessage message = new HttpRequestMessage(method, uri);
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken.Token);
            Tracer.Write($"HttpHelper.GetAuthenticatedRequestMessageAsync: End with uri.");
            return message;
        }

        public static async Task<HttpRequestMessage> GetAuthenticatedRequestMessageAsync(YoutubeAccount youtubeAccount, HttpMethod method)
        {
            Tracer.Write($"HttpHelper.GetAuthenticatedRequestMessageAsync: Start.");
            AccessToken accessToken = await HttpHelper.ensureAccessTokenAsync(youtubeAccount).ConfigureAwait(false);
            HttpRequestMessage message = new HttpRequestMessage();
            message.Method = method;
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken.Token);
            Tracer.Write($"HttpHelper.GetAuthenticatedRequestMessageAsync: End.");
            return message;
        }

        //Returns a valid access token for the account, fetching and caching a new
        //one when the cached token is missing or about to expire. The check, the
        //fetch and the store run serialized, so parallel callers neither fetch
        //twice nor observe a half-updated cache (BUG-16).
        private static async Task<AccessToken> ensureAccessTokenAsync(YoutubeAccount youtubeAccount)
        {
            Tracer.Write($"HttpHelper.ensureAccessTokenAsync: Start, checking for access token.");
            await HttpHelper.accessTokenSemaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                AccessToken accessToken;
                if (!HttpHelper.youtubeAccessTokenExpiryByAccount.TryGetValue(youtubeAccount, out accessToken) || accessToken.Expiry - DateTime.Now < oneMinute)
                {
                    Tracer.Write($"HttpHelper.ensureAccessTokenAsync: No access token found, requesting access token.");
                    accessToken = await YoutubeAuthentication.GetNewAccessTokenAsync(youtubeAccount).ConfigureAwait(false);
                    HttpHelper.youtubeAccessTokenExpiryByAccount[youtubeAccount] = accessToken;
                }

                Tracer.Write($"HttpHelper.ensureAccessTokenAsync: End.");
                return accessToken;
            }
            finally
            {
                HttpHelper.accessTokenSemaphore.Release();
            }
        }

        public static StreamContent GetStreamContentContentRangeOnly(long length)
        {
            Tracer.Write($"HttpHelper.GetStreamContentContentRangeOnly: Start.");
            StreamContent content = new StreamContent(Stream.Null);
            content.Headers.ContentLength = 0;
            content.Headers.Add("Content-Range", string.Format("bytes */{0}", length.ToString()));

            Tracer.Write($"HttpHelper.GetStreamContentContentRangeOnly: End.");
            return content;
        }

        public static ByteArrayContent GetStreamContent(string content, string contentType)
        {
            Tracer.Write($"HttpHelper.GetStreamContent: Start.");
            if (string.IsNullOrWhiteSpace(contentType))
            {
                Tracer.Write($"HttpHelper.GetStreamContent: End.");
                throw new ArgumentException("contentType");
            }

            byte[] bytes = content == null ? new byte[0] : Encoding.UTF8.GetBytes(content);
            ByteArrayContent streamContent = new ByteArrayContent(bytes);
            streamContent.Headers.ContentLength = bytes.Length;
            streamContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);

            Tracer.Write($"HttpHelper.GetStreamContent: End.");
            return streamContent;
        }

        public static StreamContent GetStreamContentResumableUpload(Stream data, long orirginalLength, long startByteIndex, string contentType)
        {
            int bufferSize = HttpHelper.minBufferSize;
            if (Settings.Instance.UserSettings.NetworkPackageSizeInBytes > bufferSize)
            {
                bufferSize = Settings.Instance.UserSettings.NetworkPackageSizeInBytes;
            }

            Tracer.Write($"HttpHelper.GetStreamContentResumableUpload: Start.");
            Tracer.Write($"HttpHelper.GetStreamContentResumableUpload: StreamContent buffer size: {bufferSize}.");
            StreamContent content = new StreamContent(data, bufferSize);
            content.Headers.ContentType = new MediaTypeHeaderValue(contentType);

            if (startByteIndex <= 0)
            {
                content.Headers.ContentLength = orirginalLength;
            }
            else
            {
                long lastByteIndex = orirginalLength - 1;
                string rangeString = string.Format("bytes {0}-{1}/{2}", startByteIndex, lastByteIndex, orirginalLength);
                content.Headers.ContentLength = lastByteIndex - startByteIndex + 1;
                content.Headers.Add("Content-Range", rangeString);
            }

            Tracer.Write($"HttpHelper.GetStreamContentResumableUpload: End.");
            return content;
        }

        public static StreamContent GetStreamContentUpload(Stream data, string contentType)
        {
            int bufferSize = HttpHelper.minBufferSize;
            if (Settings.Instance.UserSettings.NetworkPackageSizeInBytes > bufferSize)
            {
                bufferSize = Settings.Instance.UserSettings.NetworkPackageSizeInBytes;
            }

            Tracer.Write($"HttpHelper.GetStreamContentUpload: Start.");
            Tracer.Write($"HttpHelper.GetStreamContentUpload: StreamContent buffer size: {bufferSize}.");
            StreamContent content = new StreamContent(data, bufferSize);
            content.Headers.ContentLength = data.Length;
            content.Headers.ContentType = new MediaTypeHeaderValue(contentType);

            Tracer.Write($"HttpHelper.GetStreamContentUpload: End.");
            return content;
        }
    }
}
