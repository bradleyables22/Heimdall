using System.Security.Cryptography;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
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

        public string Create(string topic, ClaimsPrincipal user, TimeSpan ttl)
        {
            if (string.IsNullOrWhiteSpace(topic))
                throw new ArgumentException("Topic is required.", nameof(topic));

            if (ttl <= TimeSpan.Zero)
                ttl = TimeSpan.FromMinutes(2);

            var nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
            var payload = JsonSerializer.Serialize(new SubscribeTokenPayload(
                topic,
                CreateUserBinding(user),
                nonce));

            return _protector.Protect(payload, ttl);
        }

        public bool TryValidate(string topic, string token, ClaimsPrincipal user)
        {
            if (string.IsNullOrWhiteSpace(topic) || string.IsNullOrWhiteSpace(token))
                return false;

            try
            {
                var payload = _protector.Unprotect(token);
                var data = JsonSerializer.Deserialize<SubscribeTokenPayload>(payload);

                return data is not null &&
                       string.Equals(data.Topic, topic, StringComparison.OrdinalIgnoreCase) &&
                       string.Equals(data.UserBinding, CreateUserBinding(user), StringComparison.Ordinal);
            }
            catch
            {
                return false;
            }
        }

        private static string CreateUserBinding(ClaimsPrincipal user)
        {
            if (user.Identity?.IsAuthenticated != true)
                return string.Empty;

            var claims = GetStableUserClaims(user).ToArray();
            if (claims.Length == 0)
            {
                claims = user.Claims
                    .Select(claim => (claim.Type, claim.Value))
                    .ToArray();
            }

            var raw = string.Join(
                "\n",
                claims
                    .OrderBy(claim => claim.Type, StringComparer.Ordinal)
                    .ThenBy(claim => claim.Value, StringComparer.Ordinal)
                    .Select(claim => $"{claim.Type}={claim.Value}"));

            raw = $"{user.Identity.AuthenticationType ?? string.Empty}\n{raw}";

            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
        }

        private static IEnumerable<(string Type, string Value)> GetStableUserClaims(ClaimsPrincipal user)
        {
            var stableTypes = new[]
            {
                ClaimTypes.NameIdentifier,
                ClaimTypes.Name,
                "sub",
                "name",
                "preferred_username",
                "sid",
                "AspNet.Identity.SecurityStamp",
                "security_stamp"
            };

            foreach (var stableType in stableTypes)
            {
                foreach (var claim in user.FindAll(stableType))
                    yield return (claim.Type, claim.Value);
            }
        }

        private sealed class SubscribeTokenPayload
        {
            public SubscribeTokenPayload()
            {
            }

            public SubscribeTokenPayload(string topic, string userBinding, string nonce)
            {
                Topic = topic;
                UserBinding = userBinding;
                Nonce = nonce;
            }

            public string Topic { get; set; } = string.Empty;

            public string UserBinding { get; set; } = string.Empty;

            public string Nonce { get; set; } = string.Empty;
        }
    }
}
