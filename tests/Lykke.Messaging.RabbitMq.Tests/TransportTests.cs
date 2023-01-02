﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Lykke.Messaging.Contract;
using Lykke.Messaging.Serialization;
using Lykke.Messaging.Transports;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using RabbitMQ.Client;
using ThreadState = System.Threading.ThreadState;

namespace Lykke.Messaging.RabbitMq.Tests
{
    [TestFixture(Ignore = "Tests are broken")]
    public class TransportTests
    {
        [SetUp]
        public void Setup()
        {
            m_Connection = m_Factory.CreateConnection();
            m_Channel = m_Connection.CreateModel();
            Console.WriteLine("Purging queue {0}", TEST_QUEUE);
            m_Channel.QueuePurge(TEST_QUEUE);
            m_TempQueue = m_Channel.QueueDeclare().QueueName;
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                m_Channel.Dispose();
                m_Connection.Dispose();
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception in teardown: {0}",e);
            }
        }

        private const string TEST_QUEUE = "test.queue";
        private const string TEST_EXCHANGE = "test.exchange";
        private const string HOST = "localhost";
        private IConnection m_Connection;
        private IModel m_Channel;
        private ConnectionFactory m_Factory;
        private string m_TempQueue;
        
        [OneTimeSetUp]
        public void TestFixtureSetUp()
        {
            m_Factory = new ConnectionFactory { HostName = HOST, UserName = "guest", Password = "guest" };
            using (IConnection connection = m_Factory.CreateConnection())
            using (IModel channel = connection.CreateModel())
            {
                try
                {
                    channel.QueueDelete(TEST_QUEUE);
                }
                catch
                {
                }
            }
            using (IConnection connection = m_Factory.CreateConnection())
            using (IModel channel = connection.CreateModel())
            {
                try
                {
                    channel.ExchangeDelete(TEST_EXCHANGE);
                }
                catch
                {
                }
            }
            using (IConnection connection = m_Factory.CreateConnection())
            using (IModel channel = connection.CreateModel())
            {
                channel.ExchangeDeclare(TEST_EXCHANGE, "direct", false);
                channel.QueueDeclare(TEST_QUEUE, false, false, false, null);
                channel.QueueBind(TEST_QUEUE, TEST_EXCHANGE, "");
            }
        }


        [Test]
        public void SendTest()
        {
            using (var transport = new RabbitMqTransport(NullLoggerFactory.Instance, HOST, "guest", "guest"))
            {
                var delivered=new ManualResetEvent(false);
                IMessagingSession messagingSession = transport.CreateSession();
                messagingSession.Send(TEST_EXCHANGE, new BinaryMessage {Bytes = new byte[] {0x0, 0x1, 0x2}, Type = typeof (byte[]).Name}, 0);
                messagingSession.Subscribe(TEST_QUEUE, (message, acknowledge) =>
                    {
                        Console.WriteLine("message:" + message.Type);
                        delivered.Set();
                    }, typeof (byte[]).Name);
                Assert.That(delivered.WaitOne(1000),Is.True,"Message was not delivered");
            }
        }

