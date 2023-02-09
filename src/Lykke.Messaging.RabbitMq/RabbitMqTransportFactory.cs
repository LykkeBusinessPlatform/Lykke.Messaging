using System;
using System.Collections.Concurrent;
using System.Linq;
using Lykke.Messaging.RabbitMq.Retry;
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
        private readonly TimeSpan m_AutomaticRecoveryInterval;
        private readonly ConcurrentDictionary<TransportInfo, RabbitMqTransport> _transports = new ConcurrentDictionary<TransportInfo, RabbitMqTransport>();
        private readonly IRetryPolicyProvider _retryPolicyProvider;

        public string Name => "RabbitMq";

        /// <summary>
        /// Creates new instance of <see cref="RabbitMqTransportFactory"/>
        /// </summary>
        /// <param name="loggerFactory"></param>
        /// <param name="retryPolicyProvider"></param>
        /// <param name="shuffleBrokers">True to shuffle brokers, False to iterate brokers in default order</param>
        /// <param name="automaticRecoveryInterval">Interval for automatic recover if set to null automaitc recovery is disabled, 
        /// if set to some value automatic recovery is enabled and NetworkRecoveryInterval of RabbitMQ client is set provided valie
        /// </param>
        public RabbitMqTransportFactory(ILoggerFactory loggerFactory,
            TimeSpan automaticRecoveryInterval,
            IRetryPolicyProvider retryPolicyProvider,
            bool shuffleBrokers = true)
        {
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            m_AutomaticRecoveryInterval = automaticRecoveryInterval;
            _retryPolicyProvider = retryPolicyProvider ?? throw new ArgumentNullException(
                "Probably, you haven't called AddRabbitMqMessaging on service collection to initialize retry policy provider",
                nameof(retryPolicyProvider));
            m_ShuffleBrokers = shuffleBrokers;
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
                    _retryPolicyProvider,
                    brokers,
                    ti.Login,
                    ti.Password,
                    m_AutomaticRecoveryInterval,
                    m_ShuffleBrokers
                );
            });
        }
    }
}
