using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reactive.Disposables;
using System.Text;
using RabbitMQ.Client;
using Lykke.Messaging.Contract;
using Lykke.Messaging.RabbitMq.Exceptions;
using Lykke.Messaging.RabbitMq.Retry;
using Lykke.Messaging.Transports;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client.Events;

namespace Lykke.Messaging.RabbitMq
{
    internal class RabbitMqSession : IMessagingSession
    {
        private readonly CompositeDisposable m_Subscriptions = new CompositeDisposable();
        private readonly Dictionary<string, DefaultBasicConsumer> m_Consumers = new Dictionary<string, DefaultBasicConsumer>();
        private readonly IRetryPolicyProvider _retryPolicyProvider;
        private readonly bool _publisherConfirms;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<RabbitMqSession> _logger;
        private readonly IAutorecoveringConnection _connection;
        
        private IModel _channel;

        public RabbitMqSession(
            ILoggerFactory loggerFactory,
            IAutorecoveringConnection connection,
            IRetryPolicyProvider retryPolicyProvider,
            bool publisherConfirms = false)
        {
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            _retryPolicyProvider = retryPolicyProvider ?? throw new ArgumentNullException(nameof(retryPolicyProvider));
            _logger = loggerFactory.CreateLogger<RabbitMqSession>();
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _publisherConfirms = publisherConfirms;
        }

        /// <summary>
        /// Creates channel if it was not created yet, executes requested
        /// operation and returns result. If operation fails, the channel might
        /// be closed depending on the failure type. In case of recoverable
        /// failure the exception is wrapped into RecoverableFailureException,
        /// consumer can unharmfully retry.  
        /// </summary>
        /// <param name="operation"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        /// <exception cref="RecoverableFailureException">
        /// When potentially recoverable failure happened
        /// </exception>
        private T ExecuteChannelOperation<T>(Func<T> operation)
        {
            _channel ??= InitializeChannel();

            try
            {
                lock (_channel)
                {
                    return operation();   
                }
            }
            catch (Exception e)
            {
                var counterActions = e.DecideOnFailureActions();
                if (counterActions.HasFlag(FailureActions.CloseChannel))
                    CloseChannel();
                
                if (counterActions.HasFlag(FailureActions.Throw))
                    throw;

                if (counterActions.HasFlag(FailureActions.Retry))
                    throw new RecoverableFailureException(e);

                throw new InvalidOperationException(
                    "Looks like it was decided to just close the RabbitMQ channel on failure. " +
                    "Most of the time it should be combined with retry or rethrow.");
            }
        }

        private void CloseChannel()
        {
            if (_channel == null)
                return;

            lock (_channel)
            {
                _channel.Close();

                if (_channel is IRecoverable recoverable)
                    recoverable.Recovery -= OnChannelRecovered;
                _channel.ModelShutdown -= OnChannelShutdown;
                _channel.BasicReturn -= OnReturn;
                if (_publisherConfirms)
                {
                    _channel.BasicAcks -= OnAck;
                    _channel.BasicNacks -= OnNack;
                }

                _channel.Dispose();
                _channel = null;
            }
        }

        private IModel InitializeChannel()
        {
            var channel = _connection.CreateModel();

            if (_publisherConfirms)
            {
                channel.ConfirmSelect();
                channel.BasicAcks += OnAck;
                channel.BasicNacks += OnNack;
            }
            
            channel.BasicReturn += OnReturn;
            channel.ModelShutdown += OnChannelShutdown;
            
            if (channel is IRecoverable recoverable)
                recoverable.Recovery += OnChannelRecovered;

            channel.BasicQos(0, 300, false);
            
            return channel;
        }
        
        private void OnAck(object? sender, BasicAckEventArgs args)
        {
            _logger.LogDebug("Message {DeliveryTag} was acknowledged", args.DeliveryTag);
        }
        
        private void OnNack(object? sender, BasicNackEventArgs args)
        {
            _logger.LogWarning("Message {DeliveryTag} was not acknowledged", args.DeliveryTag);
        }
        
        private void OnReturn(object? sender, BasicReturnEventArgs args)
        {
            // todo: handle unroutable message, otherwise it will be lost, potentially
            // specific exception can be fired here and consumers can handle it
            _logger.LogError("Message {ReplyCode} was returned", args.ReplyCode);
        }
        
        private void OnChannelShutdown(object? sender, ShutdownEventArgs e)
        {
            // todo: handle channel shutdown
            _logger.LogError(e.Cause as Exception, "Channel was shutdown: {Code} - {Reason}", e.ReplyCode, e.ReplyText);
        }
        
        private void OnChannelRecovered(object? sender, EventArgs e)
        {
            _logger.LogInformation("Channel {Number} was recovered", (sender as IModel)?.ChannelNumber);
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
            ExecuteChannelOperationWithRetry(() =>
            {
                _channel.BasicPublish(destination.ExchangeName,
                    destination.RoutingKey,
                    true,
                    properties,
                    message.Bytes);
                if (_publisherConfirms)
                    _channel.WaitForConfirmsOrDie();
            });
        }

        private void Send(string destination, BinaryMessage message, Action<IBasicProperties> tuneMessage = null)
        {
            var publicationAddress = PublicationAddress.Parse(destination) ?? new PublicationAddress("direct", destination, "");
            Send(publicationAddress, message, tuneMessage);
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
                ExecuteChannelOperationWithRetry(() => _channel.BasicConsume(destination, false, consumer));
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

            ExecuteChannelOperationWithRetry(() => _channel.BasicConsume(destination, false, consumer));

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

        public Destination CreateTemporaryDestination()
        {
            var queueName = ExecuteChannelOperationWithRetry(() => _channel.QueueDeclare()).QueueName;
            return new Destination(new PublicationAddress("direct", "", queueName).ToString(), queueName);
        }

        public void Send(string destination, BinaryMessage message, int ttl)
        {
            Send(destination, message, properties =>
            {
                if (ttl > 0) properties.Expiration = ttl.ToString(CultureInfo.InvariantCulture);
            });
        }

        public RequestHandle SendRequest(string destination, BinaryMessage message, Action<BinaryMessage> callback)
        {
            string queue = ExecuteChannelOperationWithRetry(() => _channel.QueueDeclare()).QueueName;
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
        
        /// <summary>
        /// The operation will be wrapped by retry policy. If the operation fails,
        /// it potentially might be retried if possible. The channel recovery,
        /// if required, will be handled transparently for the consumer.
        /// The generic version of the method.
        /// </summary>
        /// <param name="operation">The delegate to execute</param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T ExecuteChannelOperationWithRetry<T>(Func<T> operation)
        {
            return _retryPolicyProvider.RegularPolicy.Execute(() => ExecuteChannelOperation(operation));
        }

        /// <summary>
        /// The operation will be wrapped by retry policy. If the operation fails,
        /// it potentially might be retried if possible. The channel recovery,
        /// if required, will be handled transparently for the consumer.
        /// </summary>
        /// <param name="operation"></param>
        public void ExecuteChannelOperationWithRetry(Action operation)
        {
            _retryPolicyProvider.RegularPolicy.Execute(operation);
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
            
            CloseChannel();
        }
    }
}