        [Test]
        public void AckTest()
        {
            using (var transport = new RabbitMqTransport(NullLoggerFactory.Instance, HOST, "guest", "guest"))
            {
                var delivered=new ManualResetEvent(false);
                IMessagingSession messagingSession = transport.CreateSession();
                messagingSession.Send(TEST_EXCHANGE, new BinaryMessage {Bytes = new byte[] {0x0, 0x1, 0x2}, Type = typeof (byte[]).Name}, 0);
                messagingSession.Subscribe(TEST_QUEUE, (message, acknowledge) =>
                    {
                        Console.WriteLine("message:" + message.Type);
                        delivered.Set();
                        acknowledge(true);
                    }, typeof (byte[]).Name);
                Assert.That(delivered.WaitOne(1000),Is.True,"Message was not delivered");
            }

            using (var transport = new RabbitMqTransport(NullLoggerFactory.Instance, HOST, "guest", "guest"))
            {
                var delivered = new ManualResetEvent(false);
                IMessagingSession messagingSession = transport.CreateSession();
                messagingSession.Subscribe(TEST_QUEUE, (message, acknowledge) => delivered.Set(), typeof(byte[]).Name);
                Assert.That(delivered.WaitOne(500), Is.False, "Message was returned to queue");
            }
        }
        [Test]
        public void NackTest()
        {
            using (var transport = new RabbitMqTransport(NullLoggerFactory.Instance, HOST, "guest", "guest"))
            {
                var delivered=new ManualResetEvent(false);
                IMessagingSession messagingSession = transport.CreateSession();
                messagingSession.Send(TEST_EXCHANGE, new BinaryMessage {Bytes = new byte[] {0x0, 0x1, 0x2}, Type = typeof (byte[]).Name}, 0);
                messagingSession.Subscribe(TEST_QUEUE, (message, acknowledge) =>
                    {
                        Console.WriteLine("message:" + message.Type);
                        delivered.Set();
                        acknowledge(false);
                    }, typeof (byte[]).Name);
                Assert.That(delivered.WaitOne(300),Is.True,"Message was not delivered");
            }

            using (var transport = new RabbitMqTransport(NullLoggerFactory.Instance, HOST, "guest", "guest"))
            {
                var delivered = new ManualResetEvent(false);
                IMessagingSession messagingSession = transport.CreateSession();
                messagingSession.Subscribe(TEST_QUEUE, (message, acknowledge) => delivered.Set(), typeof(byte[]).Name);
                Assert.That(delivered.WaitOne(1000), Is.True, "Message was not returned to queue");
            }
        }


        [Test]
        public void RpcTest()
        {
            using (var transport = new RabbitMqTransport(NullLoggerFactory.Instance, HOST, "guest", "guest"))
            {
                var request = new byte[] {0x0, 0x1, 0x2};
                var response = new byte[] {0x2, 0x1, 0x0};
                byte[] actualResponse = null;
                var received = new ManualResetEvent(false);

                var session = transport.CreateSession();
                session.RegisterHandler(TEST_QUEUE, message => new BinaryMessage {Bytes = response, Type = typeof (byte[]).Name}, null);
                session.SendRequest(TEST_EXCHANGE, new BinaryMessage { Bytes = request, Type = typeof(byte[]).Name }, message =>
                    {
                        actualResponse = message.Bytes;
                        received.Set();
                    });
                Assert.That(received.WaitOne(500), Is.True, "Response was not received");
                Assert.That(actualResponse, Is.EqualTo(response), "Received response does not match sent one");
            }
        }

        [Test]
        [TestCase(null, TestName = "Non shared destination")]
        [TestCase("test", TestName = "Shared destination")]
        public void UnsubscribeTest(string messageType)
        {
            using (var transport = new RabbitMqTransport(NullLoggerFactory.Instance, HOST, "guest", "guest"))
            {
                var ev = new AutoResetEvent(false);
                IMessagingSession messagingSession = transport.CreateSession();
                messagingSession.Send(TEST_EXCHANGE, new BinaryMessage {Bytes = new byte[] {0x0, 0x1, 0x2}, Type = messageType}, 0);
                IDisposable subscription = messagingSession.Subscribe(TEST_QUEUE, (message, acknowledge) => ev.Set(), messageType);
                Assert.That(ev.WaitOne(500), Is.True, "Message was not delivered");
                subscription.Dispose();
                Assert.That(ev.WaitOne(500), Is.False, "Message was delivered for canceled subscription");
            }
        }

        [Test]
        public void HandlerWaitStopsAndMessageOfUnknownTypeReturnsToQueueOnUnsubscribeTest()
        {
            using (var transport = new RabbitMqTransport(NullLoggerFactory.Instance, HOST, "guest", "guest"))
            {
                IMessagingSession messagingSession = transport.CreateSession();
                var received = new AutoResetEvent(false);
                IDisposable subscription = messagingSession.Subscribe(TEST_QUEUE, (message, acknowledge) =>
                    {
                        received.Set();
                        Console.WriteLine(Thread.CurrentThread.ManagedThreadId);
                    }, "type2");
                messagingSession.Send(TEST_EXCHANGE, new BinaryMessage {Bytes = new byte[] {0x0, 0x1, 0x2}, Type = "type1"}, 0);
                Assert.That(received.WaitOne(500), Is.False, "Message of not subscribed type has not paused processing");
                subscription.Dispose();
                messagingSession.Subscribe(TEST_QUEUE, (message, acknowledge) => received.Set(), "type1");
                Assert.That(received.WaitOne(500), Is.True, "Message was not returned to queue");
            }
        }

