using Polly.Retry;

namespace Lykke.Messaging.RabbitMq.Retry
{
    public interface IRetryPolicyProvider
    {
        RetryPolicy InitialConnectionPolicy { get; }
        RetryPolicy RegularPolicy { get; }
    }
}