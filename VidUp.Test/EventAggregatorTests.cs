using System;
using System.Threading;
using System.Threading.Tasks;
using Drexel.VidUp.UI.EventAggregation;
using NUnit.Framework;

namespace Drexel.VidUp.Test
{
    //Tests for BUG-14: the EventAggregator's unsubscribe was not locked and one
    //throwing handler stopped the delivery to all remaining handlers.
    //A private message type is used so the shared singleton does not interfere
    //with other tests.
    [TestFixture]
    public class EventAggregatorTests
    {
        private sealed class TestMessage
        {
            public int Value { get; set; }
        }

        [Test]
        public void Publish_DeliversToSubscribers()
        {
            int received = 0;
            using (EventAggregator.Instance.Subscribe<TestMessage>(message => received = message.Value))
            {
                EventAggregator.Instance.Publish(new TestMessage { Value = 42 });
            }

            Assert.That(received, Is.EqualTo(42));
        }

        [Test]
        public void DisposedSubscription_NoLongerReceives()
        {
            int count = 0;
            Subscription subscription = EventAggregator.Instance.Subscribe<TestMessage>(message => count++);
            EventAggregator.Instance.Publish(new TestMessage());
            subscription.Dispose();
            EventAggregator.Instance.Publish(new TestMessage());

            Assert.That(count, Is.EqualTo(1));
        }

        [Test]
        public void DoubleDispose_IsSafe()
        {
            Subscription subscription = EventAggregator.Instance.Subscribe<TestMessage>(message => { });
            subscription.Dispose();
            Assert.DoesNotThrow(() => subscription.Dispose());
        }

        [Test]
        public void ThrowingHandler_DoesNotStopDeliveryToOthers()
        {
            int count = 0;
            using (EventAggregator.Instance.Subscribe<TestMessage>(message => throw new InvalidOperationException("test")))
            using (EventAggregator.Instance.Subscribe<TestMessage>(message => count++))
            {
                Assert.DoesNotThrow(() => EventAggregator.Instance.Publish(new TestMessage()));
            }

            Assert.That(count, Is.EqualTo(1));
        }

        [Test]
        public void ConcurrentSubscribePublishUnsubscribe_DoesNotThrow()
        {
            //the unsubscribe used to modify the register without the lock while
            //Publish copied it, which could corrupt the list under load
            Assert.DoesNotThrow(() =>
            {
                Parallel.For(0, 8, worker =>
                {
                    for (int iteration = 0; iteration < 500; iteration++)
                    {
                        Subscription subscription = EventAggregator.Instance.Subscribe<TestMessage>(message => { });
                        EventAggregator.Instance.Publish(new TestMessage());
                        subscription.Dispose();
                    }
                });
            });
        }
    }
}
