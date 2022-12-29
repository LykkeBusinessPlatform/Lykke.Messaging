namespace Lykke.Messaging
{
    public interface ITransportInfoResolver
    {
        TransportInfo Resolve(string transportId);
    }
}