        [Test]
        public void MessageOfUnknownTypeShouldPauseProcessingTillCorrespondingHandlerIsRegisteredTest()
        {
            using (var transport = new RabbitMqTransport(NullLoggerFactory.Instance, HOST, "guest", "guest"))
            {
                IMessagingSession messagingSession = transport.CreateSession();
                var type1Received = new AutoResetEvent(false);
                var type2Received = new AutoResetEvent(false);

                messagingSession.Subscribe(TEST_QUEUE, (message, acknowledge) =>
                    {
                        type1Received.Set();
                        acknowledge(true);
                    }, "type1");

                messagingSession.Send(TEST_EXCHANGE, new BinaryMessage {Bytes = new byte[] {0x0, 0x1, 0x2}, Type = "type1"}, 0);
                Assert.That(type1Received.WaitOne(500), Is.True, "Message of subscribed type was not delivered");
                messagingSession.Send(TEST_EXCHANGE, new BinaryMessage {Bytes = new byte[] {0x0, 0x1, 0x2}, Type = "type2"}, 0);
                //Give time for type2 message to be  pushed back by mq
                //Thread.Sleep(500);
                messagingSession.Send(TEST_EXCHANGE, new BinaryMessage {Bytes = new byte[] {0x0, 0x1, 0x2}, Type = "type1"}, 0);
                Assert.That(type1Received.WaitOne(500), Is.False, "Message of not subscribed type has not paused processing");
                Assert.That(type2Received.WaitOne(500), Is.False, "Message of not subscribed type has not paused processing");
                messagingSession.Subscribe(
                    TEST_QUEUE,
                    (message, acknowledge) =>
                    {
                        type2Received.Set();
                        acknowledge(true);
                    },
                    "type2");
                Assert.That(type1Received.WaitOne(500), Is.True, "Processing was not resumed after handler for unknown message type was registered");
                Assert.That(type2Received.WaitOne(500), Is.True, "Processing was not resumed after handler for unknown message type was registered");
            }
        }

        [Test]
        public void UnknownMessageTypeHandlerWaitingDoesNotPreventTransportDisposeTest()
        {
            var received = new ManualResetEvent(false);
            Thread connectionThread = null;
            using (var transport = new RabbitMqTransport(NullLoggerFactory.Instance, HOST, "guest", "guest"))
            {
                IMessagingSession messagingSession = transport.CreateSession();
                messagingSession.Subscribe(TEST_QUEUE, (message, acknowledge) =>
                    {
                        connectionThread = Thread.CurrentThread;
                        received.Set();
                    }, "type1");
                messagingSession.Send(TEST_EXCHANGE, new BinaryMessage {Bytes = new byte[] {0x0, 0x1, 0x2}, Type = "type1"}, 0);
                Assert.That(received.WaitOne(100), Is.True, "Message was not delivered");
                messagingSession.Send(TEST_EXCHANGE, new BinaryMessage {Bytes = new byte[] {0x0, 0x1, 0x2}, Type = "type2"}, 0);
            }
            GC.Collect();
            Thread.Sleep(30000); 
            Assert.That(connectionThread.ThreadState, Is.EqualTo(ThreadState.Stopped), "Processing thread is still active in spite of transport dispose");
        }

        [Test]
        [Ignore("integration")]
        [TestCase(10, true,TestName = "10b confirmed")]
        [TestCase(10, false,TestName = "10b")]
        [TestCase(1024, false, TestName = "1Kb")]
        [TestCase(8912, false, TestName = "8Kb")]
        [TestCase(1024 * 1024, false, TestName = "1Mb")]
        public void PerformanceTest(int messageSize, bool confirmedSending)
        {
            var messageBytes = new byte[messageSize];
            new Random().NextBytes(messageBytes);

            using (var transport = new RabbitMqTransport(NullLoggerFactory.Instance, HOST, "guest", "guest"))
            {
                IMessagingSession messagingSession = transport.CreateSession();
                Stopwatch sw = Stopwatch.StartNew();
                messagingSession.Send(TEST_EXCHANGE, new BinaryMessage { Bytes = messageBytes, Type = typeof(byte[]).Name }, 0);
                int sendCounter;
                for (sendCounter = 0; sw.ElapsedMilliseconds < 4000; sendCounter++)
                    messagingSession.Send(TEST_EXCHANGE, new BinaryMessage {Bytes = messageBytes, Type = typeof (byte[]).Name}, 0);
                int receiveCounter = 0;

                var ev = new ManualResetEvent(false);
                messagingSession.Subscribe(TEST_QUEUE, (message, acknowledge) => receiveCounter++, typeof(byte[]).Name);
                ev.WaitOne(2000);
                Console.WriteLine("Send: {0} per second. {1:0.00} Mbit/s", sendCounter/4, 1.0*sendCounter*messageSize/4/1024/1024*8);
                Console.WriteLine("Receive: {0} per second. {1:0.00}  Mbit/s", receiveCounter / 2, 1.0 * receiveCounter * messageSize / 2 / 1024 / 1024 * 8);
            }
        }


