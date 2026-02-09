using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;

namespace Heimdall.Server
{
    internal sealed class BifrostSubscribeToken
    {
        private readonly ITimeLimitedDataProtector _protector;

        public BifrostSubscribeToken(IDataProtectionProvider dp)
        {
            _protector = dp
                .CreateProtector("Heimdall.Bifrost.SubscribeToken.v1")
                .ToTimeLimitedDataProtector();
        }

        public string Create(string topic, TimeSpan ttl)
        {
            if (string.IsNullOrWhiteSpace(topic))
                throw new ArgumentException("Topic is required.", nameof(topic));

            if (ttl <= TimeSpan.Zero)
                ttl = TimeSpan.FromMinutes(2);

            var nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
            var payload = $"{topic}|{nonce}";

            return _protector.Protect(payload, ttl);
        }

        public bool TryValidate(string topic, string token)
        {
            if (string.IsNullOrWhiteSpace(topic) || string.IsNullOrWhiteSpace(token))
                return false;

            try
            {
                var payload = _protector.Unprotect(token);
                var parts = payload.Split('|', 2);

                return parts.Length > 0 &&
                       string.Equals(parts[0], topic, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }
    }
}
