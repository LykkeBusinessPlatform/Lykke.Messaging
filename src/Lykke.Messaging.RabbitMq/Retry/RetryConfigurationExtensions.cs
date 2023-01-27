using System;

namespace Lykke.Messaging.RabbitMq.Retry
{
    public static class RetryConfigurationExtensions
    {
        public static (int retryCount, Func<int, TimeSpan> sleepDurationProvider) ToPolicyParams(
            this RetryConfiguration cfg)
        {
            return (
                cfg.RetryIntervals.Length,
                retryCount => cfg.RetryIntervals[retryCount - 1]);
        }
    }
}