        [Test]
        [Ignore("integration")]
        public void EndToEndRabbitResubscriptionTest()
        {

            var messagingEngine = new MessagingEngine(
                NullLoggerFactory.Instance,
                new TransportInfoResolver(new Dictionary<string, TransportInfo> {{"test", new TransportInfo(HOST, "guest", "guest", null, "RabbitMq")}}),
                new RabbitMqTransportFactory(NullLoggerFactory.Instance));

            using (messagingEngine)
            {
                for (int i = 0; i < 100; i++)
                {
                    messagingEngine.Send(i, new Endpoint("test", new Destination(TEST_EXCHANGE), serializationFormat: SerializationFormat.Json));
                }
               
                messagingEngine.Subscribe<int>(new Endpoint("test", new Destination(TEST_QUEUE), serializationFormat: SerializationFormat.Json), message =>
                {
                    Console.WriteLine(message+"\n");
                    Thread.Sleep(1000);
                });

                Thread.Sleep(30*60*1000);
            }
            Console.WriteLine("Done");
        }


        [Test]
        public void EndpointVerificationTest()
        {
            using (var transport = new RabbitMqTransport(NullLoggerFactory.Instance, HOST, "guest", "guest"))
            {
                var res = transport.VerifyDestination(new Destination("unistream.processing.events"), EndpointUsage.Publish | EndpointUsage.Subscribe, false, out var error);
                Console.WriteLine(error);
                Assert.That(res,Is.False);
            }
        }

        [Test]
        public void DefaultExchangeVerificationTest()
        {
            var defaultExchangeDestination = new Destination(
                new PublicationAddress("direct", "", m_TempQueue).ToString(),
                m_TempQueue
            );

            using (var transport = new RabbitMqTransport(NullLoggerFactory.Instance, HOST, "guest", "guest"))
            {
                var res = transport.VerifyDestination(defaultExchangeDestination, EndpointUsage.Publish | EndpointUsage.Subscribe, true, out var error);
                Console.WriteLine(error);
                Assert.That(res,Is.True);
            }
        }

        [Test]
        public void AttemptToSubscribeSameDestinationAndMessageTypeTwiceFailureTest()
        {
            using (var transport = new RabbitMqTransport(NullLoggerFactory.Instance, HOST, "guest", "guest"))
            {
                IMessagingSession messagingSession = transport.CreateSession();
                messagingSession.Subscribe(TEST_QUEUE, (message, acknowledge) => { }, "type1");
                Assert.That(() => messagingSession.Subscribe(TEST_QUEUE, (message, acknowledge) => { }, "type1"), Throws.TypeOf<InvalidOperationException>());
            }
        }

        [Test]
        public void AttemptToSubscribeSharedDestinationWithoutMessageTypeFailureTest()
        {
            using (var transport = new RabbitMqTransport(NullLoggerFactory.Instance, HOST, "guest", "guest"))
            {
                IMessagingSession messagingSession = transport.CreateSession();
                messagingSession.Subscribe(TEST_QUEUE, (message, acknowledge) => { }, "type1");

                Assert.That(() => messagingSession.Subscribe(TEST_QUEUE, (message, acknowledge) => { }, null), Throws.TypeOf<InvalidOperationException>());
            }
        }

