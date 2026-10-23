using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Drexel.VidUp.Youtube.AuthenticationService
{
    //Parses the token endpoint responses. Extracted from YoutubeAuthentication so the
    //handling of incomplete responses can be unit tested (BUG-10): Google only returns
    //a refresh_token on the first consent (or when consent is forced), so a re-sign-in
    //response without one must not crash with a KeyNotFoundException and must not
    //discard the refresh token stored earlier.
    public static class TokenResponseParser
    {
        public static AccessToken ParseAccessTokenResponse(string content)
        {
            Dictionary<string, string> tokenEndpointDecoded = JsonConvert.DeserializeObject<Dictionary<string, string>>(content);

            string accessToken;
            if (tokenEndpointDecoded == null || !tokenEndpointDecoded.TryGetValue("access_token", out accessToken) || string.IsNullOrWhiteSpace(accessToken))
            {
                throw new InvalidOperationException("Token endpoint response does not contain an access_token.");
            }

            string expiresIn;
            int expiresInSeconds;
            if (!tokenEndpointDecoded.TryGetValue("expires_in", out expiresIn) || !int.TryParse(expiresIn, out expiresInSeconds))
            {
                throw new InvalidOperationException("Token endpoint response does not contain a valid expires_in.");
            }

            return new AccessToken(accessToken, DateTime.Now.AddSeconds(expiresInSeconds));
        }

        //Returns the refresh token to store on the account: the one from the response
        //if there is one, otherwise the token stored earlier (re-sign-in without a
        //new refresh token). Returns null only when neither exists.
        public static string GetRefreshTokenToStore(string content, string existingRefreshToken)
        {
            Dictionary<string, string> tokenEndpointDecoded = JsonConvert.DeserializeObject<Dictionary<string, string>>(content);

            string refreshToken;
            if (tokenEndpointDecoded != null && tokenEndpointDecoded.TryGetValue("refresh_token", out refreshToken) && !string.IsNullOrWhiteSpace(refreshToken))
            {
                return refreshToken;
            }

            return existingRefreshToken;
        }
    }
}
