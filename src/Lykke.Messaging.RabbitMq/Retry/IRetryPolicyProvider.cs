using Polly;

namespace Lykke.Messaging.RabbitMq.Retry
{
    public interface IRetryPolicyProvider
    {
        Policy InitialConnectionPolicy { get; }
        Policy RegularPolicy { get; }
    }
}