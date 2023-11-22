using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Lykke.Messaging.Contract;
using Lykke.Messaging.RabbitMq.Retry;
using Lykke.Messaging.Transports;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.PlatformAbstractions;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using RabbitMQ.Client.Framing.Impl;

namespace Lykke.Messaging.RabbitMq
{
    internal class RabbitMqTransport : ITransport
    {
        private static readonly Random m_Random = new Random((int)DateTime.UtcNow.Ticks & 0x0000FFFF);
        private readonly TimeSpan m_NetworkRecoveryInterval;
        private readonly ConnectionFactory[] m_Factories;
        // todo: probably, replace with another collection
        private readonly List<RabbitMqSession> m_Sessions = new List<RabbitMqSession>();
        private readonly ManualResetEvent m_IsDisposed = new ManualResetEvent(false);
        private readonly string _appName = PlatformServices.Default.Application.ApplicationName;
        private readonly string _appVersion = PlatformServices.Default.Application.ApplicationVersion;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<RabbitMqTransport> _logger;
        private readonly IRetryPolicyProvider _retryPolicyProvider;
        
        private AutorecoveringConnection _connection;

        internal long SessionsCount => m_Sessions.Count;

        public RabbitMqTransport(
            ILoggerFactory loggerFactory,
            IRetryPolicyProvider retryPolicyProvider,
            string broker,
            string username,
            string password,
            TimeSpan networkRecoveryInterval)
            : this(loggerFactory, retryPolicyProvider, new[] { broker }, username, password, networkRecoveryInterval)
        {
        }

        public RabbitMqTransport(
            ILoggerFactory loggerFactory,
            IRetryPolicyProvider retryPolicyProvider,
            string[] brokers,
            string username,
            string password,
            TimeSpan networkRecoveryInterval,
            bool shuffleBrokersOnSessionCreate = true)
        {
            if (brokers == null)
                throw new ArgumentNullException(nameof(brokers));
            if (brokers.Length == 0)
                throw new ArgumentException("brokers list is empty", nameof(brokers));

            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            _retryPolicyProvider = retryPolicyProvider ?? throw new ArgumentNullException(nameof(retryPolicyProvider));
            _logger = loggerFactory.CreateLogger<RabbitMqTransport>();

            m_NetworkRecoveryInterval = networkRecoveryInterval;

            var factories = brokers.Select(brokerName =>
            {
                var f = new ConnectionFactory
                {
                    UserName = username,
                    Password = password,
                    AutomaticRecoveryEnabled = true,
                    NetworkRecoveryInterval = m_NetworkRecoveryInterval,
                    TopologyRecoveryEnabled = true
                };

                if (Uri.TryCreate(brokerName, UriKind.Absolute, out var uri))
                    f.Uri = uri;
                else
                    f.HostName = brokerName;
                
                return f;
            });

            m_Factories = shuffleBrokersOnSessionCreate && brokers.Length > 1
                ? factories.OrderBy(x => m_Random.Next()).ToArray()
                : factories.ToArray();
        }

        private AutorecoveringConnection CreateConnection(string displayName)
        {
            Exception exception = null;

            for (int i = 0; i < m_Factories.Length; i++)
            {
                try
                {
                    var connection = _retryPolicyProvider.InitialConnectionPolicy.Execute(() =>
                        m_Factories[i].CreateConnection(displayName) as AutorecoveringConnection);
                    
                    _logger.LogInformation(
                        "{Method}: Created rmq connection to {HostName} {ConnectionName}.",
                        nameof(CreateConnection),
                        m_Factories[i].Endpoint.HostName,
                        displayName);
                    
                    connection.ConnectionShutdown += (c, reason) =>
                    {
                        if ((reason.Initiator != ShutdownInitiator.Application || reason.ReplyCode != 200))
                        {
                            _logger.LogWarning(
                                "ConnectionShutdown: Rmq session to {HostName} is broken. Reason: {Reason}. Auto-recovery is in progress.",
                                _connection.Endpoint.HostName,
                                reason);
                        }
                    };

                    return connection;
                }
                catch (Exception e)
                {
                    _logger.LogCritical(e,
                        i + 1 != m_Factories.Length
                            ? "{Method}: Failed to create rmq connection to {HostName} (will try other known hosts) {ConnectionName}"
                            : "{Method}: Failed to create rmq connection to {HostName} {ConnectionName}",
                        nameof(CreateConnection),
                        m_Factories[i].Endpoint.HostName,
                        displayName);
                    exception = e;
                }
            }
            
            throw new TransportException("Failed to create rmq connection", exception);
        }

        public void Dispose()
        {
            m_IsDisposed.Set();
            RabbitMqSession[] sessions;
            lock (m_Sessions)
            {
                sessions = m_Sessions.ToArray();
            }
            foreach (var session in sessions)
            {
                session.Dispose();
            }
            
            _connection?.Close();
        }

        public IMessagingSession CreateSession(bool confirmedSending = false, string displayName = null)
        {
            if(m_IsDisposed.WaitOne(0))
                throw new ObjectDisposedException("Transport is disposed");

            _connection ??= CreateConnection(displayName);

            var session = new RabbitMqSession(_loggerFactory, _connection, _retryPolicyProvider, confirmedSending);
            
            lock (m_Sessions)
            {
                m_Sessions.Add(session);
            }

            return session;
        }

        public bool VerifyDestination(
            Destination destination,
            EndpointUsage usage,
            bool configureIfRequired,
            out string error)
        {
            try
            {
                var publish = PublicationAddress.Parse(destination.Publish) ??
                              new PublicationAddress("topic", destination.Publish, "");
                _connection ??= CreateConnection(displayName: $"{_appName} {_appVersion}");
                using var channel = _connection.CreateModel();
                if (publish.ExchangeName == "" && publish.ExchangeType.ToLower() == "direct")
                {
                    //default exchange should not be verified since it always exists and publication to it is always possible
                }
                else
                {
                    if (configureIfRequired)
                        channel.ExchangeDeclare(publish.ExchangeName, publish.ExchangeType, true);
                    else
                        channel.ExchangeDeclarePassive(publish.ExchangeName);
                }

                //temporary queue should not be verified since it is not supported by rmq client
                if((usage & EndpointUsage.Subscribe) == EndpointUsage.Subscribe && !destination.Subscribe.ToLower().StartsWith("amq."))
                {
                    if (configureIfRequired)
                        channel.QueueDeclare(destination.Subscribe, true, false, false, null);
                    else
                        channel.QueueDeclarePassive(destination.Subscribe);

                    channel.BasicQos(0, 300, false);

                    if (configureIfRequired)
                        channel.QueueBind(destination.Subscribe, publish.ExchangeName, publish.RoutingKey == "" ? "#" : publish.RoutingKey);
                }
            }
            catch (Exception e)
            {
                if (!e.GetType().Namespace.StartsWith("RabbitMQ") || e.GetType().Assembly != typeof (OperationInterruptedException).Assembly)
                    throw;
                error = e.Message;
                return false;
            }
            error = null;
            return true;
        }
    }
}
