﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Common.Log;
using Inceptum.Messaging.Contract;
using Inceptum.Messaging.RabbitMq;
using Inceptum.Messaging.Transports;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace Lykke.Messaging.RabbitMq
{
    internal class RabbitMqTransport : ITransport
    {
        private readonly ILog _log;
        private readonly TimeSpan? m_NetworkRecoveryInterval;
        private readonly ConnectionFactory[] m_Factories;
        private readonly List<RabbitMqSession> m_Sessions = new List<RabbitMqSession>();
        readonly ManualResetEvent m_IsDisposed=new ManualResetEvent(false);        
        private readonly bool m_ShuffleBrokersOnSessionCreate;
        private static readonly Random m_Random = new Random((int)DateTime.Now.Ticks & 0x0000FFFF);

        internal long SessionsCount
        {
            get { return m_Sessions.Count; }
        }
        public RabbitMqTransport(ILog log, string broker, string username, string password) : this(log, new[] {broker}, username, password)
        {

        }

        public RabbitMqTransport(ILog log, string[] brokers, string username, string password, bool shuffleBrokersOnSessionCreate=true, TimeSpan? networkRecoveryInterval = null)
        {
            _log = log;
            m_NetworkRecoveryInterval = networkRecoveryInterval;
            m_ShuffleBrokersOnSessionCreate = shuffleBrokersOnSessionCreate&& brokers.Length>1;
            if (brokers == null) throw new ArgumentNullException("brokers");
            if (brokers.Length == 0) throw new ArgumentException("brokers list is empty", "brokers");

            var factories = brokers.Select(brokerName =>
            {

                var f = new ConnectionFactory();
                Uri uri = null;
                f.UserName = username;
                f.Password = password;

                f.AutomaticRecoveryEnabled = m_NetworkRecoveryInterval.HasValue;
                if (m_NetworkRecoveryInterval.HasValue)
                {
                    f.NetworkRecoveryInterval = m_NetworkRecoveryInterval.Value; //it's default value
                }

                if (Uri.TryCreate(brokerName, UriKind.Absolute, out uri))
                {
                    f.Uri = uri.ToString();
                }
                else
                {
                    f.HostName = brokerName;
                }
                return f;
            });

            m_Factories = factories.ToArray();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private IConnection createConnection()
        {
            Exception exception=null;
            var factories = m_Factories;
            if (m_ShuffleBrokersOnSessionCreate)
            {
                
                factories = factories.OrderBy(x => m_Random.Next()).ToArray();
            }

            for (int i = 0; i < factories.Length; i++)
            {
                try
                {
                    var connection = factories[i].CreateConnection();
                    _log.WriteInfoAsync(nameof(RabbitMqTransport), nameof(createConnection), $"Created rmq connection to {factories[i].Endpoint.HostName}.");                    
                    return connection;
                }
                catch (Exception e)
                {
                    _log.WriteErrorAsync(nameof(RabbitMqTransport), nameof(createConnection), string.Format("Failed to create rmq connection to {0}{1}: ", factories[i].Endpoint.HostName, (i + 1 != factories.Length) ? " (will try other known hosts)" : ""), e);                    
                    exception = e;
                }
            }
            throw new TransportException("Failed to create rmq connection",exception);
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

        public IMessagingSession CreateSession(Action onFailure, bool confirmedSending)
        {
            if(m_IsDisposed.WaitOne(0))
                throw new ObjectDisposedException("Transport is disposed");

            var connection = createConnection();
            var session = new RabbitMqSession(connection, confirmedSending, (rabbitMqSession, destination, exception) =>
            {
                lock (m_Sessions)
                {
                    m_Sessions.Remove(rabbitMqSession);
                    _log.WriteErrorAsync(nameof(RabbitMqTransport), nameof(CreateSession), string.Format("Failed to send message to destination '{0}' broker '{1}'. Treating session as broken. ", destination, connection.Endpoint.HostName), exception);                   
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
                        _log.WriteWarningAsync(nameof(RabbitMqTransport), "ConnectionShutdown", string.Format("Rmq session to {0} is broken. Reason: {1}", connection.Endpoint.HostName, reason));                        

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

        public IMessagingSession CreateSession(Action onFailure)
        {
            return CreateSession(onFailure, false);
        }

        public bool VerifyDestination(Destination destination, EndpointUsage usage, bool configureIfRequired, out string error)
        {
            try
            {
                var publish = PublicationAddress.Parse(destination.Publish) ?? new PublicationAddress("topic", destination.Publish, ""); ;
                using (IConnection connection = createConnection())
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

                            if (configureIfRequired)
                            {
                                channel.QueueBind(destination.Subscribe, publish.ExchangeName, publish.RoutingKey == "" ? "#" : publish.RoutingKey);
                            }
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