using Lykke.Messaging.Serialization;
using System;

namespace Lykke.Messaging.Contract
{
    /// <summary>
	/// Transport-specific destination address
	/// </summary>
	public readonly struct Endpoint
	{
	    /// <summary>
	    /// The transport identifier
	    /// </summary>
		public string TransportId { get; }

	    /// <summary>
	    /// The destination
	    /// </summary>
        public Destination Destination { get; }

	    /// <summary>
	    /// ???
	    /// </summary>
		public bool SharedDestination { get; }

	    /// <summary>
	    /// Message serialization format
	    /// </summary>
	    // @atarutin TODO: probably, the serialization format should be a part of the
	    // transport not of the endpoint
		public SerializationFormat SerializationFormat { get; }

        public Endpoint(
            string transportId,
            string destination,
            bool sharedDestination = false,
            SerializationFormat serializationFormat = SerializationFormat.ProtoBuf)
	    {
		    if (string.IsNullOrWhiteSpace(transportId))
			    throw new ArgumentNullException(nameof(transportId), "TransportId must be specified");
		    TransportId = transportId;

		    if (string.IsNullOrWhiteSpace(destination))
			    throw new ArgumentNullException(nameof(destination), "Destination must be specified");
		    Destination = destination;

		    SharedDestination = sharedDestination;
		    SerializationFormat = serializationFormat;
		}

        public override string ToString()
	    {
	        return $"[Transport: {TransportId}, Destination: {Destination}]";
	    }
	}
}
