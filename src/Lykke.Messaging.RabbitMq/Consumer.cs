using System;
using RabbitMQ.Client;
using Microsoft.Extensions.Logging;

namespace Lykke.Messaging.RabbitMq
{
    public class Consumer : DefaultBasicConsumer, IDisposable
    {
        private readonly Action<IBasicProperties, ReadOnlyMemory<byte>, Action<bool>> _callback;
        private readonly ILogger<Consumer> _logger;

        public Consumer(
            IModel model,
            Action<IBasicProperties, ReadOnlyMemory<byte>, Action<bool>> callback,
            ILoggerFactory loggerFactory)
            : base(model)
        {
            _callback = callback ?? throw new ArgumentNullException(nameof(callback));
            _logger = loggerFactory?.CreateLogger<Consumer>() ?? throw new ArgumentNullException(nameof(loggerFactory));
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
                _callback(properties, new ReadOnlyMemory<byte>(bodyCopy), ack =>
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
                Model.BasicNack(deliveryTag, false, true);
            }
        }

        # region Dispose pattern
        
        private bool _disposed = false;

        protected void Dispose(bool disposing)
        {
            if (_disposed) return;
            
            if (disposing && IsRunning)
            {
                try
                {
                    foreach (var consumerTag in ConsumerTags)
                    {
                        Model.BasicCancel(consumerTag);
                    }
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "{Method}: error", nameof(Dispose));
                }
            }

            _disposed = true;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        
        # endregion
    }
}