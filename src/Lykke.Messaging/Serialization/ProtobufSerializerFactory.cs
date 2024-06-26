﻿using System;
using Common.Log;
using Lykke.Common.Log;

namespace Lykke.Messaging.Serialization
{
    public class ProtobufSerializerFactory : ISerializerFactory
    {
        private readonly ILog _log;
        private readonly ILogFactory _logFactory;

        public SerializationFormat SerializationFormat => SerializationFormat.ProtoBuf;

        [Obsolete]
        public ProtobufSerializerFactory(ILog log)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        public ProtobufSerializerFactory(ILogFactory logFactory)
        {
            _logFactory = logFactory ?? throw new ArgumentNullException(nameof(logFactory));
        }

        public IMessageSerializer<TMessage> Create<TMessage>()
        {            
            return _logFactory != null
            ? new ProtobufSerializer<TMessage>(_logFactory)
            : new ProtobufSerializer<TMessage>(_log);
        }
    }
}
