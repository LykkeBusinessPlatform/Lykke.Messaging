﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Lykke.Messaging.Contract;
using Lykke.Messaging.Transports;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.PlatformAbstractions;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace Lykke.Messaging.RabbitMq
{
    internal class RabbitMqTransport : ITransport
    {
        private static readonly Random m_Random = new Random((int)DateTime.UtcNow.Ticks & 0x0000FFFF);

        private readonly TimeSpan? m_NetworkRecoveryInterval;
        private readonly ConnectionFactory[] m_Factories;
        private readonly List<RabbitMqSession> m_Sessions = new List<RabbitMqSession>();
        private readonly ManualResetEvent m_IsDisposed = new ManualResetEvent(false);
        private readonly string _appName = PlatformServices.Default.Application.ApplicationName;
        private readonly string _appVersion = PlatformServices.Default.Application.ApplicationVersion;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<RabbitMqTransport> _logger;

        internal long SessionsCount => m_Sessions.Count;

        public RabbitMqTransport(
            ILoggerFactory loggerFactory,
            string broker,
            string username,
            string password)
            : this(loggerFactory, new[] { broker }, username, password)
        {
        }

        public RabbitMqTransport(
            ILoggerFactory loggerFactory,
            string[] brokers,
            string username,
            string password,
            bool shuffleBrokersOnSessionCreate = true,
            TimeSpan? networkRecoveryInterval = null)
        {
            if (brokers == null)
                throw new ArgumentNullException(nameof(brokers));
            if (brokers.Length == 0)
                throw new ArgumentException("brokers list is empty", nameof(brokers));

            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            _logger = loggerFactory.CreateLogger<RabbitMqTransport>();

            m_NetworkRecoveryInterval = networkRecoveryInterval;

            var factories = brokers.Select(brokerName =>
            {
                var f = new ConnectionFactory
                {
                    UserName = username,
                    Password = password,
                    AutomaticRecoveryEnabled = m_NetworkRecoveryInterval.HasValue
                };

                if (m_NetworkRecoveryInterval.HasValue)
                    f.NetworkRecoveryInterval = m_NetworkRecoveryInterval.Value; //it's default value

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

        private IConnection CreateConnection(bool logConnection, Destination destination)
        {
            Exception exception = null;

            for (int i = 0; i < m_Factories.Length; i++)
            {
                try
                {
                    var connection = m_Factories[i].CreateConnection($"{_appName} {_appVersion} {destination}");
                    if (logConnection)
                    {
                        _logger.LogInformation(
                            "{Method}: Created rmq connection to {HostName} {Destination}.",
                            nameof(CreateConnection),
                            m_Factories[i].Endpoint.HostName,
                            destination);
                    }

                    return connection;
                }
                catch (Exception e)
                {
                    _logger.LogError(e,
                        i + 1 != m_Factories.Length
                            ? "{Method}: Failed to create rmq connection to {HostName} (will try other known hosts) {Destination}"
                            : "{Method}: Failed to create rmq connection to {HostName} {Destination}",
                        nameof(CreateConnection),
                        m_Factories[i].Endpoint.HostName,
                        destination);
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
        }

        public IMessagingSession CreateSession(Action onFailure, bool confirmedSending, Destination destination = default(Destination))
        {
            if(m_IsDisposed.WaitOne(0))
                throw new ObjectDisposedException("Transport is disposed");

            var connection = CreateConnection(true, destination);
            var session = new RabbitMqSession(
                    _loggerFactory,
                    connection,
                    confirmedSending,
                    (rabbitMqSession, dst, exception) =>
                    {
                        lock (m_Sessions)
                        {
                            m_Sessions.Remove(rabbitMqSession);
                            _logger.LogError(exception,
                                "{Method}: Failed to send message to destination '{Destination}' broker '{HostName}'. Treating session as broken.",
                                nameof(CreateSession),
                                dst,
                                connection.Endpoint.HostName);
                        }
                    });

            connection.ConnectionShutdown += (c, reason) =>
                {
                    lock (m_Sessions)
                    {
                        m_Sessions.Remove(session);
                    }

                    if ((reason.Initiator != ShutdownInitiator.Application || reason.ReplyCode != 200) && onFailure != null)
                    {
                        _logger.LogWarning(
                            "ConnectionShutdown: Rmq session to {HostName} is broken. Reason: {Reason}",
                            connection.Endpoint.HostName,
                            reason);

                        // If m_NetworkRecoveryInterval is null this
                        //         means that native Rabbit MQ 
                        //         automaic recovery is disabled
                        //         and processing group recovery mechanism must be enabled
                        // If m_NetworkRecoveryInterval is set to some value this 
                        //         means that native Rabbit MQ 
                        //         automaic recovery is enabled 
                        //         and there is not need to use processing group recovery mechanism
                        if (!m_NetworkRecoveryInterval.HasValue) 
                        {
                            onFailure();
                        }
                    }
                };

            lock (m_Sessions)
            {
                m_Sessions.Add(session);
            }

            return session;
        }

        public IMessagingSession CreateSession(Action onFailure, Destination destination = default(Destination))
        {
            return CreateSession(onFailure, false, destination);
        }

        public bool VerifyDestination(
            Destination destination,
            EndpointUsage usage,
            bool configureIfRequired,
            out string error)
        {
            try
            {
                var publish = PublicationAddress.Parse(destination.Publish) ?? new PublicationAddress("topic", destination.Publish, "");
                using (IConnection connection = CreateConnection(false, destination))
                {
                    using (IModel channel = connection.CreateModel())
                    {
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
