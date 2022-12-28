using System;
using Lykke.Messaging.Contract;

namespace Lykke.Messaging.Transports
{
    public interface ITransport : IDisposable
    {
        IMessagingSession CreateSession(Action onFailure);
        
        bool VerifyDestination(
            Destination destination,
            EndpointUsage usage,
            bool configureIfRequired,
            out string error);
    }
}