using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Drexel.VidUp.Business;
using Drexel.VidUp.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Drexel.VidUp.Json.Content
{
    public class JsonSerializationContent
    {
        private string playlistListFilePath;
        private string uploadListFilePath;
        private string templateListFilePath;
        private string allUploadsFilePath;
        private string youtubeAccountListFilePath;
        private string uploadProgressFilePath;

        private UploadList uploadList;
        private TemplateList templateList;
        private PlaylistList playlistList;
        private YoutubeAccountList youtubeAccountList;

        //serialiazer can also be called on upload thread or file system watcher event e.g.
        private object allUploadsLock = new object();
        private object uploadListLock = new object();
        private object templateListLock = new object();
        private object playlistListLock = new object();
        private object youtubeAccountListLock = new object();
        private object uploadProgressLock = new object();

        private bool serializationEnabled = true;
        private object serializationEnabledLock = new object();
        private int openWritesCount = 0;
        private CountdownEvent openWritesCountDown;

        public static JsonSerializationContent JsonSerializer;

        public JsonSerializationContent(string serializationFolder, UploadList uploadList, TemplateList templateList, PlaylistList playlistList, YoutubeAccountList youtubeAccountList)
        {
            this.playlistListFilePath = Path.Combine(serializationFolder, "playlistlist.json");
            this.uploadListFilePath = Path.Combine(serializationFolder, "uploadlist.json");
            this.templateListFilePath = Path.Combine(serializationFolder, "templatelist.json");
            this.allUploadsFilePath = Path.Combine(serializationFolder, "uploads.json");
            this.youtubeAccountListFilePath = Path.Combine(serializationFolder, "accountlist.json");
            this.uploadProgressFilePath = Path.Combine(serializationFolder, "uploadprogress.json");

            this.uploadList = uploadList;
            this.templateList = templateList;
            this.playlistList = playlistList;
            this.youtubeAccountList = youtubeAccountList;
        }

        //Registers an open write so StopSerialization can wait for it. The counter
        //must always be decremented again via EndWrite, even on exceptions (BUG-01),
        //therefore every Serialize method calls EndWrite in a finally block.
        private bool BeginWrite()
        {
            lock(this.serializationEnabledLock)
            {
                if(!this.serializationEnabled)
                {
                    return false;
                }

                this.openWritesCount++;
                return true;
            }
        }

        private void EndWrite()
        {
            lock (this.serializationEnabledLock)
            {
                this.openWritesCount--;
                if (this.openWritesCountDown != null && this.openWritesCountDown.CurrentCount > 0)
                {
                    this.openWritesCountDown.Signal();
                }
            }
        }

        //on Upload [JsonObject(MemberSerialization = MemberSerialization.OptIn)]
        //is set so only opt-in properties/fields with [JsonProperty] are serialized
        public void SerializeAllUploads()
        {
            Tracer.Write($"JsonSerializationContent.SerializeAllUploads: Start.");

            if(!this.BeginWrite())
            {
                Tracer.Write($"JsonSerializationContent.SerializeAllUploads: End, serialization disabled.");
                return;
            }

            try
            {
                JsonSerializer serializer = new JsonSerializer();
                serializer.Converters.Add(new StringEnumConverter());
                serializer.Converters.Add(new TemplateGuidStringConverter());
                serializer.Converters.Add(new PlaylistPlaylistIdConverter());
                serializer.Converters.Add(new CategoryIdConverter());
                serializer.Converters.Add(new CultureInfoCultureStringConverter());
                serializer.Converters.Add(new YoutubeAccountGuidStringConverter());

                serializer.Formatting = Formatting.Indented;

                lock(this.allUploadsLock)
                {
                    //the snapshot is created inside the lock (BUG-13), so a concurrent
                    //modification cannot corrupt the enumeration or let an outdated
                    //snapshot overwrite newer content.
                    List<Upload> allUploads = new List<Upload>();
                    foreach (Upload upload in this.uploadList)
                    {
                        allUploads.Add(upload);
                    }

                    foreach (Template template in this.templateList)
                    {
                        foreach (Upload upload in template.Uploads)
                        {
                            if (!allUploads.Contains(upload))
                            {
                                allUploads.Add(upload);
                            }
                        }
                    }

                    AtomicFileWriter.WriteJsonAtomic(this.allUploadsFilePath, serializer, allUploads);
                }

                Tracer.Write($"JsonSerializationContent.SerializeAllUploads: End.");
            }
            finally
            {
                this.EndWrite();
            }
        }

        //on UploadList [JsonObject(MemberSerialization = MemberSerialization.OptIn)]
        //is set so only opt-in properties/fields with [JsonProperty] are serialized
        //uploadList changes are only triggered by GUI thread, even file system watcher call
        //is invoked on the GUI thread.
        public void SerializeUploadList()
        {
            Tracer.Write($"JsonSerializationContent.SerializeUploadList: Start.");

            if (!this.BeginWrite())
            {
                Tracer.Write($"JsonSerializationContent.SerializeUploadList: End, serialization disabled.");
                return;
            }

            try
            {
                JsonSerializer serializer = new JsonSerializer();
                serializer.Converters.Add(new UploadGuidStringConverter());
                serializer.Formatting = Formatting.Indented;
                lock (this.uploadListLock)
                {
                    AtomicFileWriter.WriteJsonAtomic(this.uploadListFilePath, serializer, this.uploadList);
                }

                Tracer.Write($"JsonSerializationContent.SerializeUploadList: End.");
            }
            finally
            {
                this.EndWrite();
            }
        }

        //on TemplateList [JsonObject(MemberSerialization = MemberSerialization.OptIn)]
        //is set so only opt-in properties/fields with [JsonProperty] are serialized
        public void SerializeTemplateList()
        {
            Tracer.Write($"JsonSerializationContent.SerializeTemplateList: Start.");

            if (!this.BeginWrite())
            {
                Tracer.Write($"JsonSerializationContent.SerializeTemplateList: End, serialization disabled.");
                return;
            }

            try
            {
                JsonSerializer serializer = new JsonSerializer();
                serializer.Converters.Add(new StringEnumConverter());
                serializer.Converters.Add(new UploadGuidStringConverter());
                serializer.Converters.Add(new PlaylistPlaylistIdConverter());
                serializer.Converters.Add(new CategoryIdConverter());
                serializer.Converters.Add(new CultureInfoCultureStringConverter());
                serializer.Converters.Add(new YoutubeAccountGuidStringConverter());

                serializer.Formatting = Formatting.Indented;

                lock (this.templateListLock)
                {
                    AtomicFileWriter.WriteJsonAtomic(this.templateListFilePath, serializer, this.templateList);
                }

                Tracer.Write($"JsonSerializationContent.SerializeTemplateList: End.");
            }
            finally
            {
                this.EndWrite();
            }
        }

        public void SerializePlaylistList()
        {
            Tracer.Write($"JsonSerializationContent.SerializePlaylistList: Start.");

            if (!this.BeginWrite())
            {
                Tracer.Write($"JsonSerializationContent.SerializePlaylistList: End, serialization disabled.");
                return;
            }

            try
            {
                JsonSerializer serializer = new JsonSerializer();
                serializer.Converters.Add(new YoutubeAccountGuidStringConverter());

                serializer.Formatting = Formatting.Indented;

                lock (this.playlistListLock)
                {
                    AtomicFileWriter.WriteJsonAtomic(this.playlistListFilePath, serializer, this.playlistList);
                }

                Tracer.Write($"JsonSerializationContent.SerializePlaylistList: End.");
            }
            finally
            {
                this.EndWrite();
            }
        }

        public void SerializeYoutubeAccountList()
        {
            Tracer.Write($"JsonSerializationContent.SerializeYoutubeAccountList: Start.");

            if (!this.BeginWrite())
            {
                Tracer.Write($"JsonSerializationContent.SerializeYoutubeAccountList: End, serialization disabled.");
                return;
            }

            try
            {
                JsonSerializer serializer = new JsonSerializer();

                serializer.Formatting = Formatting.Indented;

                lock (this.youtubeAccountListLock)
                {
                    AtomicFileWriter.WriteJsonAtomic(this.youtubeAccountListFilePath, serializer, this.youtubeAccountList);
                }

                Tracer.Write($"JsonSerializationContent.SerializeYoutubeAccountList: End.");
            }
            finally
            {
                this.EndWrite();
            }
        }

        public void SerializeUploadProgress(UploadProgress progress)
        {
            Tracer.Write($"JsonSerializationContent.SerializeUploadProgress: Start.", TraceLevel.Detailed);

            if (!this.BeginWrite())
            {
                Tracer.Write($"JsonSerializationContent.SerializeUploadProgress: End, serialization disabled.");
                return;
            }

            try
            {
                JsonSerializer serializer = new JsonSerializer();

                serializer.Formatting = Formatting.Indented;

                lock (this.uploadProgressLock)
                {
                    AtomicFileWriter.WriteJsonAtomic(this.uploadProgressFilePath, serializer, progress);
                }

                Tracer.Write($"JsonSerializationContent.SerializeUploadProgress: End.", TraceLevel.Detailed);
            }
            finally
            {
                this.EndWrite();
            }
        }

        public CountdownEvent StopSerialization()
        {
            lock (this.serializationEnabledLock)
            {
                this.serializationEnabled = false;
                //create the event only once, so a second call cannot replace the
                //event in-flight writes are signaling on (BUG-01).
                if (this.openWritesCountDown == null)
                {
                    this.openWritesCountDown = new CountdownEvent(this.openWritesCount);
                }
            }

            return this.openWritesCountDown;
        }
    }
}
