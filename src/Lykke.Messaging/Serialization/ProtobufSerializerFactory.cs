using System;
using Microsoft.Extensions.Logging;

namespace Lykke.Messaging.Serialization
{
    public class ProtobufSerializerFactory : ISerializerFactory
    {
        private readonly ILoggerFactory _logFactory;

        public SerializationFormat SerializationFormat => SerializationFormat.ProtoBuf;


        public ProtobufSerializerFactory(ILoggerFactory loggerFactory)
        {
            _logFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        }

        public IMessageSerializer<TMessage> Create<TMessage>()
        {
            return new ProtobufSerializer<TMessage>(_logFactory);
        }
    }
}