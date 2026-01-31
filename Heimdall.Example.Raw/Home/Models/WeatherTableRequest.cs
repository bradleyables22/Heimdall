namespace Heimdall.Example.Raw.Home.Models
{
	public class WeatherTableRequest
	{
		public int Page { get; set; } = 1;
		public int PageSize { get; set; } = 5;
		public string? Q { get; set; }
	}
}
