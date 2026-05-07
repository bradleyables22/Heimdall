
namespace Heimdall.Server
{
    internal sealed record BifrostMessage(
         string Topic,
         string EventName,
         string Id,
         string Html,
         DateTimeOffset CreatedUtc,
         DateTimeOffset ExpiresUtc
     );
}
