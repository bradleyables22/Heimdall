
namespace Heimdall.Server.Registry
{
    internal enum ContentActionParameterKind
    {
        HttpContext,
        CancellationToken,
        ClaimsPrincipal,
        ClientInfo,
        Service,
        Payload,
        FormPayload,
        FormFile
    }
}
