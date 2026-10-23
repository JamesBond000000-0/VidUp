using System;
using System.Threading;

namespace Drexel.VidUp.UI.EventAggregation
{
    public class Subscription : IDisposable
    {
        private Action remove;

        public Subscription(Action remove)
        {
            this.remove = remove;
        }
        public void Dispose()
        {
            //idempotent and thread safe: only the first Dispose unregisters (BUG-14)
            Interlocked.Exchange(ref this.remove, null)?.Invoke();
        }
    }
}
