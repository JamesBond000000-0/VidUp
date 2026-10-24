using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Drexel.VidUp.Utils
{
    public static class Tracer
    {
        private static readonly object initializationLock = new object();
        private static bool initialized;

        //No static constructor: it ran on first use, potentially before
        //Settings existed, and a failure there poisoned every later Write
        //with a TypeInitializationException (BUG-30). Initialization is lazy
        //instead and every entry point swallows failures: tracing must never
        //crash the app.
        private static bool ensureInitialized()
        {
            if (Tracer.initialized)
            {
                return true;
            }

            lock (Tracer.initializationLock)
            {
                if (Tracer.initialized)
                {
                    return true;
                }

                try
                {
                    string storageFolder = Settings.Instance.StorageFolder;
                    Directory.CreateDirectory(storageFolder);
                    Tracer.PruneOldTraceFiles(storageFolder);

                    FileStream traceFileStream = File.Open(Path.Combine(storageFolder, $"trace_{DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss-fff")}.txt"), FileMode.Create);
                    Trace.Listeners.Add(new TextWriterTraceListener(traceFileStream));
                    Trace.AutoFlush = true;
                    Tracer.initialized = true;
                    return true;
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }

        //Keeps the four newest trace files (plus the one created afterwards).
        //The file names start with a sortable timestamp, so ordering by name
        //is ordering by age; previously the deletion relied on the file
        //system's return order and could delete the newest files (BUG-30).
        public static void PruneOldTraceFiles(string folder)
        {
            string[] files = Directory.GetFiles(folder, "trace_*.txt")
                .OrderBy(file => file, StringComparer.Ordinal)
                .ToArray();
            for (int i = 0; i < files.Length - 4; i++)
            {
                File.Delete(files[i]);
            }
        }

        public static void Write(string text)
        {
            Tracer.Write(text, TraceLevel.Normal);
        }

        public static void Write(string text, TraceLevel traceLevel)
        {
            try
            {
                Settings settings = Settings.Instance;
                if (settings?.UserSettings == null || !settings.UserSettings.Trace)
                {
                    return;
                }

                if (settings.UserSettings.TraceLevel == TraceLevel.Normal && traceLevel == TraceLevel.Detailed)
                {
                    return;
                }

                if (!Tracer.ensureInitialized())
                {
                    return;
                }

                Trace.WriteLine($"{DateTime.Now.ToString("dd.MM.yy HH:mm:ss.fff")}: {text}");
            }
            catch (Exception)
            {
                //tracing must never crash the app (BUG-30)
            }
        }

        public static void Close()
        {
            try
            {
                Trace.Close();
            }
            catch (Exception)
            {
            }

            lock (Tracer.initializationLock)
            {
                Tracer.initialized = false;
            }
        }
    }
}
