using Lykke.Messaging.RabbitMq.Retry;
using Polly;

namespace Lykke.Messaging.RabbitMq.Tests.Fakes
{
    public class NullRabbitMqRetryPolicyProvider : IRetryPolicyProvider
    {
        public Policy InitialConnectionPolicy { get; } = Policy.NoOp();
        public Policy RegularPolicy { get; } = Policy.NoOp();
        
        public static readonly NullRabbitMqRetryPolicyProvider Instance = new NullRabbitMqRetryPolicyProvider();

        private NullRabbitMqRetryPolicyProvider()
        {
            
        }
    }
}