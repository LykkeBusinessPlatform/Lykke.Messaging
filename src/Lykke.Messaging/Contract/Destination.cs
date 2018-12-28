namespace Lykke.Messaging.Contract
{
    public struct Destination
    {
        private bool? _isDurable;

        public string Publish { get; set; }
        public string Subscribe { get; set; }

        public bool IsDurable
        {
            get => _isDurable ?? true;
            set => _isDurable = value;
        }

        public static implicit operator Destination(string destination)
        {
            return new Destination
            {
                Publish = destination,
                Subscribe = destination,
            };
        }

        public bool Equals(Destination other)
        {
            return string.Equals(Publish, other.Publish)
                && string.Equals(Subscribe, other.Subscribe)
                && IsDurable == other.IsDurable;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
                return false;

            return obj is Destination destination && Equals(destination);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = ((Publish != null ? Publish.GetHashCode() : 0) * 397) ^ (Subscribe != null ? Subscribe.GetHashCode() : 0);
                hashCode = hashCode * 2 + (IsDurable ? 1 : 0);
                return hashCode;
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
            if (Subscribe == Publish)
                return "["+Subscribe+"]";
            return $"[s:{Subscribe}, p:{Publish}], d:{IsDurable}";
        }
    }
}
