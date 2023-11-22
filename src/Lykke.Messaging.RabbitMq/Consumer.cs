using System;
using RabbitMQ.Client;
using Microsoft.Extensions.Logging;

namespace Lykke.Messaging.RabbitMq
{
    public class Consumer : DefaultBasicConsumer, IDisposable
    {
        private readonly Action<IBasicProperties, byte[], Action<bool>> _callback;
        private readonly ILogger<Consumer> _logger;

        public Consumer(
            IModel model,
            Action<IBasicProperties, byte[], Action<bool>> callback,
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
            byte[] body)
        {
            try
            {
                _callback(properties, body, ack =>
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
                    Model.BasicCancel(ConsumerTag);
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