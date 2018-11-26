﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Common.Log;
using Lykke.Common.Log;
using Lykke.Messaging.Contract;
using Lykke.Messaging.Transports;
using Lykke.Messaging.Utils;

namespace Lykke.Messaging
{
    internal class ResolvedTransport : IDisposable
    {
        private readonly List<string> m_KnownIds = new List<string>();
        private readonly TransportInfo m_TransportInfo;
        private readonly Action m_ProcessTransportFailure;
        private readonly ILog _log;
        private readonly ITransportFactory m_Factory;
        private readonly List<MessagingSessionWrapper> m_MessagingSessions = new List<MessagingSessionWrapper>();
        private readonly ILogFactory _logFactory;

        internal MessagingSessionWrapper[] Sessions => m_MessagingSessions.ToArray();

        internal ITransport Transport { get; set; }

        public IEnumerable<string> KnownIds => m_KnownIds.ToArray();

        [Obsolete]
        public ResolvedTransport(
            ILog log,
            TransportInfo transportInfo,
            Action processTransportFailure,
            ITransportFactory factory)
        {
            _log = log;
            m_Factory = factory;
            m_ProcessTransportFailure = processTransportFailure;
            m_TransportInfo = transportInfo;
        }

        public ResolvedTransport(
            ILogFactory logFactory,
            TransportInfo transportInfo,
            Action processTransportFailure,
            ITransportFactory factory)
        {
            _logFactory = logFactory ?? throw new ArgumentNullException(nameof(logFactory));
            m_Factory = factory;
            m_ProcessTransportFailure = processTransportFailure;
            m_TransportInfo = transportInfo;
        }

        private void AddId(string transportId)
        {
            if (String.IsNullOrEmpty(transportId))
                throw new ArgumentNullException(nameof(transportId));
            if (!m_KnownIds.Contains(transportId))
                m_KnownIds.Add(transportId);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public IMessagingSession GetSession(Endpoint endpoint, string name, Action onFailure)
        {
            AddId(endpoint.TransportId);

            if (Transport == null)
            {
                Transport = _logFactory == null 
                    ? m_Factory.Create(_log, m_TransportInfo, Helper.CallOnlyOnce(ProcessTransportFailure)) 
                    : m_Factory.Create(m_TransportInfo, Helper.CallOnlyOnce(ProcessTransportFailure));
            }

            var transport = Transport;
            MessagingSessionWrapper messagingSession;

            lock (m_MessagingSessions)
            {
                messagingSession = m_MessagingSessions.FirstOrDefault(g => g.TransportId == endpoint.TransportId && g.Name == name);

                if (messagingSession == null)
                {
                    messagingSession = _logFactory == null
                        ? new MessagingSessionWrapper(_log, endpoint.TransportId, name)
                        : new MessagingSessionWrapper(_logFactory, endpoint.TransportId, name);

                    messagingSession.SetSession(
                        transport.CreateSession(Helper.CallOnlyOnce(() => ProcessSessionFailure(messagingSession)), endpoint.Destination));
                    m_MessagingSessions.Add(messagingSession);
                }
            }

            if (onFailure != null)
                messagingSession.OnFailure += onFailure;
            return messagingSession;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private void ProcessTransportFailure()
        {
            MessagingSessionWrapper[] messagingSessionWrappers;
            lock (m_MessagingSessions)
            {
                messagingSessionWrappers = m_MessagingSessions.ToArray();
            }

            foreach (var session in messagingSessionWrappers)
            {
                ProcessSessionFailure(session);
            }

            m_ProcessTransportFailure();
        }

        private void ProcessSessionFailure(MessagingSessionWrapper messagingSession)
        {
            lock (m_MessagingSessions)
            {
                m_MessagingSessions.Remove(messagingSession);
            }
            messagingSession.ReportFailure();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Dispose()
        {
            if (Transport == null)
                return;

            MessagingSessionWrapper[] sessions;
            lock (m_MessagingSessions)
            {
                sessions = m_MessagingSessions.ToArray();
            }

            foreach (var session in sessions)
            {
                session.Dispose();
            }

            Transport.Dispose();
            Transport = null;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool VerifyDestination(
            Destination destination,
            EndpointUsage usage,
            bool configureIfRequired,
            out string error)
        {
            if (Transport == null)
            {
                Transport = _logFactory == null
                    ? m_Factory.Create(_log, m_TransportInfo, ProcessTransportFailure)
                    : m_Factory.Create(m_TransportInfo, ProcessTransportFailure);
            }

            var transport = Transport;
            return transport.VerifyDestination(
                destination,
                usage,
                configureIfRequired,
                out error);
        }
    }
}
