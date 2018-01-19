namespace Inceptum.Messaging.Serialization
{
    internal class MessagePackSerializer<TMessage> : IMessageSerializer<TMessage>
    {
        private readonly MessagePack.IFormatterResolver _formatterResolver;


        public MessagePackSerializer()
        {
            _formatterResolver = MessagePack.Resolvers.ContractlessStandardResolver.Instance;
        }


        public byte[] Serialize(TMessage message)
        {
            return MessagePack.MessagePackSerializer.Serialize(message, _formatterResolver);
        }

        public TMessage Deserialize(byte[] message)
        {
            return MessagePack.MessagePackSerializer.Deserialize<TMessage>(message, _formatterResolver);
        }
    }
}