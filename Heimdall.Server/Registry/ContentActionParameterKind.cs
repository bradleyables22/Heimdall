
namespace Heimdall.Server.Registry
{
    internal enum ContentInvocationParameterKind
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
