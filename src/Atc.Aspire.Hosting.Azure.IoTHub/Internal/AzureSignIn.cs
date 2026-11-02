namespace Atc.Aspire.Hosting.Azure.IoTHub.Internal;

/// <summary>
/// The Azure credentials the bridge uses to check and set up an IoT Hub.
/// </summary>
internal static class AzureSignIn
{
    /// <summary>
    /// Signs in like Aspire does when it provisions Azure resources, but never with a browser or a managed identity:
    /// e.g. with the Azure CLI or Visual Studio sign-in. For the check at start, which mustn't open anything.
    /// </summary>
    /// <param name="tenantId">The tenant to sign in to (<c>Azure:TenantId</c>), or <see langword="null"/> for the default.</param>
    /// <returns>The credential.</returns>
    public static TokenCredential Existing(string? tenantId)
        => new DefaultAzureCredential(new DefaultAzureCredentialOptions
        {
            TenantId = tenantId,
            ExcludeManagedIdentityCredential = true,
            ExcludeInteractiveBrowserCredential = true,
            AdditionallyAllowedTenants = { "*" },
        });

    /// <summary>
    /// Signs in with the existing sign-in, otherwise with the browser. For the setup command, which the developer starts.
    /// </summary>
    /// <param name="tenantId">The tenant to sign in to (<c>Azure:TenantId</c>), or <see langword="null"/> for the default.</param>
    /// <returns>The credential.</returns>
    public static TokenCredential ExistingOrBrowser(string? tenantId)
        => new ChainedTokenCredential(
            Existing(tenantId),
            new InteractiveBrowserCredential(new InteractiveBrowserCredentialOptions
            {
                TenantId = tenantId,
                AdditionallyAllowedTenants = { "*" },
            }));
}