using System;

namespace Lykke.Messaging.RabbitMq.Retry
{
    public class RabbitMqRetryPolicyOptions
    {
        public const string RabbitMqRetryPolicyOptionsName = "RabbitMqRetryPolicy";
        public TimeSpan[] InitialConnectionSleepIntervals { get; set; }
        public TimeSpan[] RegularSleepIntervals { get; set; }
    }
}