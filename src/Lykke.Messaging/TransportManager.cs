using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Lykke.Messaging.Contract;
using Lykke.Messaging.InMemory;
using Lykke.Messaging.Transports;
using Microsoft.Extensions.Logging;

namespace Lykke.Messaging
{
    internal class TransportManager : ITransportManager
    {
        private readonly ConcurrentDictionary<TransportInfo, ResolvedTransport> m_Transports = new ConcurrentDictionary<TransportInfo, ResolvedTransport>();
        private readonly ITransportInfoResolver m_TransportInfoResolver;
        private readonly ManualResetEvent m_IsDisposed = new ManualResetEvent(false);
        private readonly ITransportFactory[] m_TransportFactories;
        private readonly ILoggerFactory _loggerFactory;

        public TransportManager(ILoggerFactory loggerFactory, ITransportInfoResolver transportInfoResolver, params ITransportFactory[] transportFactories)
        {
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            m_TransportFactories = transportFactories.Concat(new[] { new InMemoryTransportFactory() }).ToArray();
            m_TransportInfoResolver = transportInfoResolver ?? throw new ArgumentNullException(nameof(transportInfoResolver));
        }

        public ITransportInfoResolver TransportInfoResolver => m_TransportInfoResolver;

        #region IDisposable Members

        public void Dispose()
        {
            m_IsDisposed.Set();
            foreach (var transport in m_Transports.Values.Distinct())
            {
                transport.Dispose();
            }
            m_Transports.Clear();
        }

        #endregion

        public event TransportEventHandler TransportEvents;

        public IMessagingSession GetMessagingSession(Endpoint endpoint, string name, Action onFailure = null)
        {
            ResolvedTransport transport = ResolveTransport(endpoint.TransportId);

            try
            {
                return transport.GetSession(endpoint, name, onFailure);
            }
            catch (Exception e)
            {
                throw new TransportException($"Failed to create processing group {name} on transport {endpoint.TransportId}", e);
            }
        }

        internal ResolvedTransport ResolveTransport(string transportId)
        {
            if (m_IsDisposed.WaitOne(0))
                throw new ObjectDisposedException($"Can not create transport {transportId}. TransportManager instance is disposed");

            var transportInfo = m_TransportInfoResolver.Resolve(transportId);
            if (transportInfo == null)
                throw new ApplicationException($"Transport '{transportId}' is not resolvable");

            var factory = m_TransportFactories.FirstOrDefault(f => f.Name == transportInfo.Messaging);
            if (factory == null)
                throw new ApplicationException($"Can not create transport '{transportId}', {transportInfo.Messaging} messaging is not supported");

            var transport = m_Transports.GetOrAdd(
                transportInfo,
                new ResolvedTransport(_loggerFactory, transportInfo, () => ProcessTransportFailure(transportInfo), factory));

            return transport;
        }

        internal virtual void ProcessTransportFailure(TransportInfo transportInfo)
        {
            if (!m_Transports.TryRemove(transportInfo, out var transport))
                return;

            var handler = TransportEvents;
            if (handler == null)
                return;

            lock (transport)
            {
                foreach (var transportId in transport.KnownIds)
                {
                    handler(transportId, Contract.TransportEvents.Failure);
                }
            }
        }

        public bool VerifyDestination(
            string transportId,
            Destination destination,
            EndpointUsage usage,
            bool configureIfRequired,
            out string error)
        {
            ResolvedTransport transport = ResolveTransport(transportId);

            try
            {
                return transport.VerifyDestination(
                    destination,
                    usage,
                    configureIfRequired,
                    out error);
            }
            catch (Exception e)
            {
                throw new TransportException($"Destination {destination} is not properly configured on transport {transportId}", e);
            }
        }

        public IDictionary<Endpoint, string> VerifyDestinations(
            string transportId,
            IEnumerable<Endpoint> endpoints,
            EndpointUsage usage,
            bool configureIfRequired)
        {
            var result = new ConcurrentDictionary<Endpoint, string>();

            ResolvedTransport transport = ResolveTransport(transportId);
            
            foreach (var endpoint in endpoints)
            {
                try
                {
                    bool verificationResult = transport.VerifyDestination(
                        endpoint.Destination,
                        usage,
                        configureIfRequired,
                        out var dstError);
                    
                    result.TryAdd(endpoint, verificationResult ? null : dstError);
                }
                catch (Exception e)
                {
                    throw new TransportException(
                        $"Destination [{endpoint.Destination}] is not properly configured on transport [{transportId}]",
                        e);
                }
            }

            return result;
        }
    }
}
