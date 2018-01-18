namespace Inceptum.Messaging.Serialization
{
    public class MessagePackSerializerFactory : ISerializerFactory
    {
        public string SerializationFormat
            => "messagepack";

        public IMessageSerializer<TMessage> Create<TMessage>()
        {
            return new MessagePackSerializer<TMessage>();
        }
    }
}