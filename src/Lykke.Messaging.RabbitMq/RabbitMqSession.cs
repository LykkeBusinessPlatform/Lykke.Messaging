using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reactive.Disposables;
using System.Text;
using RabbitMQ.Client;
using Lykke.Messaging.Contract;
using Lykke.Messaging.RabbitMq.Retry;
using Lykke.Messaging.Transports;
using Microsoft.Extensions.Logging;

namespace Lykke.Messaging.RabbitMq
{
    internal class RabbitMqSession : IMessagingSession
    {
        private readonly IModel _channel;
        private readonly CompositeDisposable m_Subscriptions = new CompositeDisposable();
        private readonly Dictionary<string, DefaultBasicConsumer> m_Consumers = new Dictionary<string, DefaultBasicConsumer>();
        private readonly IRetryPolicyProvider _retryPolicyProvider;

        private readonly bool m_ConfirmedSending = false;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<RabbitMqSession> _logger;

        public RabbitMqSession(
            ILoggerFactory loggerFactory,
            IAutorecoveringConnection connection,
            IRetryPolicyProvider retryPolicyProvider,
            bool confirmedSending = false)
        {
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            _retryPolicyProvider = retryPolicyProvider ?? throw new ArgumentNullException(nameof(retryPolicyProvider));
            _logger = loggerFactory.CreateLogger<RabbitMqSession>();

            // TODO: bad idea to init channel in ctor
            _channel = _retryPolicyProvider.RegularPolicy.Execute(connection.CreateModel);
            if (confirmedSending)
                _retryPolicyProvider.RegularPolicy.Execute(() => _channel.ConfirmSelect());
            
            
            //NOTE: looks like publish confirm is required for guaranteed delivery
            //smth like:
            //  m_Model.ConfirmSelect();
            //and publish like this:
            //  m_Model.BasicPublish()
            //  m_Model.WaitForConfirmsOrDie();
            //it will wait for ack from server and throw exception if message failed to persist ons srever side (e.g. broker reboot)
            //more info here: http://rianjs.net/2013/12/publisher-confirms-with-rabbitmq-and-c-sharp

            _retryPolicyProvider.RegularPolicy.Execute(() => _channel.BasicQos(0, 300, false));
        }

        public Destination CreateTemporaryDestination()
        {
            var queueName = _retryPolicyProvider.RegularPolicy.Execute(() => _channel.QueueDeclare()).QueueName;
            return new Destination(new PublicationAddress("direct", "", queueName).ToString(), queueName);
        }

        public void Send(string destination, BinaryMessage message, int ttl)
        {
            Send(destination, message, properties =>
                {
                    if (ttl > 0) properties.Expiration = ttl.ToString(CultureInfo.InvariantCulture);
                });
        }

        private void Send(string destination, BinaryMessage message, Action<IBasicProperties> tuneMessage = null)
        {
            var publicationAddress = PublicationAddress.Parse(destination) ?? new PublicationAddress("direct", destination, "");
            Send(publicationAddress, message, tuneMessage);
        }

        private void Send(PublicationAddress destination,
            BinaryMessage message,
            Action<IBasicProperties> tuneMessage = null)
        {
            var properties = _channel.CreateBasicProperties();

            properties.Headers = new Dictionary<string, object>();
            properties.DeliveryMode = 2; //persistent
            foreach (var header in message.Headers)
            {
                properties.Headers[header.Key] = header.Value;
            }

            if (message.Type != null)
                properties.Type = message.Type;
            tuneMessage?.Invoke(properties);

            properties.Headers.Add("initialRoute", destination.ToString());
            lock (_channel)
            {
                _retryPolicyProvider.RegularPolicy.Execute(() =>
                {
                    _channel.BasicPublish(destination.ExchangeName,
                        destination.RoutingKey,
                        true,
                        properties,
                        message.Bytes);
                    if (m_ConfirmedSending)
                        _channel.WaitForConfirmsOrDie();
                });
            }
        }

        public RequestHandle SendRequest(string destination, BinaryMessage message, Action<BinaryMessage> callback)
        {
            string queue;
            lock (_channel)
            {
                queue = _retryPolicyProvider.RegularPolicy.Execute(() => _channel.QueueDeclare()).QueueName;
            }

            var request = new RequestHandle(callback, () => { }, cb => Subscribe(queue, (binaryMessage, acknowledge) => { 
                cb(binaryMessage);
                acknowledge(true);
            }, null));
            m_Subscriptions.Add(request);
// ReSharper disable ImplicitlyCapturedClosure
            Send(destination, message, p => p.ReplyTo = new PublicationAddress("direct", "", queue).ToString());
// ReSharper restore ImplicitlyCapturedClosure
            return request;
        }

