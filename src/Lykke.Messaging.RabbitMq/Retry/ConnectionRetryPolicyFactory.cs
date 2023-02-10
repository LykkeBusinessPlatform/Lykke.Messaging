using System;
using Lykke.Messaging.RabbitMq.Exceptions;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using RabbitMQ.Client.Exceptions;

namespace Lykke.Messaging.RabbitMq.Retry
{
    /// <summary>
    /// Creates retry policies for RabbitMQ transport
    /// </summary>
    public sealed class ConnectionRetryPolicyFactory : IRetryPolicyFactory
    {
        private readonly ILoggerFactory _loggerFactory;
        
        public ConnectionRetryPolicyFactory(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
        }

        /// <summary>
        /// Creates a retry policy for the operation to instantiate an initial
        /// connection to RabbitMQ server
        /// </summary>
        /// <param name="retryCount">The number of maximum retries</param>
        /// <param name="sleepDurationProvider">The function to calculate the
        /// sleep duration between retries. Defaults to linear backoff with
        /// 2 seconds interval</param>
        /// <returns></returns>
        public RetryPolicy InitialConnectionPolicy(int retryCount,
            Func<int, TimeSpan> sleepDurationProvider)
        {
            var logger = _loggerFactory.CreateLogger("RabbitMqInitialConnectionRetryPolicy");
            
            sleepDurationProvider ??= LinearBackoffSleepDurationProvider;
            
            return Policy
                .Handle<BrokerUnreachableException>()
                .WaitAndRetry(retryCount, sleepDurationProvider,
                    (exception, span, rc, ctx) =>
                    {
                        logger.LogWarning(
                            "The broker is unreachable. Trying to reconnect for the {RetryCount} time in {Period}",
                            rc,
                            span);
                    });
        }

        /// <summary>
        /// Creates a retry policy for the regular operations upon RabbitMQ server
        /// </summary>
        /// <param name="retryCount">The number of maximum retries</param>
        /// <param name="sleepDurationProvider">The function to calculate the
        /// sleep duration between retries. Defaults to exponential backoff.</param>
        /// <returns></returns>
        public RetryPolicy RegularPolicy(int retryCount,
            Func<int, TimeSpan> sleepDurationProvider)
        {
            var logger = _loggerFactory.CreateLogger("RabbitMqRegularRetryPolicy");
            
            sleepDurationProvider ??= ExponentialBackoffSleepDurationProvider;

            return Policy
                .Handle<RecoverableFailureException>()
                .WaitAndRetry(retryCount, sleepDurationProvider,
                    (exception, span, rc, ctx) =>
                    {
                        logger.LogWarning(
                            "Trying to reconnect for the {RetryCount} time in {Period}",
                            rc,
                            span);
                    });
        }

        private static TimeSpan ExponentialBackoffSleepDurationProvider(int retryCount)
        {
            return TimeSpan.FromSeconds(Math.Pow(2, retryCount));
        }
        
        private static TimeSpan LinearBackoffSleepDurationProvider(int retryCount)
        {
            return TimeSpan.FromSeconds(2);
        }
    }
}