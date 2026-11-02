namespace Atc.Aspire.Hosting.Azure.IoTHub.Internal;

/// <summary>
/// Adds what the bridge needs to an IoT Hub through Azure Resource Manager: its consumer group and its routes.
/// </summary>
/// <param name="credential">The Azure credential.</param>
internal sealed class ArmIoTHubSetupWriter(TokenCredential credential)
{
    /// <summary>
    /// Adds the missing parts to a hub.
    /// </summary>
    /// <param name="hubName">The IoT Hub name.</param>
    /// <param name="missing">What the hub misses, from <see cref="IoTHubSetupCheck"/>.</param>
    /// <param name="consumerGroup">The bridge's consumer group.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes when the hub has been changed.</returns>
    /// <exception cref="InvalidOperationException">The hub couldn't be changed; the message says why.</exception>
    public async Task ApplyAsync(
        string hubName,
        IReadOnlyList<IoTHubSetupItem> missing,
        string consumerGroup,
        CancellationToken cancellationToken)
    {
        if (!ArmIoTHubSettingsReader.IsHubName(hubName))
        {
            throw new InvalidOperationException($"'{hubName}' isn't an IoT Hub name.");
        }

        try
        {
            var arm = new ArmClient(credential);
            var resourceId = await ArmIoTHubSettingsReader.FindAsync(arm, hubName, cancellationToken)
                             ?? throw new InvalidOperationException($"The Azure account sees no IoT Hub named '{hubName}'.");

            var hub = (await arm.GetIotHubDescriptionResource(resourceId).GetAsync(cancellationToken)).Value;

            // The consumer group first: it's a change of its own that leaves the hub's settings alone.
            if (missing.Contains(IoTHubSetupItem.ConsumerGroup))
            {
                await hub.GetEventHubConsumerGroupInfos(IoTHubSetupCheck.BuiltInEndpointName).CreateOrUpdateAsync(
                    WaitUntil.Completed,
                    consumerGroup,
                    new EventHubConsumerGroupInfoCreateOrUpdateContent(consumerGroup),
                    cancellationToken);
            }

            var data = hub.Data;
            if (!IoTHubSetupChanges.AddRoutes(data, missing))
            {
                return;
            }

            // Routes are part of the hub's settings, which are written back whole, as 'az iot hub message-route create' does.
            // Reading the hub leaves out the access policies, and writing it without them keeps them.
            // The ETag makes the write fail if the hub changed since it was read, instead of overwriting that change.
            var resourceGroup = arm.GetResourceGroupResource(
                ResourceGroupResource.CreateResourceIdentifier(resourceId.SubscriptionId, resourceId.ResourceGroupName));

            await resourceGroup.GetIotHubDescriptions().CreateOrUpdateAsync(
                WaitUntil.Completed,
                resourceId.Name,
                data,
                data.ETag?.ToString(),
                cancellationToken);
        }
        catch (RequestFailedException ex) when (ex.Status == 412)
        {
            throw new InvalidOperationException($"IoT Hub '{hubName}' changed while it was being set up. Run the setup again.", ex);
        }
        catch (RequestFailedException ex) when (ex.Status == 403)
        {
            throw new InvalidOperationException(
                $"The Azure account may not change IoT Hub '{hubName}'; it needs e.g. the Contributor role on the hub.",
                ex);
        }
        catch (RequestFailedException ex)
        {
            throw new InvalidOperationException($"Azure refused to change IoT Hub '{hubName}': {ex.Message}", ex);
        }
        catch (AuthenticationFailedException ex)
        {
            throw new InvalidOperationException($"Signing in to Azure failed: {ex.Message}", ex);
        }
    }
}