using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Drexel.VidUp.Youtube.VideoUploadService;
using NUnit.Framework;

namespace Drexel.VidUp.Test
{
    //Tests for BUG-03: a failing or disposed base stream must never leave the
    //consuming side of ThrottledBufferedStream waiting forever.
    [TestFixture]
    public class ThrottledBufferedStreamTests
    {
        private static readonly TimeSpan timeout = TimeSpan.FromSeconds(30);

        //delivers its data once, then fails like a vanished file or a disk error
        private class FailingStream : MemoryStream
        {
            private bool failed;

            public FailingStream(byte[] data)
                : base(data)
            {
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                if (this.failed)
                {
                    throw new IOException("Simulated read failure.");
                }

                int read = base.Read(buffer, offset, count);
                if (read == 0)
                {
                    this.failed = true;
                    throw new IOException("Simulated read failure.");
                }

                return read;
            }
        }

        //blocks in Read until the test releases it
        private class BlockingStream : Stream
        {
            private readonly ManualResetEventSlim gate = new ManualResetEventSlim(false);

            public void Release()
            {
                this.gate.Set();
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                this.gate.Wait();
                return 0;
            }

            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new NotSupportedException();

            public override long Position
            {
                get => 0;
                set => throw new NotSupportedException();
            }

            public override void Flush()
            {
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotSupportedException();
            }

            public override void SetLength(long value)
            {
                throw new NotSupportedException();
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                throw new NotSupportedException();
            }
        }

        [Test]
        public void ReadAllData_RoundTripsContent()
        {
            byte[] data = new byte[1024 * 1024];
            new Random(42).NextBytes(data);

            using (ThrottledBufferedStream stream = new ThrottledBufferedStream(new MemoryStream(data), 0, 0))
            {
                using (MemoryStream result = new MemoryStream())
                {
                    byte[] buffer = new byte[128 * 1024];
                    int read;
                    while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        result.Write(buffer, 0, read);
                    }

                    Assert.That(result.ToArray(), Is.EqualTo(data));
                    Assert.That(stream.Position, Is.EqualTo(data.Length));
                }
            }
        }

        [Test]
        public void Read_WithStartPosition_SkipsPrefix()
        {
            byte[] data = new byte[1000];
            new Random(7).NextBytes(data);

            using (ThrottledBufferedStream stream = new ThrottledBufferedStream(new MemoryStream(data), 400, 0))
            {
                byte[] buffer = new byte[600];
                int total = 0;
                int read;
                while ((read = stream.Read(buffer, total, buffer.Length - total)) > 0)
                {
                    total += read;
                }

                Assert.That(total, Is.EqualTo(600));
                Assert.That(buffer, Is.EqualTo(data.AsSpan(400).ToArray()));
            }
        }

        [Test]
        public void Read_WhenBaseStreamFails_ThrowsInsteadOfHanging()
        {
            byte[] data = new byte[1000];
            new Random(1).NextBytes(data);

            Task<Exception> readTask = Task.Run(() =>
            {
                try
                {
                    using (ThrottledBufferedStream stream = new ThrottledBufferedStream(new FailingStream(data), 0, 0))
                    {
                        byte[] buffer = new byte[128 * 1024];
                        while (stream.Read(buffer, 0, buffer.Length) > 0)
                        {
                        }
                    }

                    return (Exception)null;
                }
                catch (Exception e)
                {
                    return e;
                }
            });

            Assert.That(readTask.Wait(ThrottledBufferedStreamTests.timeout), Is.True, "Read hung instead of surfacing the fill failure.");
            Assert.That(readTask.Result, Is.TypeOf<IOException>());
            Assert.That(readTask.Result.InnerException, Is.TypeOf<IOException>());
        }

        [Test]
        public void Dispose_ReleasesFileLock()
        {
            //BUG-15: a replaced or finished upload stream must not keep the video file locked
            string filePath = Path.Combine(TestContext.CurrentContext.WorkDirectory, "throttled_stream_lock_test.bin");
            File.WriteAllBytes(filePath, new byte[1000]);

            try
            {
                ThrottledBufferedStream stream = new ThrottledBufferedStream(filePath, 0, 0);
                stream.Dispose();

                using (FileStream exclusive = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    Assert.That(exclusive.Length, Is.EqualTo(1000));
                }
            }
            finally
            {
                File.Delete(filePath);
            }
        }

        [Test]
        public void Dispose_UnblocksWaitingReader()
        {
            BlockingStream baseStream = new BlockingStream();
            ThrottledBufferedStream stream = new ThrottledBufferedStream(baseStream, 0, 0);

            Task readTask = Task.Run(() =>
            {
                try
                {
                    byte[] buffer = new byte[4096];
#pragma warning disable CA2022 //the read result is irrelevant here, only unblocking matters
                    stream.Read(buffer, 0, buffer.Length);
#pragma warning restore CA2022
                }
                catch (Exception)
                {
                    //any exception is fine, only hanging is the bug
                }
            });

            //give the reader time to block on the empty buffer, then dispose
            Thread.Sleep(1000);
            stream.Dispose();
            baseStream.Release();

            Assert.That(readTask.Wait(ThrottledBufferedStreamTests.timeout), Is.True, "Read stayed blocked after Dispose.");
        }

        //BUG-29: the speed history was enumerated partly outside its lock, so
        //polling the speed while reading could throw (dictionary modified
        //during enumeration, Min() on an emptied history).
        [Test]
        public async Task ConcurrentReadAndSpeedPolling_DoesNotThrow()
        {
            byte[] data = new byte[4 * 1024 * 1024];
            new Random(42).NextBytes(data);

            using (ThrottledBufferedStream stream = new ThrottledBufferedStream(new MemoryStream(data), 0, 0))
            {
                using (CancellationTokenSource stop = new CancellationTokenSource())
                {
                    Task[] pollers = new Task[4];
                    for (int i = 0; i < pollers.Length; i++)
                    {
                        pollers[i] = Task.Run(() =>
                        {
                            while (!stop.IsCancellationRequested)
                            {
#pragma warning disable CA2022
                                int speed = stream.CurrentSpeedInBytesPerSecond;
#pragma warning restore CA2022
                                Assert.That(speed, Is.GreaterThanOrEqualTo(0));
                            }
                        });
                    }

                    byte[] buffer = new byte[64 * 1024];
                    int read;
                    long total = 0;
                    while ((read = await stream.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false)) > 0)
                    {
                        total += read;
                    }

                    stop.Cancel();
                    await Task.WhenAll(pollers).ConfigureAwait(false);
                    Assert.That(total, Is.EqualTo(data.Length));
                }
            }
        }
    }
}
