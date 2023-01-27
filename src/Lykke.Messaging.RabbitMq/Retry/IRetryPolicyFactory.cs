using System;
using Polly.Retry;

namespace Lykke.Messaging.RabbitMq.Retry
{
    internal interface IRetryPolicyFactory
    {
        private const int DefaultRetryCount = 10;

        IRetryPolicy InitialConnectionPolicy(int retryCount = DefaultRetryCount,
            Func<int, TimeSpan> sleepDurationProvider = null);

        IRetryPolicy PublishPolicy(int retryCount = DefaultRetryCount,
            Func<int, TimeSpan> sleepDurationProvider = null);
    }
}