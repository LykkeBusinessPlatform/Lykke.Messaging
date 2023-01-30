using System;

namespace Lykke.Messaging.RabbitMq.Exceptions
{
    public class RecoverableFailureException : Exception
    {
        private const string MESSAGE = "Failed to execute operation on RabbitMQ. This is a recoverable failure.";

        public RecoverableFailureException() : base(MESSAGE)
        {
        }

        public RecoverableFailureException(Exception innerException) : base(MESSAGE, innerException)
        {
        }
    }
}