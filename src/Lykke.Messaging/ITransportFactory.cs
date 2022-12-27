using System;
using Lykke.Messaging.Transports;

namespace Lykke.Messaging
{
    public interface ITransportFactory
    {
        string Name { get; }

        ITransport Create(TransportInfo transportInfo, Action onFailure);
    }
}