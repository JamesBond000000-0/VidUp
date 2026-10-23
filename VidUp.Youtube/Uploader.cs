using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Drexel.VidUp.Business;
using Drexel.VidUp.Json.Content;
using Drexel.VidUp.Utils;
using Drexel.VidUp.Youtube.PlaylistItemService;
using Drexel.VidUp.Youtube.ThumbnailService;
using Drexel.VidUp.Youtube.VideoUploadService;
using System.Diagnostics;


namespace Drexel.VidUp.Youtube
{
    public delegate void ResumableSessionUriSetHandler(object sender, Upload upload);

    public delegate void UploadStartingHandler(object sender, Upload upload);

    public delegate void UploadFinishedHandler(object sender, Upload upload);

    public delegate void UploadStatsUpdatedHandler(object sender);

    public delegate void UploadBytesSentHandler(object sender, Upload upload);

    public class Uploader
    {
        private UploadList uploadList;
        private UploadStats uploadStats;

        private bool resumeUploads;
        private CancellationTokenSource tokenSource;
        private bool uploadStopped;

        private static double serializationInterval = 30d;
        private DateTime lastSerialization;

        //todo: check access for null
        private Upload currentUpload;

        public event UploadStatsUpdatedHandler UploadStatsUpdated;
        public event UploadStartingHandler UploadStarting;
        public event UploadFinishedHandler UploadFinished;
        public event ResumableSessionUriSetHandler ResumableSessionUriSet;
        public event UploadBytesSentHandler UploadBytesSent;

        public long MaxUploadInBytesPerSecond
        {
            set
            {
                Tracer.Write($"Uploader.MaxUploadInBytesPerSecond: Start, setting MaxUploadInBytesPerSecond: {value}.");

                YoutubeVideoUploadService.MaxUploadInBytesPerSecond = value;

                Tracer.Write($"Uploader.MaxUploadInBytesPerSecond: End.");
            }
        }

        public bool ResumeUploads
        {
            set
            {
                this.resumeUploads = value;
                this.uploadStats.ResumeUploads = value;
            }
        }

        public bool UploadStopped { get => this.uploadStopped; }

        public Uploader(UploadList uploadList, long maxUploadInBytesPerSecond)
        {
            Tracer.Write($"Uploader.Uploader: Start with maxUploadInBytesPerSecond: {maxUploadInBytesPerSecond}.");
            if (uploadList == null)
            {
                throw new ArgumentException("uploadList must not be null.");
            }


            this.uploadList = uploadList;

            this.tokenSource = new CancellationTokenSource();

            
            YoutubeVideoUploadService.MaxUploadInBytesPerSecond = maxUploadInBytesPerSecond;

            Tracer.Write($"Uploader.Uploader: End.");
        }

        private void onUploadStatsUpdated()
        {
            UploadStatsUpdatedHandler handler = this.UploadStatsUpdated;

            if (handler != null)
            {
                handler(this);
            }
        }

        private void onUploadStarting(Upload upload)
        {
            UploadStartingHandler handler = this.UploadStarting;

            if (handler != null)
            {
                handler(this, upload);
            }
        }

        private void onUploadFinished(Upload upload)
        {
            UploadFinishedHandler handler = this.UploadFinished;

            if (handler != null)
            {
                handler(this, upload);
            }
        }

        private void onResumableSessionUriSet(Upload upload)
        {
            ResumableSessionUriSetHandler handler = this.ResumableSessionUriSet;

            if (handler != null)
            {
                handler(this, upload);
            }
        }

        private void onUploadBytesSent(Upload upload)
        {
            UploadBytesSentHandler handler = this.UploadBytesSent;

            if (YoutubeVideoUploadService.CurrentPosition != null)
            {
                upload.BytesSent = YoutubeVideoUploadService.CurrentPosition.Value;
            }

            if (handler != null)
            {
                handler(this, upload);
            }
        }

