namespace Atc.Aspire.Hosting.Azure.IoTHub.Internal;

/// <summary>
/// Reads an IoT Hub's routes, custom endpoints and consumer groups through Azure Resource Manager,
/// with the developer's existing Azure sign-in. It finds the hub by name, so no subscription or resource group is needed.
/// </summary>
/// <param name="credential">The Azure credential.</param>
internal sealed partial class ArmIoTHubSettingsReader(TokenCredential credential)
{
    /// <summary>
    /// Creates a reader that signs in like Aspire does when it provisions Azure resources, but never with a browser
    /// or a managed identity: e.g. with the Azure CLI or Visual Studio sign-in.
    /// </summary>
    /// <param name="tenantId">The tenant to sign in to (<c>Azure:TenantId</c>), or <see langword="null"/> for the default.</param>
    /// <returns>The reader.</returns>
    public static ArmIoTHubSettingsReader Create(string? tenantId)
        => new(new DefaultAzureCredential(new DefaultAzureCredentialOptions
        {
            TenantId = tenantId,
            ExcludeManagedIdentityCredential = true,
            ExcludeInteractiveBrowserCredential = true,
            AdditionallyAllowedTenants = { "*" },
        }));

    /// <summary>
    /// Reads the settings of an IoT Hub.
    /// </summary>
    /// <param name="hubName">The IoT Hub name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The settings, or why they couldn't be read.</returns>
    public async Task<IoTHubSettingsReadResult> ReadAsync(
        string hubName,
        CancellationToken cancellationToken)
    {
        // The name goes into a Resource Graph query, so only names IoT Hub allows get there.
        if (!HubName().IsMatch(hubName))
        {
            return IoTHubSettingsReadResult.NotVerified($"'{hubName}' isn't an IoT Hub name.");
        }

        try
        {
            var arm = new ArmClient(credential);
            var resourceId = await FindAsync(arm, hubName, cancellationToken);
            if (resourceId is null)
            {
                return IoTHubSettingsReadResult.NotVerified(
                    $"The Azure account sees no IoT Hub named '{hubName}'. If the hub is in another tenant, set Azure:TenantId.");
            }

            var hub = (await arm.GetIotHubDescriptionResource(resourceId).GetAsync(cancellationToken)).Value;

            var consumerGroups = new List<string>();
            await foreach (var consumerGroup in hub.GetEventHubConsumerGroupInfos(IoTHubSetupCheck.BuiltInEndpointName).GetAllAsync(cancellationToken))
            {
                consumerGroups.Add(consumerGroup.Data.Name);
            }

            return IoTHubSettingsReadResult.Read(FromData(hub.Data, consumerGroups));
        }
        catch (CredentialUnavailableException)
        {
            return IoTHubSettingsReadResult.NotVerified("Not signed in to Azure, e.g. with 'az login'.");
        }
        catch (AuthenticationFailedException ex)
        {
            return IoTHubSettingsReadResult.NotVerified($"Signing in to Azure failed: {ex.Message}");
        }
        catch (RequestFailedException ex) when (ex.Status == 403)
        {
            return IoTHubSettingsReadResult.NotVerified($"The Azure account may not read IoT Hub '{hubName}'; it needs e.g. the Reader role.");
        }
    }

    /// <summary>
    /// Maps the hub as Azure Resource Manager reports it to the settings the check needs.
    /// </summary>
    /// <param name="data">The hub.</param>
    /// <param name="consumerGroups">The consumer groups of the built-in endpoint.</param>
    /// <returns>The settings.</returns>
    internal static IoTHubSettings FromData(
        IotHubDescriptionData data,
        IEnumerable<string> consumerGroups)
    {
        var routing = data.Properties?.Routing;
        var endpoints = routing?.Endpoints;

        var customEndpointNames = endpoints is null
            ? []
            : endpoints.EventHubs.Select(x => x.Name)
                .Concat(endpoints.ServiceBusQueues.Select(x => x.Name))
                .Concat(endpoints.ServiceBusTopics.Select(x => x.Name))
                .Concat(endpoints.StorageContainers.Select(x => x.Name))
                .Concat(endpoints.CosmosDBSqlContainers.Select(x => x.Name))
                .ToList();

        var fallbackRoute = routing?.FallbackRoute is { } fallback
            ? new IoTHubRoute(fallback.Name ?? "$fallback", fallback.Source.ToString(), [.. fallback.EndpointNames], fallback.IsEnabled)
            : null;

        return new IoTHubSettings(
            routing?.Routes.Select(x => new IoTHubRoute(x.Name, x.Source.ToString(), [.. x.EndpointNames], x.IsEnabled)).ToList() ?? [],
            fallbackRoute,
            customEndpointNames,
            [.. consumerGroups]);
    }

    private static async Task<ResourceIdentifier?> FindAsync(
        ArmClient arm,
        string hubName,
        CancellationToken cancellationToken)
    {
        // A Resource Graph query is made on a tenant resource; it searches the subscriptions of the tenant signed in to.
        await using var tenants = arm.GetTenants().GetAllAsync(cancellationToken).GetAsyncEnumerator(cancellationToken);
        if (!await tenants.MoveNextAsync())
        {
            return null;
        }

        var tenant = tenants.Current;

        // Resource Graph searches every subscription the account can read. IoT Hub names are unique, so at most one matches.
        var result = await tenant.GetResourcesAsync(
            new ResourceQueryContent($"resources | where type =~ 'microsoft.devices/iothubs' and name =~ '{hubName}' | project id"),
            cancellationToken);

        using var rows = JsonDocument.Parse(result.Value.Data.ToString());

        return rows.RootElement.GetArrayLength() > 0 &&
               rows.RootElement[0].GetProperty("id").GetString() is { } id
            ? new ResourceIdentifier(id)
            : null;
    }

    // IoT Hub names have 3 to 50 letters, digits and hyphens.
    [GeneratedRegex("^[A-Za-z0-9-]{3,50}$", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex HubName();
}