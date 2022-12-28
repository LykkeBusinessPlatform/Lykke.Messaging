using System;

namespace Lykke.Messaging.Contract
{
    /// <summary>
    /// Transport-agnostic destination address.
    /// Keeps information about the destination address for publishing and
    /// subscribing.
    /// Depending on the underlying transport the address can differ depending
    /// on its usage for publishing or subscribing.
    /// By default the address is the same for publishing and subscribing.
    /// </summary>
    public readonly struct Destination
    {
        /// <summary>
        /// Address used for publishing.
        /// </summary>
        public string Publish { get; }
        
        /// <summary>
        /// Address used for subscribing.
        /// </summary>
        public string Subscribe { get; }

        public Destination(string publish, string subscribe)
        {
            if (string.IsNullOrWhiteSpace(publish))
                throw new ArgumentNullException(nameof(publish), "Publish address cannot be empty");
            Publish = publish;
            
            if (string.IsNullOrWhiteSpace(subscribe))
                throw new ArgumentNullException(nameof(subscribe), "Subscribe address cannot be empty");
            Subscribe = subscribe;
        }

        private Destination(string address):this(address, address)
        {
        }

        public static implicit operator Destination(string destination)
        {
            return new Destination(destination);
        }

        private bool Equals(Destination other)
        {
            return string.Equals(Publish, other.Publish) && string.Equals(Subscribe, other.Subscribe);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is Destination destination && Equals(destination);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((Publish != null ? Publish.GetHashCode() : 0) * 397) ^
                       (Subscribe != null ? Subscribe.GetHashCode() : 0);
            }
        }

        public static bool operator ==(Destination left, Destination right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Destination left, Destination right)
        {
            return !left.Equals(right);
        }

        public override string ToString()
        {
            return Subscribe == Publish 
                ? $"[{Subscribe}]" 
                : $"[s:{Subscribe}, p:{Publish}]";
        }
    }
}
