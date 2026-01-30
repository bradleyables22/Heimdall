using Heimdall.Server;
using Microsoft.AspNetCore.Html;

namespace Heimdall.Example.Raw.Home
{
    public static class Home
    {
        [ContentInvocation]
        public static IHtmlContent GetTitle()
        {
            return new HtmlString("Welcome to Heimdall Example Raw!");
        }
    }
}