        [Test]
        public void AttemptToSubscribeNonSharedDestinationWithMessageTypeFailureTest()
        {
            using (var transport = new RabbitMqTransport(NullLoggerFactory.Instance, HOST, "guest", "guest"))
            {
                IMessagingSession messagingSession = transport.CreateSession();

                Assert.That(() =>
                {
                    messagingSession.Subscribe(TEST_QUEUE, (message, acknowledge) => { }, null);
                    messagingSession.Subscribe(TEST_QUEUE, (message, acknowledge) => { }, "type1");
                }, Throws.TypeOf<InvalidOperationException>());
            }
        }

        [Test]
        public void AttemptToSubscribeSameDestinationWithoutMessageTypeTwiceFailureTest()
        {
            using (var transport = new RabbitMqTransport(NullLoggerFactory.Instance, HOST, "guest", "guest"))
            {
                IMessagingSession messagingSession = transport.CreateSession();

                Assert.That(() =>
                {
                    messagingSession.Subscribe(TEST_QUEUE, (message, acknowledge) => { }, null);
                    messagingSession.Subscribe(TEST_QUEUE, (message, acknowledge) => { }, null);
                }, Throws.TypeOf<InvalidOperationException>());
            }
        }



        [Test]
        public string VerifyPublishEndpointFailureTest()
        {
            var transport = new RabbitMqTransport(NullLoggerFactory.Instance, HOST, "guest", "guest");
            var valid = transport.VerifyDestination(new Destination("non.existing"), EndpointUsage.Publish, false, out var error);
            Assert.That(valid,Is.False, "endpoint reported as valid");
            Assert.That(error, Is.EqualTo(@"The AMQP operation was interrupted: AMQP close-reason, initiated by Peer, code=404, text=""NOT_FOUND - no exchange 'non.existing' in vhost '/'"", classId=40, methodId=10, cause="));
            return error;
        }

        [Test]
        public string VerifySubscriptionEndpointNoExchangeFailureTest()
        {
            var transport = new RabbitMqTransport(NullLoggerFactory.Instance, HOST, "guest", "guest");
            var valid = transport.VerifyDestination(new Destination("non.existing", "non.existing"), EndpointUsage.Subscribe, false, out var error);
            Assert.That(valid,Is.False, "endpoint reported as valid");
            Assert.That(error, Is.EqualTo(@"The AMQP operation was interrupted: AMQP close-reason, initiated by Peer, code=404, text=""NOT_FOUND - no exchange 'non.existing' in vhost '/'"", classId=40, methodId=10, cause="));
            return error;
        }


        [Test]
        public string VerifySubscriptionEndpointNoQueueFailureTest()
        {
            var transport = new RabbitMqTransport(NullLoggerFactory.Instance, HOST, "guest", "guest");
            var valid = transport.VerifyDestination(new Destination("amq.direct", "non.existing"), EndpointUsage.Subscribe, false, out var error);
            Assert.That(valid,Is.False, "endpoint reported as valid");
            Assert.That(error, Is.EqualTo(@"The AMQP operation was interrupted: AMQP close-reason, initiated by Peer, code=404, text=""NOT_FOUND - no queue 'non.existing' in vhost '/'"", classId=50, methodId=10, cause="));
            return error;
        }


        [Test]
        public void SubscriptionToClusterTest()
        {

            ITransportInfoResolver transportInfoResolver = new TransportInfoResolver(new Dictionary<string, TransportInfo>()
            {
                {"main", new TransportInfo("localhost1,localhost", "guest", "guest", "None", "RabbitMq")},
                {"sendTransport", new TransportInfo("localhost", "guest", "guest", "None", "RabbitMq")}
            });
            var endpoint = new Endpoint("main", new Destination(TEST_EXCHANGE, TEST_QUEUE), true, SerializationFormat.Json);
            var sendEndpoint = new Endpoint("sendTransport", new Destination(TEST_EXCHANGE, TEST_QUEUE), true, SerializationFormat.Json);


            using (var me = new MessagingEngine(NullLoggerFactory.Instance, transportInfoResolver, new RabbitMqTransportFactory(NullLoggerFactory.Instance, false)))
            {
                me.Send(1, sendEndpoint);
                me.ResubscriptionTimeout = 100;
                var received = new ManualResetEvent(false);
                me.Subscribe<int>(endpoint, i => received.Set());
                Assert.That(received.WaitOne(1000), Is.True, "Subscription when first broker in list is not resolvable while next one is ok");
            }
        }
    }
}