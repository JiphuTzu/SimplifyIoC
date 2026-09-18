// Signal 行为快照测试。
using System.Collections.Generic;
using NUnit.Framework;
using SimplifyIoC.Signals;

namespace SimplifyIoC.Tests
{
    public class SignalTests
    {
        private int _counter;

        private void Handler() => _counter++;

        [SetUp]
        public void SetUp()
        {
            _counter = 0;
        }

        [Test]
        public void AddListenerThenDispatchInvokesListener()
        {
            var signal = new Signal();
            signal.AddListener(Handler);

            signal.Dispatch();

            Assert.AreEqual(1, _counter);
        }

        [Test]
        public void RemoveListenerStopsInvocation()
        {
            var signal = new Signal();
            signal.AddListener(Handler);

            signal.Dispatch();
            signal.RemoveListener(Handler);
            signal.Dispatch();

            Assert.AreEqual(1, _counter);
        }

        [Test]
        public void AddOnceFiresExactlyOnce()
        {
            var signal = new Signal();
            signal.AddOnce(Handler);

            signal.Dispatch();
            signal.Dispatch();

            Assert.AreEqual(1, _counter);
        }

        [Test]
        public void AddListenerSameCallbackTwiceFiresOnce()
        {
            var signal = new Signal();
            signal.AddListener(Handler);
            signal.AddListener(Handler); // AddUnique 应去重
            signal.Dispatch();

            Assert.AreEqual(1, _counter);
        }

        [Test]
        public void RemoveAllListenersClearsEverything()
        {
            var signal = new Signal();
            signal.AddListener(Handler);
            signal.AddOnce(Handler);

            signal.RemoveAllListeners();
            signal.Dispatch();

            Assert.AreEqual(0, _counter);
        }

        [Test]
        public void ValueSignalDeliversPayload()
        {
            var received = new List<int>();
            var signal = new Signal<int>();
            signal.AddListener(received.Add);

            signal.Dispatch(42);
            signal.Dispatch(7);

            Assert.AreEqual(new[] { 42, 7 }, received);
        }

        [Test]
        public void ValueSignalGetTypesReportsPayloadTypes()
        {
            var signal = new Signal<int>();
            Assert.AreEqual(new[] { typeof(int) }, signal.GetTypes());

            var bare = new Signal();
            Assert.AreEqual(0, bare.GetTypes().Count);
        }
    }
}
