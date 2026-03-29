
namespace Heimdall.Server
{
    internal sealed record BifrostMessage(
         string Topic,
         string Id,
         string Html,
         DateTimeOffset CreatedUtc,
         DateTimeOffset ExpiresUtc
     );
}
