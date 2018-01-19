using System;

namespace Inceptum.Messaging.Serialization
{
    internal class MessagePackSerializer<TMessage> : IMessageSerializer<TMessage>
    {
        private readonly MessagePack.IFormatterResolver _formatterResolver;
        private readonly ProtobufSerializer<TMessage> _protobufSerializer;

        public MessagePackSerializer()
        {
            _formatterResolver = MessagePack.Resolvers.ContractlessStandardResolver.Instance;
            _protobufSerializer = new ProtobufSerializer<TMessage>();
        }


        public byte[] Serialize(TMessage message)
        {
            return MessagePack.MessagePackSerializer.Serialize(message, _formatterResolver);
        }

        public TMessage Deserialize(byte[] message)
        {
            try
            {
                return MessagePack.MessagePackSerializer.Deserialize<TMessage>(message, _formatterResolver);
            }
            catch (Exception)
            {
                return _protobufSerializer.Deserialize(message);
            }
            
        }
    }
}