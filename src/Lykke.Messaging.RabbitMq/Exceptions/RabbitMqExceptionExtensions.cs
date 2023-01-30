using System;
using Lykke.Messaging.RabbitMq.Retry;
using RabbitMQ.Client.Exceptions;

namespace Lykke.Messaging.RabbitMq.Exceptions
{
    internal static class RabbitMqExceptionExtensions
    {
        /// <summary>
        /// Returns an actions to be performed on RabbitMQ connection/channel
        /// failure depending on the exception type and exception details.
        /// The goal is to try restore connection/channel. 
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        public static FailureActions DecideOnFailureActions(this Exception e)
        {
            return e switch
            {
                OperationInterruptedException operationInterruptedException => 
                    MapToFailureActions(operationInterruptedException.ShutdownReason?.ReplyCode),
                NotSupportedException notSupportedException => 
                    MapToFailureActions(notSupportedException),
                BrokerUnreachableException brokerUnreachableException => 
                    MapToFailureActions(brokerUnreachableException),
                _ => FailureActions.Throw
            };
        }

        private static FailureActions MapToFailureActions(ushort? operationInterruptedExceptionReason) =>
            operationInterruptedExceptionReason switch
            {
                // connection closed
                320 => FailureActions.Retry,
                // access refused
                403 => FailureActions.CloseChannel | FailureActions.Throw,
                // not found
                404 => FailureActions.CloseChannel | FailureActions.Throw,
                // resource locked
                405 => FailureActions.CloseChannel | FailureActions.Throw,
                // precondition failed
                406 => FailureActions.CloseChannel | FailureActions.Throw,
                // internal errors
                541 => FailureActions.CloseChannel | FailureActions.Retry,
                // default
                _ => FailureActions.Throw
            };

        private static FailureActions MapToFailureActions(NotSupportedException e)
        {
            return e.Message.Contains("pipelining")
                ?
                // an attempt to use channel by multiple threads simultaneously
                FailureActions.CloseChannel | FailureActions.Retry
                : FailureActions.Throw;
        }

        private static FailureActions MapToFailureActions(BrokerUnreachableException e)
        {
            return FailureActions.Retry;
        }
    }
}