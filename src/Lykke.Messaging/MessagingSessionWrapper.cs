﻿using System;
using Lykke.Messaging.Contract;
using Lykke.Messaging.Transports;
using Microsoft.Extensions.Logging;

namespace Lykke.Messaging
{
    /// <summary>
    /// @atarutin: Didn't realize the intention yet
    /// It is implemented as decorator however it just routes calls to an
    /// underlying messaging session. Looks like the wrapper just adds error
    /// handling as a separate method.
    /// </summary>
    internal class MessagingSessionWrapper:IMessagingSession
    {

        private readonly ILogger<MessagingSessionWrapper> _logger;
        private IMessagingSession _messagingSession;

        public string TransportId { get; private set; }
        public string Name { get; private set; }

        public event Action OnFailure;

        public MessagingSessionWrapper(ILoggerFactory loggerFactory, string transportId, string name)
        {
            if (loggerFactory == null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }

            _logger = loggerFactory.CreateLogger<MessagingSessionWrapper>();
            
            TransportId = transportId;
            Name = name;
        }

        public void SetSession(IMessagingSession messagingSession)
        {
            _messagingSession = messagingSession;
        }

        public void ReportFailure()
        {
            if (OnFailure == null)
                return;

            foreach (var handler in OnFailure.GetInvocationList())
            {
                try
                {
                    handler.DynamicInvoke();
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "{Method}: error", nameof(ReportFailure));
                }
            }
        }

        public void Dispose()
        {
            if (_messagingSession == null)
                return;
            _messagingSession.Dispose();
            _messagingSession = null;
        }

        public void Send(string destination, BinaryMessage message, int ttl)
        {
            _messagingSession.Send(destination, message, ttl);
        }

        public RequestHandle SendRequest(string destination, BinaryMessage message, Action<BinaryMessage> callback)
        {
            return _messagingSession.SendRequest(destination, message, callback);
        }

        public IDisposable RegisterHandler(string destination, Func<BinaryMessage, BinaryMessage> handler, string messageType)
        {
            return _messagingSession.RegisterHandler(destination, handler, messageType);
        }

        public IDisposable Subscribe(string destination, Action<BinaryMessage, Action<bool>> callback, string messageType)
        {
            return _messagingSession.Subscribe(destination,callback, messageType);
        }

        public Destination CreateTemporaryDestination()
        {
            return _messagingSession.CreateTemporaryDestination();
        }
    }
}