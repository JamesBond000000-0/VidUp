using System;
using Drexel.VidUp.Business;
using Drexel.VidUp.Youtube.AuthenticationService;
using Drexel.VidUp.Youtube.Http;

namespace Drexel.VidUp.Youtube
{
    public static class StatusInformationCreatorYoutube
    {
        public static StatusInformation Create(string code, string message, AuthenticationException e)
        {
            StatusInformationType statusInformationType = StatusInformationType.AuthenticationError;

            string messageAdditional = "Authentication error";
            if (e.IsApiResponseError)
            {
                statusInformationType |= StatusInformationType.AuthenticationApiResponseError;
                messageAdditional += " Server denied authentication";
                //pattern match instead of the unchecked cast: a differently
                //typed or missing inner exception previously replaced the
                //actual authentication error with a cast/null error (BUG-32)
                if (e.InnerException is HttpStatusException httpStatusException)
                {
                    return new StatusInformation(code, $"{message} {messageAdditional}: {httpStatusException.StatusCode} {httpStatusException.Message} with content '{httpStatusException.Content}'.", statusInformationType);
                }
            }

            string innerDescription = e.InnerException != null
                ? $"{e.InnerException.GetType().Name}: {e.InnerException.Message}"
                : "no inner exception";
            return new StatusInformation(code, $"{message} {messageAdditional}: {innerDescription}.", statusInformationType);
        }

        public static StatusInformation Create(string code, string source, string message, HttpStatusException e)
        {
            StatusInformationType statusInformationType;
            if (e.Content != null && e.Content.Contains("quotaExceeded"))
            {
                statusInformationType = StatusInformationType.QuotaError;
            }
            else
            {
                statusInformationType = StatusInformationType.Other;
            }

            if (string.IsNullOrWhiteSpace(message))
            {
                message = "Server denied request";
            }

            string sourceString = string.Empty;
            if (source != null)
            {
                sourceString = $"{source}: ";
            }

            return new StatusInformation(code, $"{sourceString}{message} {e.StatusCode} {e.Message} with content '{e.Content}'.", statusInformationType);
        }

        public static StatusInformation Create(string code, string message, HttpStatusException e)
        {
            return StatusInformationCreatorYoutube.Create(code, null, message, e);
        }
    }
}