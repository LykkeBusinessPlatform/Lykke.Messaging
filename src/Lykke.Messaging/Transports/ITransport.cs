using System;
using Lykke.Messaging.Contract;

namespace Lykke.Messaging.Transports
{
    /// <summary>
    /// Transport layer abstraction
    /// </summary>
    public interface ITransport : IDisposable
    {
        /// <summary>
        /// Create a session
        /// </summary>
        /// <param name="onFailure">Failure handler</param>
        /// <param name="displayName">Session display name</param>
        /// <returns></returns>
        IMessagingSession CreateSession(Action onFailure, string displayName);
        
        /// <summary>
        /// Ensures that destination exists
        /// </summary>
        /// <param name="destination"></param>
        /// <param name="usage"></param>
        /// <param name="configureIfRequired"></param>
        /// <param name="error"></param>
        /// <returns></returns>
        bool VerifyDestination(
            Destination destination,
            EndpointUsage usage,
            bool configureIfRequired,
            out string error);
    }
}