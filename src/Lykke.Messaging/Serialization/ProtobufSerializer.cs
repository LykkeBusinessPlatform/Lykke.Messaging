﻿using Microsoft.Extensions.Logging;

namespace Lykke.Messaging.Serialization
{
    internal class ProtobufSerializer<TMessage> : IMessageSerializer<TMessage>
    {
        private readonly ResilientBinarySerializer<TMessage> _serializer;

        public ProtobufSerializer(ILoggerFactory loggerFactory)
        {
            _serializer = new ResilientBinarySerializer<TMessage>(loggerFactory, SerializationFormat.ProtoBuf);
        }

        public byte[] Serialize(TMessage message)
        {
            return _serializer.Serialize(message);
        }

        public TMessage Deserialize(byte[] message)
        {
            return _serializer.Deserialize(message);
        }
    }
}
