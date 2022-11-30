using System;
using Microsoft.Extensions.Logging;

namespace Lykke.Messaging.Serialization
{
    public class MessagePackSerializerFactory : ISerializerFactory
    {
        private readonly ILoggerFactory _loggerFactory;

        public SerializationFormat SerializationFormat => SerializationFormat.MessagePack;

        public MessagePackSerializerFactory(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        }

        public IMessageSerializer<TMessage> Create<TMessage>()
        {
            return new MessagePackSerializer<TMessage>(_loggerFactory);
        }

        public static class Defaults
        {
            public static MessagePack.IFormatterResolver FormatterResolver { get; set; } = null;
        }
    }
}