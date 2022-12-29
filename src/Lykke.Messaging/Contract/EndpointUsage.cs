using System;

namespace Lykke.Messaging.Contract
{
    /// <summary>
    /// If endpoint is used to send commands or receive events or for both
    /// </summary>
    // @atarutin TODO: most probably, should be a part of endpoint or destination definition
    // looks like it somehow duplicated the destination type which is already defined in the endpoint
    // and has Publish/Subscribe semantics
    [Flags]
    public enum EndpointUsage
    {
        None,
        Publish,
        Subscribe,
    }
}
