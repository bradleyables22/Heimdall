using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Matching;

namespace Heimdall.Server;

internal sealed class HeimdallContentInvocationEndpointMetadata
{
    public HeimdallContentInvocationEndpointMetadata(string? invocationId)
    {
        InvocationId = invocationId;
    }

    public string? InvocationId { get; }

    public bool IsFallback => InvocationId is null;
}

internal sealed class HeimdallContentInvocationMatcherPolicy(ContentInvocationRegistry registry)
    : MatcherPolicy, IEndpointSelectorPolicy
{
    public override int Order => -1000;

    public bool AppliesToEndpoints(IReadOnlyList<Endpoint> endpoints)
        => endpoints.Any(endpoint =>
            endpoint.Metadata.GetMetadata<HeimdallContentInvocationEndpointMetadata>() is not null);

    public Task ApplyAsync(HttpContext httpContext, CandidateSet candidates)
    {
        var invocationId = ContentEndpoints.GetInvocationId(httpContext.Request);
        var isKnownInvocation = !string.IsNullOrWhiteSpace(invocationId) &&
            registry.TryGet(invocationId, out _);

        for (var index = 0; index < candidates.Count; index++)
        {
            var metadata = candidates[index].Endpoint.Metadata
                .GetMetadata<HeimdallContentInvocationEndpointMetadata>();

            if (metadata is null)
                continue;

            var isSelected = isKnownInvocation
                ? metadata.InvocationId is not null &&
                  string.Equals(metadata.InvocationId, invocationId, StringComparison.OrdinalIgnoreCase)
                : metadata.IsFallback;

            candidates.SetValidity(index, isSelected);
        }

        return Task.CompletedTask;
    }
}
