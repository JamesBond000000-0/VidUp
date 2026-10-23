using System;
using System.IO;
using Newtonsoft.Json;

namespace Drexel.VidUp.Json.Content
{
    //Writes a JSON file atomically (BUG-01): the content is first written to a
    //temporary file in the same folder and then swapped into place, keeping the
    //previous generation as "<file>.bak" (one generation only). If serialization
    //fails, the existing file stays untouched and no partial file is left behind.
    internal static class AtomicFileWriter
    {
        public static void WriteJsonAtomic(string filePath, JsonSerializer serializer, object value)
        {
            string tempFilePath = filePath + ".tmp";
            try
            {
                using (FileStream fileStream = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
                using (StreamWriter streamWriter = new StreamWriter(fileStream))
                using (JsonWriter writer = new JsonTextWriter(streamWriter))
                {
                    serializer.Serialize(writer, value);
                }

                if (File.Exists(filePath))
                {
                    File.Replace(tempFilePath, filePath, filePath + ".bak");
                }
                else
                {
                    File.Move(tempFilePath, filePath);
                }
            }
            catch
            {
                try
                {
                    if (File.Exists(tempFilePath))
                    {
                        File.Delete(tempFilePath);
                    }
                }
                catch
                {
                    //best effort cleanup only, the original exception is the relevant one
                }

                throw;
            }
        }
    }
}
