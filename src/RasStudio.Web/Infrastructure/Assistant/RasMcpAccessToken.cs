using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;

namespace RasStudio.Web.Infrastructure.Assistant;

public sealed class RasMcpAccessToken(IConfiguration configuration)
{
    private readonly byte[] _tokenBytes = Encoding.UTF8.GetBytes(CreateToken(configuration));

    public string AuthorizationHeaderValue =>
        $"Bearer {Encoding.UTF8.GetString(_tokenBytes)}";

    public bool IsAuthorized(string? authorizationHeader)
    {
        if (!AuthenticationHeaderValue.TryParse(authorizationHeader, out var header) ||
            !string.Equals(header.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrEmpty(header.Parameter))
            return false;

        var suppliedToken = Encoding.UTF8.GetBytes(header.Parameter);

        return suppliedToken.Length == _tokenBytes.Length &&
               CryptographicOperations.FixedTimeEquals(suppliedToken, _tokenBytes);
    }

    private static string CreateToken(IConfiguration configuration)
    {
        var configuredToken = configuration["Mcp:AccessToken"];

        return string.IsNullOrWhiteSpace(configuredToken)
            ? Convert.ToHexString(RandomNumberGenerator.GetBytes(32))
            : configuredToken.Trim();
    }
}
