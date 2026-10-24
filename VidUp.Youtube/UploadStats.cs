using System;
using System.Collections.Generic;
using System.Threading;
using Drexel.VidUp.Business;
using Drexel.VidUp.Utils;

namespace Drexel.VidUp.Youtube
{
    public class UploadStats
    {
        private bool uploadFinished;
        private UploadList uploadList;
        private Upload currentUpload;
        private List<Upload> uploaded = new List<Upload>();
        private bool resumeUploads;

        private long currentUploadSpeedInBytesPerSecond;

        //total file size of all files to upload to check changes, changes only on upload list changes
        private long totalFileLengthToSend;
        //total remaining bytes of all files to uploads, changes only on upload list changes
        private long totalRemainingBytes;

        //total file size of all files which were tried to upload, e.g. finished or failed, changes on upload change
        private long totalFileLengthProcessed;
        //remaining bytes uploaded of all files which were tried to upload, e.g. finished or failed, changes on upload change
        private long totalBytesSent;
        //bytes sent of current upload, changes on upload change
        private long currentUploadBytesSentInitial;

        //reset on every update to avoid reevaluations for some of the stats
        private long currentRemainingBytesLeftToSend;
        //gate shared with the upload service: Update and the upload's status
        //transitions mutually exclude each other (SemaphoreSlim since BUG-09,
        //before that an AutoResetEvent whose token could be consumed without
        //being returned, see BUG-06)
        private SemaphoreSlim statsGate;

        public float TotalProgressPercentage
        {
            get
            {
                if(uploadFinished)
                {
                    return 1;
                }

                //without uploads there is no denominator; dividing by zero here
                //produced NaN / infinity, which the UI turned into garbage
                //values (BUG-28)
                if (this.totalRemainingBytes <= 0)
                {
                    return 0;
                }

                long currentUploadSent = this.currentUpload != null ? this.currentUpload.BytesSent - this.currentUploadBytesSentInitial : 0;
                return (this.totalBytesSent + currentUploadSent) / (float)this.totalRemainingBytes;
            }
        }

        public TimeSpan? CurrentFileTimeLeft
        {
            get
            {
                if (this.currentUpload != null && this.currentUploadSpeedInBytesPerSecond > 0)
                {
                    float seconds = (this.currentUpload.FileLength - this.currentUpload.BytesSent) / (float) this.currentUploadSpeedInBytesPerSecond;
                    return TimeSpan.FromSeconds(seconds);
                }

                return null;
            }
        }
        public int CurrentFileMbLeft
        {
            get
            {
                if (this.currentUpload == null)
                {
                    return 0;
                }

                return (int)((float)(this.currentUpload.FileLength - this.currentUpload.BytesSent) / Constants.ByteMegaByteFactor);
            }
        }
        public int CurrentFilePercent
        {
            get
            {
                if (this.currentUpload == null)
                {
                    return 0;
                }

                //a zero byte file is trivially complete; dividing by its length
                //produced NaN, which the int cast turned into an arbitrary
                //value (BUG-28)
                if (this.currentUpload.FileLength <= 0)
                {
                    return 100;
                }

                return (int)((float)this.currentUpload.BytesSent / this.currentUpload.FileLength * 100);
            }
        }

        public int TotalMbLeft
        {
            get
            {
                return (int)((float)this.currentRemainingBytesLeftToSend / Constants.ByteMegaByteFactor);
            }

        }

        public TimeSpan? TotalTimeLeft
        {
            get
            {
                if (this.currentUploadSpeedInBytesPerSecond > 0)
                {
                    float seconds = this.currentRemainingBytesLeftToSend / (float) this.currentUploadSpeedInBytesPerSecond;
                    return TimeSpan.FromSeconds(seconds);
                }

                return null;
            }
                
        }

        public long CurrentSpeedInBytesPerSecond
        {
            set
            {
                this.currentUploadSpeedInBytesPerSecond = value;
            }
        }

        public long CurrentSpeedInKiloBytesPerSecond
        { 
            get
            {
                return (long)(this.currentUploadSpeedInBytesPerSecond / 1024f);
            }
        }

        public bool UploadFinished
        {
            set
            {
                this.uploadFinished = value;
            }
        }

        public bool ResumeUploads
        {
            set => this.resumeUploads = value;
        }

        public UploadStats(SemaphoreSlim statsGate)
        {
            this.statsGate = statsGate;
        }

        public void Update()
        {
            this.statsGate.Wait();

            try
            {
                this.currentRemainingBytesLeftToSend = this.resumeUploads ? this.uploadList.GetRemainingBytesOfFilesToUploadIncludingResumable(this.uploaded) : this.uploadList.GetRemainingBytesOfFilesToUpload(this.uploaded);

                //check if upload has been added, removed, paused, reset...
                long currentTotalBytesInSession = (this.resumeUploads ? this.uploadList.GetTotalBytesOfFilesToUploadIncludingResumable(this.uploaded) : this.uploadList.GetTotalBytesOfFilesToUpload(this.uploaded)) + this.totalFileLengthProcessed;
                long delta = currentTotalBytesInSession - this.totalFileLengthToSend;
                if (delta != 0)
                {
                    this.totalFileLengthToSend = this.resumeUploads ? this.uploadList.GetTotalBytesOfFilesToUploadIncludingResumable(this.uploaded) : this.uploadList.GetTotalBytesOfFilesToUpload(this.uploaded);
                    this.totalRemainingBytes = this.resumeUploads ? this.uploadList.GetRemainingBytesOfFilesToUploadIncludingResumable(this.uploaded) : this.uploadList.GetRemainingBytesOfFilesToUpload(this.uploaded);
                    this.currentUploadBytesSentInitial = this.currentUpload != null ? this.currentUpload.BytesSent : 0;
                    this.totalFileLengthProcessed = 0;
                    this.totalBytesSent = 0;
                }
            }
            finally
            {
                //without the finally an exception here deadlocks every gate wait in
                //the upload service, because the gate is never released again (BUG-06)
                this.statsGate.Release();
            }
        }

        public void Initialize(UploadList uploadList, in bool resumeUploads)
        {
            this.uploadList = uploadList;
            this.resumeUploads = resumeUploads;
            this.totalFileLengthToSend = this.resumeUploads ? this.uploadList.GetTotalBytesOfFilesToUploadIncludingResumable(null) : this.uploadList.GetTotalBytesOfFilesToUpload(null);
            this.totalRemainingBytes = this.resumeUploads ? this.uploadList.GetRemainingBytesOfFilesToUploadIncludingResumable(null) : this.uploadList.GetRemainingBytesOfFilesToUpload(null);
        }

        public void NewUpload(Upload upload)
        {
            if (this.currentUpload != null)
            {
                //only the bytes actually sent count towards the progress; adding
                //the full file length also credited failed or stopped uploads
                //with bytes that never left the machine (BUG-28)
                this.totalBytesSent += this.currentUpload.BytesSent - this.currentUploadBytesSentInitial;
                this.totalFileLengthProcessed += this.currentUpload.FileLength;
                this.uploaded.Add(this.currentUpload);
            }

            this.currentUpload = upload;
            this.currentUploadBytesSentInitial = upload.BytesSent;
        }
    }
}
