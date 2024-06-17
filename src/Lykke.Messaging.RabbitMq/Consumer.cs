using System;
using RabbitMQ.Client;
using Common.Log;
using Lykke.Common.Log;

namespace Lykke.Messaging.RabbitMq
{
    internal class Consumer : DefaultBasicConsumer, IDisposable
    {
        private readonly ILog _log;
        private readonly Action<IBasicProperties, ReadOnlyMemory<byte>, Action<bool>> m_Callback;

        [Obsolete]
        public Consumer(
            ILog log,
            IModel model,
            Action<IBasicProperties, ReadOnlyMemory<byte>, Action<bool>> callback)
            : base(model)
        {
            _log = log;
            m_Callback = callback ?? throw new ArgumentNullException("callback");
        }

        public Consumer(
            ILogFactory logFactory,
            IModel model,
            Action<IBasicProperties, ReadOnlyMemory<byte>, Action<bool>> callback)
            
            : base(model)
        {
            if (logFactory == null)
            {
                throw new ArgumentNullException(nameof(logFactory));
            }

            _log = logFactory.CreateLog(this);
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
                _log.WriteError(nameof(Consumer), nameof(HandleBasicDeliver), e);
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
                        _log.WriteError(nameof(Consumer), nameof(Dispose), e);
                    }
                }
            }
        }
    }
}