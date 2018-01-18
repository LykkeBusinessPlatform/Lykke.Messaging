using System;
using System.IO;

namespace Inceptum.Messaging.Serialization
{
    internal class MessagePackSerializer<TMessage> : IMessageSerializer<TMessage>
    {
        private readonly MsgPack.Serialization.MessagePackSerializer<TMessage> _serializer;

        public MessagePackSerializer()
        {
            _serializer = MsgPack.Serialization.MessagePackSerializer.Get<TMessage>();
        }

        public byte[] Serialize(TMessage message)
        {
            using (var stream = new MemoryStream())
            {
                _serializer.Pack(stream, message);

                return stream.ToArray();
            }
        }

        public TMessage Deserialize(byte[] message)
        {
            using (var stream = new MemoryStream(message))
            {
                return _serializer.Unpack(stream);
            }
        }
    }
}