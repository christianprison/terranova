using NUnit.Framework;
using Terranova.Core;

namespace Terranova.Tests.EditMode
{
    /// <summary>
    /// Tests for the EventBus system.
    /// </summary>
    public class EventBusTests
    {
        // Test event type
        private struct TestEvent
        {
            public int Value;
        }

        [SetUp]
        public void SetUp()
        {
            // Clean state before each test
            EventBus.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Clear();
        }

        [Test]
        public void Publish_SubscriberReceivesEvent()
        {
            int received = -1;
            EventBus.Subscribe<TestEvent>(e => received = e.Value);

            EventBus.Publish(new TestEvent { Value = 42 });

            Assert.AreEqual(42, received);
        }

        [Test]
        public void Publish_MultipleSubscribers_AllReceive()
        {
            int count = 0;
            EventBus.Subscribe<TestEvent>(_ => count++);
            EventBus.Subscribe<TestEvent>(_ => count++);
            EventBus.Subscribe<TestEvent>(_ => count++);

            EventBus.Publish(new TestEvent());

            Assert.AreEqual(3, count);
        }

        [Test]
        public void Unsubscribe_StopsReceivingEvents()
        {
            int count = 0;
            void Handler(TestEvent _) => count++;

            EventBus.Subscribe<TestEvent>(Handler);
            EventBus.Publish(new TestEvent());
            Assert.AreEqual(1, count);

            EventBus.Unsubscribe<TestEvent>(Handler);
            EventBus.Publish(new TestEvent());
            Assert.AreEqual(1, count, "Should not receive events after unsubscribe");
        }

        [Test]
        public void Publish_NoSubscribers_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => EventBus.Publish(new TestEvent()));
        }

        [Test]
        public void Clear_RemovesAllSubscriptions()
        {
            int count = 0;
            EventBus.Subscribe<TestEvent>(_ => count++);

            EventBus.Clear();
            EventBus.Publish(new TestEvent());

            Assert.AreEqual(0, count);
        }
    }
}
