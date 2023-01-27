using System;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using RabbitMQ.Client.Exceptions;

namespace Lykke.Messaging.RabbitMq
{
    /// <summary>
    /// Creates retry policies for RabbitMQ transport
    /// </summary>
    class ConnectionRetryPolicyFactory
    {
        private readonly ILoggerFactory _loggerFactory;
        
        private const int RetryCount = 10;

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
        public IRetryPolicy InitialConnectionPolicy(int retryCount = RetryCount,
            Func<int, TimeSpan> sleepDurationProvider = null)
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
        /// Creates a retry policy for the operation to publish a message to
        /// RabbitMQ server
        /// </summary>
        /// <param name="retryCount">The number of maximum retries</param>
        /// <param name="sleepDurationProvider">The function to calculate the
        /// sleep duration between retries. Defaults to exponential backoff.</param>
        /// <returns></returns>
        public IRetryPolicy PublishPolicy(int retryCount = RetryCount,
            Func<int, TimeSpan> sleepDurationProvider = null)
        {
            var logger = _loggerFactory.CreateLogger("RabbitMqPublishRetryPolicy");
            
            sleepDurationProvider ??= ExponentialBackoffSleepDurationProvider;

            return Policy
                .Handle<OperationInterruptedException>()
                .WaitAndRetry(retryCount, sleepDurationProvider,
                    (exception, span, rc, ctx) =>
                    {
                        var operationInterruptedException = exception as OperationInterruptedException;

                        if (operationInterruptedException == null)
                        {
                            logger.LogCritical(exception, "Unexpected exception type");
                            return;
                        }
                        
                        logger.LogWarning(
                            "The operation was interrupted with reason {ReasonCode}:{ReasonText}. " +
                            "Trying to reconnect for the {RetryCount} time in {Period}",
                            operationInterruptedException.ShutdownReason.ReplyCode,
                            operationInterruptedException.ShutdownReason.ReplyText,
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