using System;
using System.Collections.Concurrent;
using System.Linq;
using Lykke.Messaging.Transports;
using Microsoft.Extensions.Logging;

namespace Lykke.Messaging.RabbitMq
{
    /// <summary>
    /// Implementation of <see cref="ITransportFactory"/> interface for RabbitMQ
    /// </summary>
    public class RabbitMqTransportFactory : ITransportFactory
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly bool m_ShuffleBrokers;
        private readonly TimeSpan? m_AutomaticRecoveryInterval;
        private readonly ConcurrentDictionary<TransportInfo, RabbitMqTransport> _transports = new ConcurrentDictionary<TransportInfo, RabbitMqTransport>();

        public string Name => "RabbitMq";

        /// <summary>
        /// Creates new instance of <see cref="RabbitMqTransportFactory"/>
        /// </summary>
        /// <param name="loggerFactory"></param>
        /// <param name="shuffleBrokers">True to shuffle brokers, False to iterate brokers in default order</param>
        /// <param name="automaticRecoveryInterval">Interval for automatic recover if set to null automaitc recovery is disabled, 
        /// if set to some value automatic recovery is enabled and NetworkRecoveryInterval of RabbitMQ client is set provided valie
        /// </param>
        public RabbitMqTransportFactory(ILoggerFactory loggerFactory, bool shuffleBrokers = true, TimeSpan? automaticRecoveryInterval = null)
        {
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            m_ShuffleBrokers = shuffleBrokers;
            m_AutomaticRecoveryInterval = automaticRecoveryInterval;
        }

        public ITransport Create(TransportInfo transportInfo, Action onFailure)
        {
            return _transports.GetOrAdd(transportInfo, ti =>
            {
                var brokers = ti.Broker
                    .Split(',')
                    .Select(b => b.Trim())
                    .ToArray();

                return new RabbitMqTransport(
                    _loggerFactory,
                    brokers,
                    ti.Login,
                    ti.Password,
                    m_ShuffleBrokers,
                    m_AutomaticRecoveryInterval
                );
            });
        }
    }
}
