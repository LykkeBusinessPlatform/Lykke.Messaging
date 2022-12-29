using System;
using System.Linq;
using System.Threading;
using Lykke.Messaging.Contract;
using Lykke.Messaging.InMemory;
using Lykke.Messaging.Transports;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;

namespace Lykke.Messaging.Tests
{
    // ReSharper disable InconsistentNaming
    // ReSharper disable PossibleNullReferenceException

    [TestFixture]
    public class TransportManagerTests : IDisposable
    {
        private readonly ILoggerFactory _loggerFactory;

        public TransportManagerTests()
        {
            _loggerFactory = NullLoggerFactory.Instance;
        }

        public void Dispose()
        {
            _loggerFactory?.Dispose();
        }

        private class TransportConstants
        {
            public const string TRANSPORT_ID1 = "tr1";
            public const string TRANSPORT_ID2 = "tr2";
            public const string TRANSPORT_ID3 = "tr3";
            public const string USERNAME = "test";
            public const string PASSWORD = "test";
            public const string BROKER = "test";
        }

        private static ITransportInfoResolver MockTransportResolver()
        {
            var resolver = new Mock<ITransportInfoResolver>();
            resolver
                .Setup(r => r.Resolve(TransportConstants.TRANSPORT_ID1))
                .Returns(new TransportInfo(TransportConstants.BROKER, TransportConstants.USERNAME, TransportConstants.PASSWORD, "MachineName", "InMemory") );
            resolver
                .Setup(r => r.Resolve(TransportConstants.TRANSPORT_ID2))
                .Returns(new TransportInfo(TransportConstants.BROKER, TransportConstants.USERNAME, TransportConstants.PASSWORD, "MachineName", "InMemory") );
            resolver
                .Setup(r => r.Resolve(TransportConstants.TRANSPORT_ID3))
                .Returns(new TransportInfo(TransportConstants.BROKER, TransportConstants.USERNAME, TransportConstants.PASSWORD, "MachineName", "Mock") );
            return resolver.Object;
        }

        [Test]
        public void MessagingSessionFailureCallbackTest()
        {
            var resolver = MockTransportResolver();
            Action createdSessionOnFailure = () => { Console.WriteLine("!!"); };
            var transport = new Mock<ITransport>();
            transport
                .Setup(t => t.CreateSession(It.IsAny<Action>(), It.IsAny<string>()))
                .Callback<Action, string>((invocation, displayName) => createdSessionOnFailure = invocation);
            var factory = new Mock<ITransportFactory>();
            factory.Setup(f => f.Create(It.IsAny<TransportInfo>(), It.IsAny<Action>())).Returns(transport.Object);
            factory.Setup(f => f.Name).Returns("Mock");
            var transportManager = new TransportManager(_loggerFactory, resolver, factory.Object);
            int i = 0;

            transportManager.GetMessagingSession(
                new Endpoint(TransportConstants.TRANSPORT_ID3, "queue"),
                "test",
                () => { Interlocked.Increment(ref i); });

            createdSessionOnFailure();
            createdSessionOnFailure();

            Assert.That(i, Is.Not.EqualTo(0),"Session failure callback was not called");
            Assert.That(i, Is.EqualTo(1), "Session  failure callback was called twice");
        }

        [Test]
        public void ConcurrentTransportResolutionTest()
        {
            var resolver = MockTransportResolver();
            var transportManager = new TransportManager(_loggerFactory, resolver, new InMemoryTransportFactory());
            var start = new ManualResetEvent(false);
            int errorCount = 0;
            int attemptCount = 0;

            foreach (var i in Enumerable.Range(1, 10))
            {
                var thread = new Thread(() =>
                {
                    start.WaitOne();
                    try
                    {
                        transportManager.GetMessagingSession(
                            new Endpoint(TransportConstants.TRANSPORT_ID1, "queue"),
                            "test");
                        Interlocked.Increment(ref attemptCount);
                    }
                    catch (Exception)
                    {
                        Interlocked.Increment(ref errorCount);
                    }
                });
                thread.Start();
            }

            start.Set();
            while (attemptCount < 10)
            {
                Thread.Sleep(50);
            }

            Assert.That(errorCount, Is.EqualTo(0));
        }
    }

    // ReSharper restore InconsistentNaming
    // ReSharper restore PossibleNullReferenceException
}