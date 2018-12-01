﻿using System;

namespace Lykke.Messaging
{
    public class TransportInfo
    {
        public string Broker { get; }
        public string Login { get; }
        public string Password { get; }
        public string JailStrategyName { get; }
        public string Messaging { get; }

        public JailStrategy JailStrategy { get; internal set; }

        public TransportInfo(
            string broker,
            string login,
            string password,
            string jailStrategyName,
            string messaging = "InMemory")
        {
            if (string.IsNullOrEmpty((broker ?? "").Trim()))
                throw new ArgumentException("broker should be not empty string", nameof(broker));
            if (string.IsNullOrEmpty((login ?? "").Trim()))
                throw new ArgumentException("login should be not empty string", nameof(login));
            if (string.IsNullOrEmpty((password ?? "").Trim()))
                throw new ArgumentException("password should be not empty string", nameof(password));

            Broker = broker;
            Login = login;
            Password = password;
            JailStrategyName = jailStrategyName;
            Messaging = messaging;
        }

        protected bool Equals(TransportInfo other)
        {
            return string.Equals(Broker, other.Broker)
                && string.Equals(Login, other.Login)
                && string.Equals(Password, other.Password)
                && string.Equals(Messaging, other.Messaging)
                && string.Equals(JailStrategyName, other.JailStrategyName);
        }

        public static bool operator ==(TransportInfo left, TransportInfo right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(TransportInfo left, TransportInfo right)
        {
            return !Equals(left, right);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
                return false;
            if (ReferenceEquals(this, obj))
                return true;
            if (obj.GetType() != this.GetType())
                return false;
            return Equals((TransportInfo) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (Broker != null ? Broker.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ (Login != null ? Login.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ (Password != null ? Password.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ (Messaging != null ? Messaging.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ (JailStrategyName != null ? JailStrategyName.GetHashCode() : 0);
                return hashCode;
            }
        }

        public override string ToString()
        {
            return $"Broker: {Broker}, Login: {Login}";
        }
    }
}
