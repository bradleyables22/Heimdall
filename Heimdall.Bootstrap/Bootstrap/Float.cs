namespace Heimdall.Bootstrap
{
    public static partial class Bootstrap
    {
        public static class Float
        {
            public const string Start = "float-start";
            public const string End = "float-end";
            public const string None = "float-none";
            public const string Clearfix = "clearfix";

            public static string At(Breakpoint bp, FloatKind kind)
            {
                var k = kind switch
                {
                    FloatKind.Start => "start",
                    FloatKind.End => "end",
                    FloatKind.None => "none",
                    _ => throw new ArgumentOutOfRangeException(nameof(kind))
                };

                return bp == Breakpoint.None
                    ? $"float-{k}"
                    : $"float-{Bp(bp)}-{k}";
            }
        }
    }
}
