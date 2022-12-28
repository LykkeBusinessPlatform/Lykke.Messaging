using Lykke.Messaging.Serialization;
using System;

namespace Lykke.Messaging.Contract
{
    /// <summary>
	/// Transport-specific destination address
	/// </summary>
	// @atarutin: Taking into account comments on TransportId and SerializationFormat,
	// there is no distinction between Destination and Endpoint structs, unless
    // SharedDestination field brings in something valuable. Consider merging types
    // Endpoint and Destination.
	public readonly struct Endpoint
	{
	    /// <summary>
	    /// The transport identifier
	    /// </summary>
	    // @atarutin TODO: probably, the TransportId should be a part of the
	    // transport, not of the Endpoint
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
		    Destination = new Destination(destination);

		    SharedDestination = sharedDestination;
		    SerializationFormat = serializationFormat;
		}

        public override string ToString()
	    {
	        return $"[Transport: {TransportId}, Destination: {Destination}]";
	    }
	}
}