        public async Task<UploaderResult> UploadAsync(UploadStats uploadStats, bool resumeUploads, SemaphoreSlim statsGate)
        {
            Tracer.Write($"Uploader.Upload: Start with resumeUploads: {resumeUploads}.");
            this.uploadStats = uploadStats;
            this.resumeUploads = resumeUploads;

            //a cancelled token source stays cancelled, so a reused Uploader would
            //instantly "stop" every later session; create a fresh one per session
            //and reset the stopped flag (BUG-09, latent while the UI created a
            //new Uploader for every session)
            this.tokenSource?.Dispose();
            this.tokenSource = new CancellationTokenSource();
            this.uploadStopped = false;

            List<Predicate<Upload>> predicates = new List<Predicate<Upload>>(2);
            predicates.Add(upload2 => upload2.UploadStatus == UplStatus.ReadyForUpload && File.Exists(upload2.FilePath));
            if (this.resumeUploads)
            {
                predicates.Add(upload2 => (upload2.UploadStatus == UplStatus.Failed || upload2.UploadStatus == UplStatus.Stopped) && File.Exists(upload2.FilePath));
            }

            this.uploadStats.Initialize(this.uploadList, resumeUploads);
            Upload upload = this.uploadList.GetUpload(TinyHelpers.PredicateOr(predicates.ToArray()));
            if (upload == null)
            {
                return UploaderResult.NothingDone;
            }

            bool dataSent = false;

            List<Upload> uploadsOfSession = new List<Upload>();

            //todo: move timer to upload stats and create tick event on upload stats
            using (Timer timer = new Timer(this.timerElapsed, null, 0, 2000))
            {
                while (upload != null)
                {
                    this.currentUpload = upload;
                    uploadsOfSession.Add(upload);

                    upload.UploadStatus = UplStatus.Uploading;
                    this.uploadStats.NewUpload(upload);

                    this.onUploadStarting(upload);

                    this.lastSerialization = DateTime.Now;

                    if (upload.YoutubeAccount.IsAuthenticated)
                    {
                        UploadResult videoResult = await YoutubeVideoUploadService.UploadAsync(upload, this.resumableSessionUriSet, this.tokenSource.Token, statsGate).ConfigureAwait(false);
                        JsonSerializationContent.JsonSerializer.SerializeAllUploads();

                        this.onUploadFinished(upload);

                        if (videoResult == UploadResult.Finished)
                        {
                            dataSent = true;
                            await YoutubeThumbnailService.AddThumbnailAsync(upload, this.tokenSource.Token).ConfigureAwait(false);
                            await YoutubePlaylistItemService.AddToPlaylistAsync(upload, this.tokenSource.Token).ConfigureAwait(false);
                        }

                        if (videoResult == UploadResult.FailedWithDataSent || videoResult == UploadResult.StoppedWithDataSent)
                        {
                            dataSent = true;
                        }

                        if (videoResult == UploadResult.StoppedWithDataSent || videoResult == UploadResult.StoppedWithoutDataSent)
                        {
                            this.uploadStopped = true;
                            break;
                        }
                    }
                    else
                    {
                        upload.AddStatusInformation(StatusInformationCreator.Create("ERR0017", "Account is not signed in. Sign in at Settings->YouTube Account->Kebab Menu.", StatusInformationType.AuthenticationError));
                        upload.UploadStatus = UplStatus.Failed;
                        this.onUploadFinished(upload);
                    }

                    if (!upload.UploadErrors.Any(error => error.IsQuotaError == true))
                    {
                        upload = this.uploadList.GetUpload(
                            TinyHelpers.PredicateAnd(
                                new Predicate<Upload>[]
                                {
                                    TinyHelpers.PredicateOr(predicates.ToArray()),
                                    upload2 => !uploadsOfSession.Contains(upload2)
                                }));
                    }
                    else
                    {
                        upload = null;
                    }
                }
            }

            if (dataSent)
            {
                this.updateSchedules(uploadsOfSession);
                JsonSerializationContent.JsonSerializer.SerializeTemplateList();
            }

            this.uploadStats.UploadFinished = true;

            this.uploadStats = null;

            Tracer.Write($"Uploader.Upload: End.");
            return dataSent ? UploaderResult.DataSent : UploaderResult.NoDataSent;
        }

        private void updateSchedules(List<Upload> finishedUploads)
        {
            Template[] templates = finishedUploads.Select(upload => upload.Template).Distinct().Where(template => template != null).ToArray();

            if (templates.Any())
            {
                foreach (Template template in templates)
                {
                    template.SetScheduleProgress();
                }
            }
        }

        private void timerElapsed(object info)
        {
            //the timer starts before the first upload is assigned and can still fire
            //after the upload session ended, so work on snapshots and tolerate null (BUG-06)
            Upload upload = this.currentUpload;
            if (upload == null || this.uploadStats == null)
            {
                return;
            }

            //updates BytesSent, which are used for upload stats update
            //sequence is important for consistent GUI updates
            this.onUploadBytesSent(upload);
            this.updateUploadProgress();
            this.onUploadStatsUpdated();
            Debug.WriteLine("Hello, world.");
            this.serializeOnUpload(upload);
        }

        private void updateUploadProgress()
        {
            UploadStats uploadStats = this.uploadStats;
            if (uploadStats == null)
            {
                return;
            }

            uploadStats.Update();
            //this.uploadStats.CurrentTotalBytesLeftRemaining = ;
            uploadStats.CurrentSpeedInBytesPerSecond = YoutubeVideoUploadService.CurrentSpeedInBytesPerSecond;
        }

        private void serializeOnUpload(Upload upload)
        {
            if ((DateTime.Now - this.lastSerialization).TotalSeconds >= Uploader.serializationInterval)
            {
                UploadProgress progress = new UploadProgress
                {
                    UploadGuid = upload.Guid,
                    BytesSent = upload.BytesSent
                };

                JsonSerializationContent.JsonSerializer.SerializeUploadProgress(progress);
                this.lastSerialization = DateTime.Now;
            }
        }

        private void resumableSessionUriSet(Upload upload)
        {
            this.onResumableSessionUriSet(upload);
        }


        public void StopUpload()
        {
            try
            {
                this.tokenSource?.Cancel();
            }
            catch (ObjectDisposedException)
            {
                //the token source is recreated at the start of a session, a stop
                //racing with that moment must not crash the caller (BUG-09)
            }
        }
    }
}
