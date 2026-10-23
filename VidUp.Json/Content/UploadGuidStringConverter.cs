using System;
using Drexel.VidUp.Business;
using Newtonsoft.Json;

namespace Drexel.VidUp.Json.Content
{
    public class UploadGuidStringConverter : JsonConverter<Upload>
    {

        public override Upload ReadJson(JsonReader reader, Type objectType, Upload existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            string guidString = (string)reader.Value;
            if (string.IsNullOrWhiteSpace(guidString))
            {
                return null;
            }

            if(JsonDeserializationContent.AllUploads == null)
            {
                return null;
            }

            if (!Guid.TryParse(guidString, out Guid uploadGuid))
            {
                //malformed guid: treat like a missing upload (BUG-21), the caller
                //removes null entries from template uploads and reserializes.
                return null;
            }

            Upload upload = JsonDeserializationContent.AllUploads.Find(upload2 => upload2.Guid == uploadGuid);
            if (upload == null)
            {
                return null;
            }

            return upload;
        }

        public override void WriteJson(JsonWriter writer, Upload value, JsonSerializer serializer)
        {
            writer.WriteValue(value.Guid);
        }
    }
}
