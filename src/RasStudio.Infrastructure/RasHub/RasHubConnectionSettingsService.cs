using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using Nava.Settings.Abstractions;
using RasStudio.Application.RasHub;

namespace RasStudio.Infrastructure.RasHub;

public sealed class RasHubConnectionSettingsService(
    ISettingsProvider<StoredRasHubConnectionSettings> settingsProvider,
    IDataProtectionProvider dataProtectionProvider,
    ILogger<RasHubConnectionSettingsService> logger)
    : IRasHubConnectionProvider, IRasHubConnectionSettings
{
    private const string ProtectionPurpose = "RasStudio.RasHub.UserApiKey.v1";
    private const int ApiKeyMaxLength = 64;

    private readonly IDataProtector _protector =
        dataProtectionProvider.CreateProtector(ProtectionPurpose);

    public RasHubConnection GetRequiredConnection()
    {
        var settings = settingsProvider.Settings;

        if (string.IsNullOrWhiteSpace(settings.BaseUrl) ||
            string.IsNullOrWhiteSpace(settings.ProtectedApiKey))
            throw new RasHubConnectionNotConfiguredException();

        var baseAddress = NormalizeBaseAddress(settings.BaseUrl);

        try
        {
            var apiKey = _protector.Unprotect(settings.ProtectedApiKey);
            return new RasHubConnection(baseAddress, apiKey);
        }
        catch (CryptographicException exception)
        {
            logger.LogWarning(
                exception,
                "Unable to unprotect the saved RasHub user API key");
            throw new RasHubApiException(
                "The saved RasHub API key cannot be decrypted. Save the connection again.",
                innerException: exception);
        }
    }

    public RasHubConnectionState Current
    {
        get
        {
            var settings = settingsProvider.Settings;
            return new RasHubConnectionState(
                settings.BaseUrl,
                !string.IsNullOrWhiteSpace(settings.ProtectedApiKey));
        }
    }

    public async Task SaveAsync(
        SaveRasHubConnection connection,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);

        var baseAddress = NormalizeBaseAddress(connection.BaseUrl);
        var current = settingsProvider.Settings;
        var protectedApiKey = current.ProtectedApiKey;

        if (connection.ApiKey is null &&
            !string.IsNullOrWhiteSpace(protectedApiKey) &&
            !HasSameBaseAddress(current.BaseUrl, baseAddress))
            throw new RasHubConnectionValidationException(
                "A new RasHub API key is required when the RasHub URL changes.");

        if (connection.ApiKey is not null)
        {
            ValidateApiKey(connection.ApiKey);
            protectedApiKey = _protector.Protect(connection.ApiKey);
        }

        if (string.IsNullOrWhiteSpace(protectedApiKey))
            throw new RasHubConnectionValidationException("RasHub API key is required.");

        cancellationToken.ThrowIfCancellationRequested();
        await settingsProvider.UpdateAsync(
            new StoredRasHubConnectionSettings
            {
                BaseUrl = baseAddress.AbsoluteUri,
                ProtectedApiKey = protectedApiKey
            });

        logger.LogInformation("RasHub connection settings were saved");
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await settingsProvider.UpdateAsync(new StoredRasHubConnectionSettings());
        logger.LogInformation("RasHub connection settings were removed");
    }

    private static bool HasSameBaseAddress(string currentValue, Uri baseAddress)
    {
        try
        {
            return NormalizeBaseAddress(currentValue).Equals(baseAddress);
        }
        catch (RasHubConnectionValidationException)
        {
            return false;
        }
    }

    private static Uri NormalizeBaseAddress(string value)
    {
        var trimmed = value.Trim();

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
            string.IsNullOrWhiteSpace(uri.Host))
            throw new RasHubConnectionValidationException(
                "Enter an absolute HTTP or HTTPS RasHub URL.");

        if (!string.IsNullOrEmpty(uri.UserInfo) ||
            !string.IsNullOrEmpty(uri.Query) ||
            !string.IsNullOrEmpty(uri.Fragment))
            throw new RasHubConnectionValidationException(
                "RasHub URL cannot contain credentials, a query, or a fragment.");

        var builder = new UriBuilder(uri)
        {
            Path = uri.AbsolutePath.EndsWith("/", StringComparison.Ordinal)
                ? uri.AbsolutePath
                : $"{uri.AbsolutePath}/"
        };

        return builder.Uri;
    }

    private static void ValidateApiKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new RasHubConnectionValidationException("RasHub API key is required.");
        if (!string.Equals(value, value.Trim(), StringComparison.Ordinal))
            throw new RasHubConnectionValidationException(
                "RasHub API key cannot contain leading or trailing whitespace.");
        if (value.Length > ApiKeyMaxLength)
            throw new RasHubConnectionValidationException(
                $"RasHub API key cannot exceed {ApiKeyMaxLength} characters.");
    }
}
