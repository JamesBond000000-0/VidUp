using System;
using System.Globalization;
using System.Linq;
using Drexel.VidUp.Business;
using Newtonsoft.Json;

namespace Drexel.VidUp.Json.Content
{
    public class CultureInfoCultureStringConverter : JsonConverter<CultureInfo>
    {
        public override CultureInfo ReadJson(JsonReader reader, Type objectType, CultureInfo existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.Value == null)
            {
                return null;
            }

            //must be AllCultureInfos as culture can be set on template before cultures were filtered in settings.
            //So in a template e.g. can be a culture which is later filtered out.
            string cultureName = (string)reader.Value;
            CultureInfo culture = Cultures.AllCultureInfos.Where(cult => cult.Name == cultureName).FirstOrDefault();
            if (culture == null)
            {
                //a culture name unknown on this OS (e.g. written by a newer Windows
                //version) must not fail the whole deserialization (BUG-21). Try to
                //resolve it directly, otherwise fall back to no culture and flag
                //the file for reserialization.
                try
                {
                    culture = CultureInfo.GetCultureInfo(cultureName);
                }
                catch (CultureNotFoundException)
                {
                    JsonDeserializationContent.AllCulturesDeserialized = false;
                }
            }

            return culture;
        }

        public override void WriteJson(JsonWriter writer, CultureInfo value, JsonSerializer serializer)
        {
            writer.WriteValue(value.Name);
        }
    }
}
