using System;
using Drexel.VidUp.Youtube.AuthenticationService;
using NUnit.Framework;

namespace Drexel.VidUp.Test
{
    //BUG-10: Google does not return a refresh_token on every sign-in. Parsing the
    //token endpoint responses must not crash with a KeyNotFoundException and must
    //not discard the refresh token stored earlier.
    [TestFixture]
    public class TokenResponseParserTests
    {
        [Test]
        public void ParseAccessTokenResponse_Valid_ReturnsTokenAndExpiry()
        {
            DateTime before = DateTime.Now;

            AccessToken token = TokenResponseParser.ParseAccessTokenResponse(
                "{\"access_token\":\"ya29.token\",\"expires_in\":3600,\"token_type\":\"Bearer\"}");

            Assert.That(token.Token, Is.EqualTo("ya29.token"));
            Assert.That(token.Expiry, Is.InRange(before.AddSeconds(3590), before.AddSeconds(3610)));
        }

        [Test]
        public void ParseAccessTokenResponse_MissingAccessToken_Throws()
        {
            Assert.Throws<InvalidOperationException>(() =>
                TokenResponseParser.ParseAccessTokenResponse("{\"expires_in\":3600}"));
        }

        [Test]
        public void ParseAccessTokenResponse_MissingExpiresIn_Throws()
        {
            Assert.Throws<InvalidOperationException>(() =>
                TokenResponseParser.ParseAccessTokenResponse("{\"access_token\":\"ya29.token\"}"));
        }

        [Test]
        public void GetRefreshTokenToStore_ResponseHasToken_ReturnsResponseToken()
        {
            string result = TokenResponseParser.GetRefreshTokenToStore(
                "{\"access_token\":\"a\",\"refresh_token\":\"new-refresh\"}", "old-refresh");

            Assert.That(result, Is.EqualTo("new-refresh"));
        }

        [Test]
        public void GetRefreshTokenToStore_ResponseMissing_KeepsExisting()
        {
            string result = TokenResponseParser.GetRefreshTokenToStore(
                "{\"access_token\":\"a\",\"expires_in\":3600}", "old-refresh");

            Assert.That(result, Is.EqualTo("old-refresh"));
        }

        [Test]
        public void GetRefreshTokenToStore_ResponseEmptyToken_KeepsExisting()
        {
            string result = TokenResponseParser.GetRefreshTokenToStore(
                "{\"access_token\":\"a\",\"refresh_token\":\"\"}", "old-refresh");

            Assert.That(result, Is.EqualTo("old-refresh"));
        }

        [Test]
        public void GetRefreshTokenToStore_ResponseMissingAndNoExisting_ReturnsNull()
        {
            string result = TokenResponseParser.GetRefreshTokenToStore(
                "{\"access_token\":\"a\",\"expires_in\":3600}", null);

            Assert.That(result, Is.Null);
        }
    }
}
