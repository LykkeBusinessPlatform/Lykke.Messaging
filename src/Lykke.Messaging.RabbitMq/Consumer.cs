﻿using System;
using RabbitMQ.Client;
using Microsoft.Extensions.Logging;

namespace Lykke.Messaging.RabbitMq
{
    internal class Consumer : DefaultBasicConsumer, IDisposable
    {
        private readonly Action<IBasicProperties, ReadOnlyMemory<byte>, Action<bool>> m_Callback;
        private readonly ILogger<Consumer> _logger;

        public Consumer(
            ILoggerFactory loggerFactory,
            IModel model,
            Action<IBasicProperties, ReadOnlyMemory<byte>, Action<bool>> callback)
            
            : base(model)
        {
            if (loggerFactory == null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }

            _logger = loggerFactory.CreateLogger<Consumer>();
            m_Callback = callback ?? throw new ArgumentNullException(nameof(callback));
        }

        public override void HandleBasicDeliver(
            string consumerTag,
            ulong deliveryTag,
            bool redelivered,
            string exchange,
            string routingKey,
            IBasicProperties properties,
            ReadOnlyMemory<byte> body)
        {
            // make a copy of the body, as it can be released any time
            // https://www.rabbitmq.com/dotnet-api-guide.html#consuming-async
            var bodyCopy = new byte[body.Length];
            Buffer.BlockCopy(body.ToArray(), 0, bodyCopy, 0, body.Length);
            
            try
            {
                m_Callback(properties, new ReadOnlyMemory<byte>(bodyCopy), ack =>
                {
                    if (ack)
                        Model.BasicAck(deliveryTag, false);
                    else
                        Model.BasicNack(deliveryTag, false, true);
                });
            }
            catch (Exception e)
            {
                _logger.LogError(e, "{Method}: error", nameof(HandleBasicDeliver));
            }
        }

        public void Dispose()
        {
            lock (Model)
            {
                if (Model.IsOpen)
                {
                    try
                    {
                        foreach (var tag in ConsumerTags)
                        {
                            Model.BasicCancel(tag);
                        }
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e, "{Method}: error", nameof(Dispose));
                    }
                }
            }
        }
    }
}