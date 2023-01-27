using System;

namespace Lykke.Messaging.RabbitMq.Retry
{
    public readonly struct RetryConfiguration
    {
        public RetryConfiguration(params TimeSpan[] retryIntervals)
        {
            RetryIntervals = retryIntervals;
        }

        public TimeSpan[] RetryIntervals { get; }
    }
}