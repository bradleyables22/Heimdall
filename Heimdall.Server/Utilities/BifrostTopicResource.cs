using Microsoft.AspNetCore.Http;

namespace Heimdall.Server
{
    /// <summary>
    /// Authorization resource passed when evaluating access to a Bifrost topic.
    /// </summary>
    /// <param name="Topic">The topic being subscribed to.</param>
    /// <param name="HttpContext">The current HTTP context.</param>
    public sealed record BifrostTopicResource(string Topic, HttpContext HttpContext);
}
