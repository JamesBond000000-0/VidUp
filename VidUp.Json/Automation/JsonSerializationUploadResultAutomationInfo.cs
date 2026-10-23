using System;
using System.IO;
using Drexel.VidUp.Json.Content;
using Drexel.VidUp.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Drexel.VidUp.Json.Settings
{
    public class JsonSerializationUploadResultAutomationInfo
    {
        private string serializationFolder;
        private object serializationLock = new object();
        public static JsonSerializationUploadResultAutomationInfo JsonSerializer;

        public JsonSerializationUploadResultAutomationInfo(string serializationFolder)
        {
            this.serializationFolder = serializationFolder;
        }
        public string SerializationFolder
        {
            get => this.serializationFolder;
        }

        public void SerializeUploadResultAutomationInfo(string fileName, UploadResultAutomationInfo uploadResultAutomationInfo)
        {
            JsonSerializer serializer = new JsonSerializer();
            serializer.Formatting = Formatting.Indented;
            serializer.NullValueHandling = NullValueHandling.Ignore;

            //same protection as the content serialization (BUG-01/BUG-13):
            //dedicated lock and atomic write with one .bak generation.
            lock (this.serializationLock)
            {
                AtomicFileWriter.WriteJsonAtomic(Path.Combine(serializationFolder, fileName), serializer, uploadResultAutomationInfo);
            }
        }
    }
}
