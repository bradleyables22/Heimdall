using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Matching;

namespace Heimdall.Server;

internal sealed class HeimdallContentActionEndpointMetadata
{
    public HeimdallContentActionEndpointMetadata(string? actionId)
    {
        ActionId = actionId;
    }

    public string? ActionId { get; }

    public bool IsFallback => ActionId is null;
}

internal sealed class HeimdallContentActionMatcherPolicy(ContentRegistry registry)
    : MatcherPolicy, IEndpointSelectorPolicy
{
    public override int Order => -1000;

    public bool AppliesToEndpoints(IReadOnlyList<Endpoint> endpoints)
        => endpoints.Any(endpoint =>
            endpoint.Metadata.GetMetadata<HeimdallContentActionEndpointMetadata>() is not null);

    public Task ApplyAsync(HttpContext httpContext, CandidateSet candidates)
    {
        var actionId = httpContext.Request.Headers["X-Heimdall-Content-Action"].ToString();
        var isKnownAction = !string.IsNullOrWhiteSpace(actionId) && registry.TryGet(actionId, out _);

        for (var index = 0; index < candidates.Count; index++)
        {
            var metadata = candidates[index].Endpoint.Metadata
                .GetMetadata<HeimdallContentActionEndpointMetadata>();

            if (metadata is null)
                continue;

            var isSelected = isKnownAction
                ? metadata.ActionId is not null &&
                  string.Equals(metadata.ActionId, actionId, StringComparison.OrdinalIgnoreCase)
                : metadata.IsFallback;

            candidates.SetValidity(index, isSelected);
        }

        return Task.CompletedTask;
    }
}
