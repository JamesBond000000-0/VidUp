using System;
using System.IO;
using System.Linq;
using Drexel.VidUp.Utils;
using NUnit.Framework;

namespace Drexel.VidUp.Test
{
    //BUG-30: Tracer's static constructor threw when Settings was not set up
    //yet (or the storage folder was missing), after which every Tracer.Write
    //failed with a TypeInitializationException. The old trace file pruning
    //also relied on the file system return order instead of sorting.
    [TestFixture]
    public class TracerTests
    {
        [Test]
        public void Write_WithoutSettings_DoesNotThrow()
        {
            Settings original = Settings.Instance;
            try
            {
                Settings.Instance = null;
                Assert.DoesNotThrow(() => Tracer.Write("test without settings"));
            }
            finally
            {
                Settings.Instance = original;
            }
        }

        [Test]
        public void Write_WithTracingEnabled_DoesNotThrow()
        {
            Settings original = Settings.Instance;
            try
            {
                Settings settings = new Settings("Test", "TracerTests");
                settings.UserSettings.Trace = true;
                Settings.Instance = settings;
                Directory.CreateDirectory(settings.StorageFolder);

                Assert.DoesNotThrow(() => Tracer.Write("tracer test line"));
            }
            finally
            {
                Settings.Instance = original;
            }
        }

        [Test]
        public void PruneOldTraceFiles_KeepsFourNewestByName()
        {
            string folder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                string[] names =
                {
                    "trace_2026-01-01_00-00-00-000.txt",
                    "trace_2026-01-02_00-00-00-000.txt",
                    "trace_2026-01-03_00-00-00-000.txt",
                    "trace_2026-01-04_00-00-00-000.txt",
                    "trace_2026-01-05_00-00-00-000.txt",
                    "trace_2026-01-06_00-00-00-000.txt",
                    "trace_2026-01-07_00-00-00-000.txt",
                };
                //create in scrambled order; pruning must not depend on the
                //file system's enumeration order
                foreach (string name in names.Reverse())
                {
                    File.WriteAllText(Path.Combine(folder, name), "x");
                }
                File.WriteAllText(Path.Combine(folder, "other.txt"), "x");

                Tracer.PruneOldTraceFiles(folder);

                string[] remaining = Directory.GetFiles(folder, "trace_*.txt")
                    .Select(Path.GetFileName)
                    .OrderBy(name => name, StringComparer.Ordinal)
                    .ToArray();
                Assert.That(remaining, Is.EqualTo(names.Skip(3).ToArray()));
                Assert.That(File.Exists(Path.Combine(folder, "other.txt")), Is.True);
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }
    }
}
