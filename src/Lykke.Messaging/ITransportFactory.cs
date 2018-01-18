using System;
using Common.Log;
using Inceptum.Messaging;
using Inceptum.Messaging.Transports;

namespace Lykke.Messaging
{
    public interface ITransportFactory
    {
        string Name { get; }
        ITransport Create(ILog log, TransportInfo transportInfo, Action onFailure);
    }
}