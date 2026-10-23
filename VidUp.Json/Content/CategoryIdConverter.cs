using System;
using System.Linq;
using Drexel.VidUp.Business;
using Newtonsoft.Json;

namespace Drexel.VidUp.Json.Content
{
    public class CategoryIdConverter : JsonConverter<Category>
    {
        public override Category ReadJson(JsonReader reader, Type objectType, Category existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.Value == null)
            {
                return null;
            }

            //an unknown category id (e.g. Youtube added a category after this
            //version was released) must not fail the whole deserialization (BUG-21).
            //Fall back to no category, like the playlist converter falls back to
            //null for unknown playlists, and flag the file for reserialization.
            Category category = Category.Categories.Where(cat => cat.Id == (long)reader.Value).FirstOrDefault();
            if (category == null)
            {
                JsonDeserializationContent.AllCategoriesDeserialized = false;
            }

            return category;
        }

        public override void WriteJson(JsonWriter writer, Category value, JsonSerializer serializer)
        {
            writer.WriteValue(value.Id);
        }
    }
}
