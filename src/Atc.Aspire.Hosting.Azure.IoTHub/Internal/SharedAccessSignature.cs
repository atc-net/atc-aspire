namespace Atc.Aspire.Hosting.Azure.IoTHub.Internal;

/// <summary>
/// Builds the shared access signature token that IoT Hub accepts for a shared access policy.
/// </summary>
internal static class SharedAccessSignature
{
    /// <summary>
    /// Creates a token for a resource, signed with the key of a shared access policy.
    /// </summary>
    /// <param name="resource">The resource the token is for, e.g. <c>myhub.azure-devices.net/messages/events</c>.</param>
    /// <param name="keyName">The name of the shared access policy.</param>
    /// <param name="key">The base64 key of the shared access policy.</param>
    /// <param name="expiresAt">When the token expires.</param>
    /// <returns>The token: <c>SharedAccessSignature sr=...&amp;sig=...&amp;se=...&amp;skn=...</c>.</returns>
    public static string Create(
        string resource,
        string keyName,
        string key,
        DateTimeOffset expiresAt)
    {
        var encodedResource = WebUtility.UrlEncode(resource);
        var expiry = expiresAt.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);

        using var hmac = new HMACSHA256(Convert.FromBase64String(key));
        var signature = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes($"{encodedResource}\n{expiry}")));

        return $"SharedAccessSignature sr={encodedResource}&sig={WebUtility.UrlEncode(signature)}&se={expiry}&skn={WebUtility.UrlEncode(keyName)}";
    }
}