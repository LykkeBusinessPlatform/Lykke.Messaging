using System;
using Polly.Retry;

namespace Lykke.Messaging.RabbitMq.Retry
{
    /// <summary>
    /// Provides retry policies for RabbitMQ transport with given configuration
    /// of retry intervals. For default retry intervals retry policies are reused. 
    /// </summary>
    public sealed class ConnectionRetryPolicyProvider : IRetryPolicyProvider
    {
        private readonly IRetryPolicyFactory _retryPolicyFactory;

        public ConnectionRetryPolicyProvider(IRetryPolicyFactory retryPolicyFactory,
            RetryConfiguration initialConnectionConfiguration,
            RetryConfiguration regularConfiguration)
        {
            _retryPolicyFactory = retryPolicyFactory;

            InitialConnectionPolicy =
                CreatePolicy(_retryPolicyFactory.InitialConnectionPolicy, initialConnectionConfiguration);
            RegularPolicy = 
                CreatePolicy(_retryPolicyFactory.RegularPolicy, regularConfiguration);
        }

        private static RetryPolicy CreatePolicy(Func<int, Func<int, TimeSpan>, RetryPolicy> factoryMethod,
            RetryConfiguration configuration)
        {
            var (retryCount, sleepDurationProvider) = configuration.ToPolicyParams();
            return factoryMethod(retryCount, sleepDurationProvider);
        }

        /// <summary>
        /// Creates a retry policy for the operation to instantiate a connection
        /// to RabbitMQ server with default retry intervals
        /// </summary>
        /// <returns></returns>
        public RetryPolicy InitialConnectionPolicy { get; }

        /// <summary>
        /// Creates a retry policy for the operation to publish a message
        /// to RabbitMQ server with default retry intervals 
        /// </summary>
        /// <returns></returns>
        public RetryPolicy RegularPolicy { get; }
    }
}