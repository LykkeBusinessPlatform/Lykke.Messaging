using System;
using System.Collections.Generic;
using Lykke.Messaging.Transports;

namespace Lykke.Messaging.InMemory
{
    internal class InMemoryTransportFactory : ITransportFactory
    {
        private readonly Dictionary<TransportInfo, InMemoryTransport> m_Transports = new Dictionary<TransportInfo, InMemoryTransport>();

        public string Name
        {
            get { return "InMemory"; }
        }

        public ITransport Create(TransportInfo transportInfo, Action onFailure)
        {
            lock (m_Transports)
            {
                if (m_Transports.TryGetValue(transportInfo, out var transport))
                {
                    return transport;
                }

                transport = new InMemoryTransport();
                m_Transports.Add(transportInfo, transport);
                return transport;
            }
        }
    }
}