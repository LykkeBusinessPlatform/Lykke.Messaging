using System;

namespace Lykke.Messaging.RabbitMq.Retry
{
    /// <summary>
    /// Actions to be performed to handle a RabbitMQ connection or operation
    /// execution failure
    /// </summary>
    [Flags]
    internal enum FailureActions
    {
        /// <summary>
        /// Ignore failure and just retry
        /// </summary>
        Retry = 1,
        
        /// <summary>
        /// Close channel because the current one is, probably, broken
        /// </summary>
        CloseChannel = 2,
        
        /// <summary>
        /// Throw an exception cause it is not possible to recover from the failure
        /// </summary>
        Throw = 4
    }
}