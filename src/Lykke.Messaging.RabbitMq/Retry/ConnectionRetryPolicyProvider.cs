using System;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;

namespace Lykke.Messaging.RabbitMq.Retry
{
    /// <summary>
    /// Provides retry policies for RabbitMQ transport with given configuration
    /// of retry intervals. Retry policies are reused as they are thread safe. 
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

        private static RetryPolicy CreatePolicy(RetryPolicyFactoryMethod factoryMethod,
            TimeSpan[] sleepIntervals)
        {
            var maxRetryCount = sleepIntervals.Length;
            return factoryMethod(maxRetryCount, SleepDurations);
            
            // todo: unit test this
            TimeSpan SleepDurations(int i) => sleepIntervals[i - 1];
        }

        /// <summary>
        /// Creates a retry policy for the operation to instantiate a connection
        /// to RabbitMQ server with default retry intervals
        /// </summary>
        /// <returns></returns>
        public Policy InitialConnectionPolicy
        {
            get
            {
                _initialConnectionPolicy ??= CreatePolicy(_retryPolicyFactory.InitialConnectionPolicy,
                    _policyOptions.InitialConnectionSleepIntervals);
                return _initialConnectionPolicy;
            }
        }

        /// <summary>
        /// Creates a retry policy for the operation to publish a message
        /// to RabbitMQ server with default retry intervals 
        /// </summary>
        /// <returns></returns>
        public Policy RegularPolicy
        {
            get
            {
                _regularPolicy ??= CreatePolicy(_retryPolicyFactory.RegularPolicy,
                    _policyOptions.RegularSleepIntervals);
                return _regularPolicy;
            }
        }
    }
}