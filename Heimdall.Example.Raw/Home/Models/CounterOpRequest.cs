namespace Heimdall.Example.Raw.Home.Models
{
    public class CounterOpRequest
    {
        public string? Op { get; set; } // "inc" | "dec" | "reset" | "get"
        public int Step { get; set; } = 1;
    }
}
