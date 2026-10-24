using System;
using Drexel.VidUp.Business;
using Drexel.VidUp.Youtube;
using Drexel.VidUp.Youtube.AuthenticationService;
using Drexel.VidUp.Youtube.Http;
using NUnit.Framework;

namespace Drexel.VidUp.Test
{
    //BUG-32: StatusInformationCreatorYoutube cast the inner exception without
    //checking and dereferenced it without a null check, so creating the status
    //information for an authentication error could itself throw and mask the
    //actual error.
    [TestFixture]
    public class StatusInformationCreatorTests
    {
        [Test]
        public void Create_ApiResponseErrorWithHttpStatusInner_UsesStatusDetails()
        {
            AuthenticationException exception = new AuthenticationException(
                "auth failed",
                new HttpStatusException("Unauthorized", 401, "bad token"),
                true);

            StatusInformation result = StatusInformationCreatorYoutube.Create("ERR", "msg", exception);

            Assert.That(result.Message, Does.Contain("401"));
            Assert.That(result.Message, Does.Contain("bad token"));
            Assert.That(result.IsAuthenticationApiResponseError, Is.True);
        }

        [Test]
        public void Create_ApiResponseErrorWithWrongInnerType_DoesNotThrow()
        {
            AuthenticationException exception = new AuthenticationException(
                "auth failed",
                new InvalidOperationException("something else"),
                true);

            StatusInformation result = null;
            Assert.DoesNotThrow(() => result = StatusInformationCreatorYoutube.Create("ERR", "msg", exception));
            Assert.That(result.Message, Does.Contain("InvalidOperationException"));
        }

        [Test]
        public void Create_WithoutInnerException_DoesNotThrow()
        {
            AuthenticationException exception = new AuthenticationException("auth failed", false);

            StatusInformation result = null;
            Assert.DoesNotThrow(() => result = StatusInformationCreatorYoutube.Create("ERR", "msg", exception));
            Assert.That(result.Message, Does.Contain("no inner exception"));
        }

        [Test]
        public void Create_HttpStatusWithNullContent_DoesNotThrow()
        {
            HttpStatusException exception = new HttpStatusException("error", 500, null);

            StatusInformation result = null;
            Assert.DoesNotThrow(() => result = StatusInformationCreatorYoutube.Create("ERR", "source", "msg", exception));
            Assert.That(result.IsOtherError, Is.True);
        }

        [Test]
        public void Create_HttpStatusWithQuotaContent_IsQuotaError()
        {
            HttpStatusException exception = new HttpStatusException("error", 403, "{\"error\":\"quotaExceeded\"}");

            StatusInformation result = StatusInformationCreatorYoutube.Create("ERR", "source", "msg", exception);

            Assert.That(result.IsQuotaError, Is.True);
        }
    }
}
