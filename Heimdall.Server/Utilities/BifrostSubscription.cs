using System.Threading.Channels;

namespace Heimdall.Server.Utilities
{
    internal readonly record struct BifrostSubscription(
        Guid Id,
        ChannelReader<BifrostMessage> Reader,
        Action Unsubscribe,
        Task<string> DisconnectRequested,
        Func<string, bool> RequestDisconnect);
}
