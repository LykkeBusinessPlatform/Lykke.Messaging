using System;
using Microsoft.Extensions.Options;
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
        private readonly RabbitMqRetryPolicyOptions _policyOptions;
        private RetryPolicy _initialConnectionPolicy;
        private RetryPolicy _regularPolicy;

        public ConnectionRetryPolicyProvider(IRetryPolicyFactory retryPolicyFactory,
            IOptions<RabbitMqRetryPolicyOptions> options)
        {
            _retryPolicyFactory = retryPolicyFactory;
            _policyOptions = options.Value;
        }

        private static RetryPolicy CreatePolicy(Func<int, Func<int, TimeSpan>, RetryPolicy> factoryMethod,
            TimeSpan[] retryIntervals)
        {
            return factoryMethod(retryIntervals.Length, i => retryIntervals[i - 1]);
        }

        /// <summary>
        /// Creates a retry policy for the operation to instantiate a connection
        /// to RabbitMQ server with default retry intervals
        /// </summary>
        /// <returns></returns>
        public RetryPolicy InitialConnectionPolicy
        {
            get
            {
                _initialConnectionPolicy ??= CreatePolicy(_retryPolicyFactory.InitialConnectionPolicy,
                    _policyOptions.InitialConnectionRetryIntervals);
                return _initialConnectionPolicy;
            }
        }

        /// <summary>
        /// Creates a retry policy for the operation to publish a message
        /// to RabbitMQ server with default retry intervals 
        /// </summary>
        /// <returns></returns>
        public RetryPolicy RegularPolicy
        {
            get
            {
                _regularPolicy ??= CreatePolicy(_retryPolicyFactory.RegularPolicy,
                    _policyOptions.RegularRetryIntervals);
                return _regularPolicy;
            }
        }
    }
}