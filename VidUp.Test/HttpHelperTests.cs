using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Drexel.VidUp.Business;
using Drexel.VidUp.Utils;
using Drexel.VidUp.Youtube.AuthenticationService;
using Drexel.VidUp.Youtube.Http;
using NUnit.Framework;

namespace Drexel.VidUp.Test
{
    //BUG-16: the access token cache was a plain Dictionary accessed without
    //synchronization. Parallel request creation, clears and removals must not
    //corrupt it or surface dictionary exceptions. The account used here is not
    //signed in, so every token fetch fails fast with an AuthenticationException
    //before any network access; that is the only exception these calls may make.
    [TestFixture]
    public class HttpHelperTests
    {
        [OneTimeSetUp]
        public void Initialize()
        {
            //HttpHelper logs through Tracer, whose static initialization needs a
            //Settings instance (in the app it is created by the main window).
            if (Settings.Instance == null)
            {
                Settings.Instance = new Settings("Test", "HttpHelperTests");
            }

            Directory.CreateDirectory(Settings.Instance.StorageFolder);
        }

        [Test]
        public async Task ParallelAccess_ClearAndRemove_DoNotCorruptCache()
        {
            YoutubeAccount account = new YoutubeAccount();
            ConcurrentBag<Exception> unexpected = new ConcurrentBag<Exception>();

            Task[] tasks = new Task[32];
            for (int i = 0; i < tasks.Length; i++)
            {
                int iteration = i;
                tasks[i] = Task.Run(async () =>
                {
                    try
                    {
                        if (iteration % 4 == 0)
                        {
                            HttpHelper.ClearAccessTokens();
                        }
                        else if (iteration % 4 == 1)
                        {
                            HttpHelper.RemoveAccessToken(account);
                        }
                        else
                        {
                            await HttpHelper.GetAuthenticatedRequestMessageAsync(account, HttpMethod.Get, "https://www.googleapis.com/youtube/v3/videos").ConfigureAwait(false);
                        }
                    }
                    catch (AuthenticationException)
                    {
                        //expected: the account has no refresh token
                    }
                    catch (Exception e)
                    {
                        unexpected.Add(e);
                    }
                });
            }

            await Task.WhenAll(tasks).ConfigureAwait(false);

            Assert.That(unexpected, Is.Empty, string.Join(Environment.NewLine, unexpected));
        }

        [Test]
        public void RemoveAccessToken_NullAccount_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => HttpHelper.RemoveAccessToken(null));
        }
    }
}
