using System;

namespace Lykke.Messaging.RabbitMq.Retry
{
    public class RabbitMqRetryPolicyOptions
    {
        public TimeSpan[] InitialConnectionRetryIntervals { get; set; }
        public TimeSpan[] RegularRetryIntervals { get; set; }
    }
}