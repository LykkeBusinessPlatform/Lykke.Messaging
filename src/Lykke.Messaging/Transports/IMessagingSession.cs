using System;
using Lykke.Messaging.Contract;

namespace Lykke.Messaging.Transports
{
    /// <summary>
    /// Messaging session interface, that is used to send and receive messages.
    /// Is an abstraction of communication channel to the messaging system.
    /// </summary>
    public interface IMessagingSession : IDisposable
    {
        /// <summary>
        /// Sends a message to the specified destination.
        /// </summary>
        /// <param name="destination">The destionation to send message to</param>
        /// <param name="message">The message body</param>
        /// <param name="ttl">Message expiration if applicable</param>
        void Send(string destination, BinaryMessage message, int ttl);
        
        /// <summary>
        /// ???
        /// </summary>
        /// <param name="destination"></param>
        /// <param name="message"></param>
        /// <param name="callback"></param>
        /// <returns></returns>
        RequestHandle SendRequest(string destination, BinaryMessage message, Action<BinaryMessage> callback);
        
        /// <summary>
        /// ???
        /// </summary>
        /// <param name="destination"></param>
        /// <param name="handler"></param>
        /// <param name="messageType"></param>
        /// <returns></returns>
        IDisposable RegisterHandler(string destination, Func<BinaryMessage, BinaryMessage> handler, string messageType);
        
        /// <summary>
        /// Subscribes to the specified destination with the specified handler.
        /// </summary>
        /// <param name="destination"></param>
        /// <param name="callback"></param>
        /// <param name="messageType"></param>
        /// <returns></returns>
        IDisposable Subscribe(string destination, Action<BinaryMessage, Action<bool>> callback, string messageType);
        
        /// <summary>
        /// Creates new destination with random name
        /// </summary>
        /// <returns></returns>
        Destination CreateTemporaryDestination();
    }
}