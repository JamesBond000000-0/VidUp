using System;
using System.Runtime.Serialization;
using Drexel.VidUp.Business;
using Newtonsoft.Json;

namespace Drexel.VidUp.Json.Content
{
    public class YoutubeAccountGuidStringConverter : JsonConverter<YoutubeAccount>
    {
        public override YoutubeAccount ReadJson(JsonReader reader, Type objectType, YoutubeAccount existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            string guid = (string)reader.Value;
            YoutubeAccount account = null;
            if (!string.IsNullOrWhiteSpace(guid) && Guid.TryParse(guid, out Guid accountGuid))
            {
                account = DeserializationRepositoryContent.YoutubeAccountList.Find(acc => acc.Guid == accountGuid);
            }

            if (account == null)
            {
                //empty, malformed or unknown account guid: use the replacement
                //account like for a missing account (BUG-21) instead of throwing
                //and failing the whole deserialization.
                JsonDeserializationContent.AllYoutubeAccountsDeserialized = false;
                return JsonDeserializationContent.YoutubeAccountForNullReplacement;
            }

            return account;
        }

        public override void WriteJson(JsonWriter writer, YoutubeAccount value, JsonSerializer serializer)
        {
            writer.WriteValue(value.Guid);
        }
    }
}
