namespace Heimdall.Server.Rendering
{
    public static partial class HeimdallHtml
    {
        /// <summary>Identifies a browser Web Storage area.</summary>
        public enum StorageArea
        {
            /// <summary>The origin-scoped local storage area.</summary>
            Local,
            /// <summary>The tab-scoped session storage area.</summary>
            Session
        }

        internal static string StorageAreaToString(StorageArea area)
            => area switch
            {
                StorageArea.Local => "local",
                StorageArea.Session => "session",
                _ => throw new ArgumentOutOfRangeException(nameof(area), area, "Unsupported storage area.")
            };

        internal static string NormalizeStorageKey(string key, string parameterName = "key")
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Storage key is required.", parameterName);

            return key.Trim();
        }
    }
}
