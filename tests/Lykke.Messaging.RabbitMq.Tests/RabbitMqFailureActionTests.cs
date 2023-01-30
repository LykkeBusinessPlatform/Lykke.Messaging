using System;
using Lykke.Messaging.RabbitMq.Exceptions;
using Lykke.Messaging.RabbitMq.Retry;
using NUnit.Framework;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace Lykke.Messaging.RabbitMq.Tests
{
    [TestFixture]
    public class RabbitMqFailureActionTests
    {
        [TestCase((ushort)320)]
        [TestCase((ushort)541)]
        public void When_RetryIsPossible_DecideOnFailureActions_Returns_Retry(ushort replyCode)
        {
            var ex = new OperationInterruptedException(new
                ShutdownEventArgs(ShutdownInitiator.Application,
                    replyCode,
                    "reply-text"));

            var failureActions = ex.DecideOnFailureActions();
            
            Assert.IsTrue(failureActions.HasFlag(FailureActions.Retry));
        }
        
        [TestCase((ushort)403)]
        [TestCase((ushort)404)]
        [TestCase((ushort)405)]
        [TestCase((ushort)406)]
        [TestCase((ushort)541)]
        public void When_ChannelIsBroken_DecideOnFailureActions_Returns_CloseChannel(ushort replyCode)
        {
            var ex = new OperationInterruptedException(new
                ShutdownEventArgs(ShutdownInitiator.Application,
                    replyCode,
                    "reply-text"));

            var failureActions = ex.DecideOnFailureActions();

            Assert.IsTrue(failureActions.HasFlag(FailureActions.CloseChannel));
        }
        
        [Test]
        public void When_Attempt_Made_To_Use_Channel_By_Multiple_Threads_Simultaneously_DecideOnFailureActions_Returns_CloseChannel_And_Retry()
        {
            var ex = new NotSupportedException("pipelining");

            var failureActions = ex.DecideOnFailureActions();

            Assert.IsTrue(failureActions.HasFlag(FailureActions.CloseChannel));
            Assert.IsTrue(failureActions.HasFlag(FailureActions.Retry));
        }
        
        
    }
}