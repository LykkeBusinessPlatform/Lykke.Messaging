using System;
using Polly.Retry;

namespace Lykke.Messaging.RabbitMq.Retry
{
    public delegate RetryPolicy RetryPolicyFactoryMethod(int retryCount, Func<int, TimeSpan> sleepDurationProvider);
    
    public interface IRetryPolicyFactory
    {
        private const int DefaultRetryCount = 10;
        
        RetryPolicy InitialConnectionPolicy(int retryCount = DefaultRetryCount,
            Func<int, TimeSpan> sleepDurationProvider = null);
        
        RetryPolicy RegularPolicy(int retryCount = DefaultRetryCount,
            Func<int, TimeSpan> sleepDurationProvider = null);
    }
}