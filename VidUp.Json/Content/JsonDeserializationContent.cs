using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Metadata;
using Drexel.VidUp.Business;
using Drexel.VidUp.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Drexel.VidUp.Json.Content
{
    public class JsonDeserializationContent
    {
        private string playlistListFilePath;
        private string uploadListFilePath;
        private string templateListFilePath;
        private string allUploadsFilePath;
        private string youtubeAccountListFilePath;
        private string uploadProgressFilePath;
        private string serializationFolder;

        private string thumbnailFallbackImageFolder;

        private readonly List<string> backupRecoveredFiles = new List<string>();

        //Names of data files whose main file was unreadable and which were
        //recovered from their ".bak" backup instead (BUG-01). The UI shows
        //this to the user, because the restored data may be slightly older.
        public IReadOnlyList<string> BackupRecoveredFiles
        {
            get => this.backupRecoveredFiles;
        }

        //Returns the path to read for a data file: the file itself, or its
        //".bak" backup when the main file is not even parseable JSON anymore
        //(e.g. truncated by a crash during an old-style direct write).
        private string getReadFilePath(string filePath)
        {
            if (!File.Exists(filePath) || JsonDeserializationContent.isValidJson(filePath))
            {
                return filePath;
            }

            string backupFilePath = filePath + ".bak";
            if (File.Exists(backupFilePath) && JsonDeserializationContent.isValidJson(backupFilePath))
            {
                string fileName = Path.GetFileName(filePath);
                if (!this.backupRecoveredFiles.Contains(fileName))
                {
                    this.backupRecoveredFiles.Add(fileName);
                }

                Tracer.Write($"JsonDeserializationContent.getReadFilePath: {fileName} is unreadable, falling back to backup file {fileName}.bak.");
                return backupFilePath;
            }

            return filePath;
        }

        private static bool isValidJson(string filePath)
        {
            try
            {
                using (StreamReader sr = new StreamReader(filePath))
                using (JsonReader reader = new JsonTextReader(sr))
                {
                    JToken.Load(reader);
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool allPlaylistsDeserialiozed = true;
        private static bool allYoutubeAccountsDeserialiozed = true;
        private static bool allCategoriesDeserialized = true;
        private static bool allCulturesDeserialized = true;
        private static YoutubeAccount youtubeAccountForNullReplacement;
        public static bool AllPlaylistsDeserialiozed
        {
            set => JsonDeserializationContent.allPlaylistsDeserialiozed = value;
        }

        public static bool AllYoutubeAccountsDeserialized
        {
            set => JsonDeserializationContent.allYoutubeAccountsDeserialiozed = value;
        }

        public static bool AllCategoriesDeserialized
        {
            set => JsonDeserializationContent.allCategoriesDeserialized = value;
        }

        public static bool AllCulturesDeserialized
        {
            set => JsonDeserializationContent.allCulturesDeserialized = value;
        }

        public static YoutubeAccount YoutubeAccountForNullReplacement
        {
            get
            {
                if(JsonDeserializationContent.youtubeAccountForNullReplacement == null)
                {
                    JsonDeserializationContent.youtubeAccountForNullReplacement = new YoutubeAccount($"Missing account replacement {DateTime.Now.ToString("yyyyy-MM-dd_HH-mm-ss")}");
                    DeserializationRepositoryContent.YoutubeAccountList.AddYoutubeAccount(JsonDeserializationContent.youtubeAccountForNullReplacement);
                }

                return JsonDeserializationContent.youtubeAccountForNullReplacement;
            }
        }

        public static List<Upload> AllUploads { get; set; }
        private List<Template> templates { get; set; }
        private List<Guid> uploadListGuids { get; set; }

        public JsonDeserializationContent(string serializationFolder, string thumbnailFallbackImageFolder)
        {
            this.serializationFolder = serializationFolder;
            this.playlistListFilePath = Path.Combine(this.serializationFolder, "playlistlist.json");
            this.uploadListFilePath = Path.Combine(this.serializationFolder, "uploadlist.json");
            this.templateListFilePath = Path.Combine(this.serializationFolder, "templatelist.json");
            this.allUploadsFilePath = Path.Combine(this.serializationFolder, "uploads.json");
            this.youtubeAccountListFilePath = Path.Combine(this.serializationFolder, "accountlist.json");
            this.uploadProgressFilePath = Path.Combine(serializationFolder, "uploadprogress.json");

            this.thumbnailFallbackImageFolder = thumbnailFallbackImageFolder;
        }

        public void Deserialize(ReSerialize reSerialize)
        {
            Tracer.Write($"JsonDeserializationContent.Deserialize: Start.");

            reSerialize.YoutubeAccountList = this.deserializeYoutubeAccountList();
            reSerialize.PlaylistList = this.deserializePlaylistList();
            if (!JsonDeserializationContent.allYoutubeAccountsDeserialiozed)
            {
                Tracer.Write($"JsonDeserializationContent.Deserialize: At least one youtube account on playlist list deserialization was missing and replaced.");
                reSerialize.PlaylistList = true;
                reSerialize.YoutubeAccountList = true;
            }

            JsonDeserializationContent.allYoutubeAccountsDeserialiozed = true;
            this.deserializeAllUploads();
            if(!JsonDeserializationContent.allPlaylistsDeserialiozed)
            {
                Tracer.Write($"JsonDeserializationContent.Deserialize: At least one playlist on all uploads deserialization was missing.");
                reSerialize.AllUploads = true;
            }

            if (!JsonDeserializationContent.allYoutubeAccountsDeserialiozed)
            {
                Tracer.Write($"JsonDeserializationContent.Deserialize: At least one youtube account on all uploads deserialization was missing and replaced.");
                reSerialize.AllUploads = true;
                reSerialize.YoutubeAccountList = true;
            }

            if (!JsonDeserializationContent.allCategoriesDeserialized || !JsonDeserializationContent.allCulturesDeserialized)
            {
                Tracer.Write($"JsonDeserializationContent.Deserialize: At least one category or culture on all uploads deserialization was unknown and replaced with null.");
                reSerialize.AllUploads = true;
            }

            JsonDeserializationContent.allPlaylistsDeserialiozed = true;
            JsonDeserializationContent.allYoutubeAccountsDeserialiozed = true;
            JsonDeserializationContent.allCategoriesDeserialized = true;
            JsonDeserializationContent.allCulturesDeserialized = true;
            reSerialize.TemplateList = this.deserializeTemplateList();
            if (!JsonDeserializationContent.allPlaylistsDeserialiozed)
            {
                Tracer.Write($"JsonDeserializationContent.Deserialize: At least one playlist on template list deserialization was missing.");
                reSerialize.TemplateList = true;
            }

            if (!JsonDeserializationContent.allYoutubeAccountsDeserialiozed)
            {
                Tracer.Write($"JsonDeserializationContent.Deserialize: At least one youtube account on template list deserialization was missing and replaced.");
                reSerialize.TemplateList = true;
                reSerialize.YoutubeAccountList = true;
            }

            if (!JsonDeserializationContent.allCategoriesDeserialized || !JsonDeserializationContent.allCulturesDeserialized)
            {
                Tracer.Write($"JsonDeserializationContent.Deserialize: At least one category or culture on template list deserialization was unknown and replaced with null.");
                reSerialize.TemplateList = true;
            }

            if (!this.setTemplateOnUploads())
            {
                //the uploads file holds the broken reference, so uploads must be
                //rewritten, not the template list (BUG-21).
                reSerialize.AllUploads = true;
            }

            this.deserializeUploadListGuids();
            this.createTemplateListToRepository();
            reSerialize.UploadList = this.createUploadListToRepository();

            if(!this.checkUploadListUploadInTemplateUploads())
            {
                Tracer.Write($"JsonDeserializationContent.Deserialize: At least one upload with template in upload list was not in template uploads.");
                reSerialize.AllUploads = true;
            }

            if(!this.checkUploadTemplateEqualsContainingTemplate())
            {
                Tracer.Write($"JsonDeserializationContent.Deserialize: At least one upload's template in template uploads was different to containing template.");
                reSerialize.AllUploads = true;
                reSerialize.TemplateList = true;
            }

            if (!this.deserializeUploadProgress())
            {
                reSerialize.AllUploads = true;
            }

            Tracer.Write($"JsonDeserializationContent.Deserialize: End.");
        }

        private bool deserializeUploadProgress()
        {
            bool result = true;
            Tracer.Write($"JsonDeserializationContent.deserializeUploadProgress: Start.");
            if (!File.Exists(this.uploadProgressFilePath))
            {
                Tracer.Write($"JsonDeserializationContent.deserializeUploadProgress: End, no upload progress storage file found.");
                return true;
            }

            JsonSerializer serializer = new JsonSerializer();
            using (StreamReader sr = new StreamReader(this.getReadFilePath(this.uploadProgressFilePath)))
            using (JsonReader reader = new JsonTextReader(sr))
            {
                try
                {
                    UploadProgress progress = serializer.Deserialize<UploadProgress>(reader);
                    if (progress != null)
                    {
                        Upload upload = JsonDeserializationContent.AllUploads.Find(upl => upl.Guid == progress.UploadGuid);

                        if (upload != null)
                        {
                            if (upload.UploadStatusOnDeserialization == UplStatus.Uploading)
                            {
                                upload.BytesSent = progress.BytesSent;
                                result = false;
                            }
                        }
                    }
                } 
                catch (Exception ex)
                {
                    Tracer.Write($"JsonDeserializationContent.deserializeUploadProgress: End, upload progress storage file could not be deserialized.");
                }
            }

            File.Delete(this.uploadProgressFilePath);

            Tracer.Write($"JsonDeserializationContent.deserializeUploadProgress: End.");
            return result;
        }

        private bool deserializeYoutubeAccountList()
        {
            Tracer.Write($"JsonDeserializationContent.deserializeYoutubeAccountList: Start.");
            if (!File.Exists(this.youtubeAccountListFilePath))
            {
                DeserializationRepositoryContent.YoutubeAccountList = new YoutubeAccountList(null);
                DeserializationRepositoryContent.YoutubeAccountList.AddYoutubeAccount(new YoutubeAccount("Default Account"));
                Tracer.Write($"JsonDeserializationContent.deserializeYoutubeAccountList: Adding default account.");
                return true;
            }

            JsonSerializer serializer = new JsonSerializer();
            using (StreamReader sr = new StreamReader(this.getReadFilePath(this.youtubeAccountListFilePath)))
            using (JsonReader reader = new JsonTextReader(sr))
            {
                DeserializationRepositoryContent.YoutubeAccountList = serializer.Deserialize<YoutubeAccountList>(reader);
            }

            Tracer.Write($"JsonDeserializationContent.deserializeYoutubeAccountList: End.");
            return false;
        }

        private bool deserializePlaylistList()
        {
            Tracer.Write($"JsonDeserializationContent.deserializePlaylistList: Start.");
            if (!File.Exists(this.playlistListFilePath))
            {
                DeserializationRepositoryContent.PlaylistList = new PlaylistList(null);
                Tracer.Write($"JsonDeserializationContent.deserializePlaylistList: End, no playlist storage file found.");
                return false;
            }

            JsonSerializer serializer = new JsonSerializer();
            serializer.Converters.Add(new YoutubeAccountGuidStringConverter());

            using (StreamReader sr = new StreamReader(this.getReadFilePath(this.playlistListFilePath)))
            using (JsonReader reader = new JsonTextReader(sr))
            {
                DeserializationRepositoryContent.PlaylistList = serializer.Deserialize<PlaylistList>(reader);
            }

            Tracer.Write($"JsonDeserializationContent.deserializePlaylistList: End.");
            return false;
        }

        private void deserializeAllUploads()
        {
            Tracer.Write($"JsonDeserializationContent.deserializeAllUploads: Start.");
            if (!File.Exists(this.allUploadsFilePath))
            {
                Tracer.Write($"JsonDeserializationContent.deserializeAllUploads: End, no all uploads storage file found.");
                return;
            }

            JsonSerializer serializer = new JsonSerializer();
            //This converters returns existing Playlist objects from deserialization repository.
            serializer.Converters.Add(new PlaylistPlaylistIdConverter());
            serializer.Converters.Add(new CategoryIdConverter());
            serializer.Converters.Add(new CultureInfoCultureStringConverter());
            //templates are set to null, because template objects do not yet exist.
            //templates are set on setTemplateOnUploads by reading json again.
            serializer.Converters.Add(new TemplateNullConverter());
            serializer.Converters.Add(new YoutubeAccountGuidStringConverter());
            //a broken value in one record (e.g. a malformed guid) must not fail
            //the whole file (BUG-21): log it and let deserialization continue
            //with the remaining records.
            serializer.Error += (sender, args) =>
            {
                if (args.ErrorContext.Error is JsonReaderException
                    || args.ErrorContext.Error.Message.StartsWith("Unexpected end"))
                {
                    //structurally broken file (e.g. truncated): do not salvage
                    //partial records; the caller falls back to an empty list.
                    return;
                }

                Tracer.Write($"JsonDeserializationContent.deserializeAllUploads: Error in uploads storage file, skipping broken value: {args.ErrorContext.Error.Message}");
                args.ErrorContext.Handled = true;
            };

            try
            {
                using (StreamReader sr = new StreamReader(this.getReadFilePath(this.allUploadsFilePath)))
                using (JsonReader reader = new JsonTextReader(sr))
                {
                    JsonDeserializationContent.AllUploads = serializer.Deserialize<List<Upload>>(reader);
                }
            }
            catch (Exception ex)
            {
                //a broken uploads file (and no readable backup, see getReadFilePath)
                //must not abort the whole startup (BUG-01). Continue with an empty
                //list and leave the broken file untouched on disk.
                Tracer.Write($"JsonDeserializationContent.deserializeAllUploads: uploads storage file could not be deserialized ({ex.GetType().Name}: {ex.Message}), continuing with empty uploads list.");
                JsonDeserializationContent.AllUploads = new List<Upload>();
            }

            if (JsonDeserializationContent.AllUploads != null)
            {
                //records whose guid could not be parsed keep an empty guid (their
                //conversion error was handled above); drop them (BUG-21).
                int removedCount = JsonDeserializationContent.AllUploads.RemoveAll(upload => upload == null || upload.Guid == Guid.Empty);
                if (removedCount > 0)
                {
                    Tracer.Write($"JsonDeserializationContent.deserializeAllUploads: Removed {removedCount} upload(s) with missing or malformed guid.");
                }
            }

            Tracer.Write($"JsonDeserializationContent.deserializeAllUploads: End.");
        }

        private bool deserializeTemplateList()
        {
            bool reserialize = false;
            Tracer.Write($"JsonDeserializationContent.deserializeTemplateList: Start.");
            if (!File.Exists(this.templateListFilePath))
            {
                Tracer.Write($"JsonDeserializationContent.deserializeTemplateList: End, no template list storage file found.");
                return reserialize;
            }

            JsonSerializer serializer = new JsonSerializer();
            serializer.Converters.Add(new UploadGuidStringConverter());
            serializer.Converters.Add(new PlaylistPlaylistIdConverter());
            serializer.Converters.Add(new CategoryIdConverter());
            serializer.Converters.Add(new CultureInfoCultureStringConverter());
            serializer.Converters.Add(new YoutubeAccountGuidStringConverter());
            serializer.Error += (sender, args) =>
            {
                if (args.ErrorContext.Error is JsonReaderException
                    || args.ErrorContext.Error.Message.StartsWith("Unexpected end"))
                {
                    return;
                }

                Tracer.Write($"JsonDeserializationContent.deserializeTemplateList: Error in template list storage file, skipping broken value: {args.ErrorContext.Error.Message}");
                args.ErrorContext.Handled = true;
            };

            //read the "templates" property of the root object explicitly instead of
            //the previous fixed "read 3 tokens and deserialize the rest" approach
            //(BUG-23): that broke whenever the file structure differed, e.g. for
            //"templates": null, which is now treated as an empty template list.
            //Deserializing the property value directly also still avoids creating a
            //TemplateList, whose constructor would add event listeners etc.
            JToken templatesToken = null;
            using (StreamReader sr = new StreamReader(this.getReadFilePath(this.templateListFilePath)))
            using (JsonReader reader = new JsonTextReader(sr))
            {
                JToken rootToken = JToken.Load(reader);
                if (rootToken.Type == JTokenType.Object)
                {
                    templatesToken = rootToken["templates"];
                }
                else if (rootToken.Type == JTokenType.Array)
                {
                    templatesToken = rootToken;
                }
            }

            if (templatesToken == null || templatesToken.Type == JTokenType.Null)
            {
                this.templates = new List<Template>();
            }
            else
            {
                this.templates = templatesToken.ToObject<List<Template>>(serializer) ?? new List<Template>();
            }

            int removedCount = this.templates.RemoveAll(template => template == null || template.Guid == Guid.Empty);
            if (removedCount > 0)
            {
                Tracer.Write($"JsonDeserializationContent.deserializeTemplateList: Removed {removedCount} template(s) with missing or malformed guid.");
                reserialize = true;
            }

            foreach (Template template in this.templates)
            {
                if (template.Uploads.Contains(null))
                {
                    template.RemoveUpload(null);
                    reserialize = true;
                }
            }

            if (reserialize)
            {
                Tracer.Write($"JsonDeserializationContent.deserializeTemplateList: Uploads from template uploads were missing in all uploads.");
            }

            Tracer.Write($"JsonDeserializationContent.deserializeTemplateList: End.");

            return reserialize;
        }

        //setting templates on uploads by reading json again
        private bool setTemplateOnUploads()
        {
            bool result = true;
            Tracer.Write($"JsonDeserializationContent.setTemplateOnUploads: Start.");
            if (JsonDeserializationContent.AllUploads != null && this.templates != null)
            {
                Dictionary<Guid,Guid> uploadTemplateMap = new Dictionary<Guid, Guid>();
                JArray jUploads;
                try
                {
                    using (StreamReader sr = new StreamReader(this.getReadFilePath(this.allUploadsFilePath)))
                    using (JsonReader reader = new JsonTextReader(sr))
                    {
                        jUploads = JArray.Load(reader);
                    }
                }
                catch (Exception ex)
                {
                    //unreadable uploads file (already handled in deserializeAllUploads):
                    //continue without template assignments instead of aborting startup.
                    Tracer.Write($"JsonDeserializationContent.setTemplateOnUploads: uploads storage file could not be read again ({ex.GetType().Name}: {ex.Message}), skipping template assignment.");
                    return false;
                }

                {
                    foreach (JToken jUpload in jUploads)
                    {
                        //skip records with missing or malformed guids instead of
                        //failing the whole deserialization (BUG-21); such records
                        //are already dropped or repaired elsewhere.
                        string uploadGuid = jUpload["guid"]?.Value<string>();
                        string templateGuid = jUpload["template"]?.Value<string>();
                        if (!String.IsNullOrWhiteSpace(templateGuid)
                            && Guid.TryParse(uploadGuid, out Guid uploadGuidParsed)
                            && Guid.TryParse(templateGuid, out Guid templateGuidParsed))
                        {
                            uploadTemplateMap[uploadGuidParsed] = templateGuidParsed;
                        }
                    }
                }

                foreach (Upload upload in JsonDeserializationContent.AllUploads)
                {
                    if(uploadTemplateMap.ContainsKey(upload.Guid))
                    {
                        Type uploadType = typeof(Upload);
                        FieldInfo templateFieldInfo = uploadType.GetField("template", BindingFlags.NonPublic | BindingFlags.Instance);
                        Template template = this.templates.Find(template => template.Guid == uploadTemplateMap[upload.Guid]);

                        if (template != null)
                        {
                            templateFieldInfo.SetValue(upload, template);
                        }      
                        else
                        {
                            Tracer.Write($"JsonDeserializationContent.setTemplateOnUploads: Template {uploadTemplateMap[upload.Guid]} not found.");
                            result = false;
                        }
                    }
                }
            }

            Tracer.Write($"JsonDeserializationContent.setTemplateOnUploads: End.");
            return result;
        }

        private void deserializeUploadListGuids()
        {
            Tracer.Write($"JsonDeserializationContent.deserializeUploadListGuids: Start.");
            if (!File.Exists(this.uploadListFilePath))
            {
                Tracer.Write($"JsonDeserializationContent.deserializeUploadListGuids: End, no upload list guid storage file found.");
                return;
            }

            JsonSerializer serializer = new JsonSerializer();

            UploadListGuids guids;
            using (StreamReader sr = new StreamReader(this.getReadFilePath(this.uploadListFilePath)))
            using (JsonReader reader = new JsonTextReader(sr))
            {
                guids = serializer.Deserialize<UploadListGuids>(reader);
            }

            this.uploadListGuids = guids.Guids;

            Tracer.Write($"JsonDeserializationContent.deserializeUploadListGuids: End.");
        }

        private bool createUploadListToRepository()
        {
            bool reserialize = false;
            Tracer.Write($"JsonDeserializationContent.createUploadListToRepository: Start.");
            List<Upload> uploads = new List<Upload>();
            if (this.uploadListGuids != null && this.uploadListGuids.Count > 0)
            {
                Upload upload;
                foreach (Guid guid in this.uploadListGuids)
                {
                    upload = null;

                    if (JsonDeserializationContent.AllUploads != null)
                    {
                        upload = JsonDeserializationContent.AllUploads.Find(upload => upload.Guid == guid);
                    }

                    if (upload != null)
                    {
                        uploads.Add(upload);
                    }
                    else
                    {
                        reserialize = true;
                    }
                }
            }

            if(reserialize)
            {
                Tracer.Write($"JsonDeserializationContent.createUploadListToRepository: At least one upload from upload list were missing in all uploads.");
            }

            DeserializationRepositoryContent.UploadList = new UploadList(uploads, DeserializationRepositoryContent.TemplateList, DeserializationRepositoryContent.PlaylistList, this.thumbnailFallbackImageFolder);

            Tracer.Write($"JsonDeserializationContent.createUploadListToRepository: End.");
            return reserialize;
        }

        private void createTemplateListToRepository()
        {
            Tracer.Write($"JsonDeserializationContent.createTemplateListToRepository: Start.");
            DeserializationRepositoryContent.TemplateList = new TemplateList(this.templates, DeserializationRepositoryContent.PlaylistList);
            Tracer.Write($"JsonDeserializationContent.createTemplateListToRepository: End.");
        }

        private bool checkUploadListUploadInTemplateUploads()
        {
            Tracer.Write($"JsonDeserializationContent.checkUploadListUploadInTemplateUploads: Start.");

            bool result = true;
            foreach (Upload upload in DeserializationRepositoryContent.UploadList)
            {
                if (upload.Template != null)
                {
                    if (!upload.Template.Uploads.Contains(upload))
                    {
                        upload.Template = null;
                        result = false;
                    }
                }
            }

            Tracer.Write($"JsonDeserializationContent.checkUploadListUploadInTemplateUploads: End.");
            return result;
        }

        private bool checkUploadTemplateEqualsContainingTemplate()
        {
            Tracer.Write($"JsonDeserializationContent.checkUploadTemplateEqualsContainingTemplate: Start.");

            bool result = true;
            //collect the mismatched (template, upload) pairs first (BUG-04): correcting
            //during enumeration of template.Uploads invalidates the enumerator, because
            //both the Template setter and RemoveUpload modify that list.
            List<KeyValuePair<Template, Upload>> mismatchedUploads = new List<KeyValuePair<Template, Upload>>();
            foreach (Template template in DeserializationRepositoryContent.TemplateList)
            {
                foreach (Upload upload in template.Uploads)
                {
                    if (upload.Template != template)
                    {
                        mismatchedUploads.Add(new KeyValuePair<Template, Upload>(template, upload));
                        result = false;
                    }
                }
            }

            foreach (KeyValuePair<Template, Upload> mismatchedUpload in mismatchedUploads)
            {
                //remove the upload from the containing template's list only. The
                //upload's Template property is authoritative (it mirrors the json
                //template field) and may legitimately point to another template
                //whose list does contain the upload. The previous code additionally
                //set upload.Template = null, whose setter removed the upload from
                //that other template's list and left it in this list with a null
                //template, so the mismatch reappeared on every startup.
                mismatchedUpload.Key.RemoveUpload(mismatchedUpload.Value);
            }

            Tracer.Write($"JsonDeserializationContent.checkUploadTemplateEqualsContainingTemplate: End.");
            return result;
        }
    }
}