        public IDisposable RegisterHandler(string destination, Func<BinaryMessage, BinaryMessage> handler, string messageType)
        {
           
            var subscription = Subscribe(destination, (properties, bytes,acknowledge) =>
            {
                var correlationId = properties.CorrelationId;
                var responseBytes = handler(ToBinaryMessage(properties, bytes));
                //If replyTo is not parsable we treat it as queue name and message is sent via default exchange  (http://www.rabbitmq.com/tutorials/amqp-concepts.html#exchange-default)
                var publicationAddress = PublicationAddress.Parse(properties.ReplyTo) ?? new PublicationAddress("direct", "", properties.ReplyTo);
                Send(publicationAddress, responseBytes, p =>
                    {
                        if (correlationId != null)
                            p.CorrelationId = correlationId;
                    });
                acknowledge(true);
            }, messageType);

            return subscription;
        }

        public IDisposable Subscribe(string destination, Action<BinaryMessage, Action<bool>> callback, string messageType)
        {
            return Subscribe(destination, (properties, bytes, acknowledge) => callback(ToBinaryMessage(properties, bytes), acknowledge), messageType);
        }

        private BinaryMessage ToBinaryMessage(IBasicProperties properties, ReadOnlyMemory<byte> bytes)
        {
            var binaryMessage = new BinaryMessage {Bytes = bytes.ToArray(), Type = properties.Type};
            if (properties.Headers != null)
            {
                foreach (var header in properties.Headers)
                {
                    var value = header.Value as byte[];
                    binaryMessage.Headers[header.Key] = value == null ? null : Encoding.UTF8.GetString(value);
                }
            }
            return binaryMessage;
        }

        private IDisposable Subscribe(string destination, Action<IBasicProperties, ReadOnlyMemory<byte>, Action<bool>> callback, string messageType)
        {
            lock (m_Consumers)
            {
                DefaultBasicConsumer basicConsumer;
                m_Consumers.TryGetValue(destination, out basicConsumer);
                if (messageType == null)
                {
                    if (basicConsumer is SharedConsumer)
                        throw new InvalidOperationException("Attempt to subscribe for shared destination without specifying message type. It should be a bug in MessagingEngine");
                    if (basicConsumer != null)
                        throw new InvalidOperationException("Attempt to subscribe for same destination twice.");
                    return SubscribeNonShared(destination, callback);
                }

                if (basicConsumer is Consumer)
                    throw new InvalidOperationException("Attempt to subscribe for non shared destination with specific message type. It should be a bug in MessagingEngine");

                return SubscribeShared(
                    destination,
                    callback,
                    messageType,
                    basicConsumer as SharedConsumer);
            }
        }

        private IDisposable SubscribeShared(
            string destination,
            Action<IBasicProperties, ReadOnlyMemory<byte>, Action<bool>> callback,
            string messageType,
            SharedConsumer consumer)
        {
            if (consumer == null)
            {
                consumer = new SharedConsumer(_loggerFactory, _channel);
                m_Consumers[destination] = consumer;
                lock (_channel)
                {
                    _retryPolicyProvider.RegularPolicy.Execute(() => _channel.BasicConsume(destination, false, consumer));
                }
            }

            consumer.AddCallback(callback, messageType);

            return Disposable.Create(() =>
                {
                    lock (m_Consumers)
                    {
                        if (!consumer.RemoveCallback(messageType))
                            m_Consumers.Remove(destination);
                    }
                });
        }

        private IDisposable SubscribeNonShared(string destination, Action<IBasicProperties, ReadOnlyMemory<byte>, Action<bool>> callback)
        {
            var consumer = new Consumer(_loggerFactory, _channel, callback);

            lock (_channel)
            {
                _retryPolicyProvider.RegularPolicy.Execute(() => _channel.BasicConsume(destination, false, consumer));
            }

            m_Consumers[destination] = consumer;
            // ReSharper disable ImplicitlyCapturedClosure
            return Disposable.Create(() =>
                {
                    lock (m_Consumers)
                    {
                        consumer.Dispose();
                        m_Consumers.Remove(destination);
                    }
                });
            // ReSharper restore ImplicitlyCapturedClosure
        }

        public void Dispose()
        {
            lock (m_Consumers)
            {
                foreach (var c in m_Consumers.Values)
                {
                    if (c is IDisposable consumer)
                        consumer.Dispose();
                }
            }
            
            lock (_channel)
            {
                try
                {
                    _channel.Close(200, "Goodbye");
                    _channel.Dispose();
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "{Method}: ", nameof(Dispose));
                }
            }
        }
    }
}