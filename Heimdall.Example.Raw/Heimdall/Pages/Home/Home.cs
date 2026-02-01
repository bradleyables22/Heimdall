

using Heimdall.Server;
using Microsoft.AspNetCore.Html;

namespace Heimdall.Example.Raw.Heimdall.Pages.Home
{
	public static class Home
	{
		[ContentInvocation]
		public static IHtmlContent GetServerTime() 
		{
			return new HtmlString($"<h1>Server Time: {System.DateTime.Now}</h1>");
		}
		
	}
}
