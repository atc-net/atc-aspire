namespace Atc.Aspire.Hosting.Azure.IoTEdge.Tests;

/// <summary>
/// Builders and readers shared by the device and module tests.
/// </summary>
internal static class IoTEdgeTestHelpers
{
    public static IResourceBuilder<IoTEdgeDeviceResource> AddDevice(
        IDistributedApplicationBuilder builder)
        => builder.AddIoTEdgeDevice(
            "edge-device",
            builder.AddParameter("iothub-connection-string", secret: true));

    public static IoTEdgeDeviceIdentity CreateIdentity()
        => new("myhub.azure-devices.net", "edge-device");

    /// <summary>
    /// Provisions the device with an IoT Hub service that returns module connection strings in the library's format,
    /// built from the request, so a test sees the gateway host name each module asked for.
    /// </summary>
    /// <param name="device">The device to provision.</param>
    /// <returns>The IoT Hub service substitute, for tests that change its answers.</returns>
    public static IIoTHubService SetProvisioned(IoTEdgeDeviceResource device)
    {
        var iotHubService = Substitute.For<IIoTHubService>();
        iotHubService
            .GetModuleConnectionString(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(x => Task.FromResult<string?>(CreateConnectionString(x.ArgAt<string>(0), x.ArgAt<string>(1), x.ArgAt<string?>(2))));

        device.IoTHubService = iotHubService;
        device.SetProvisioned(CreateIdentity());

        return iotHubService;
    }

    public static async Task<Dictionary<string, string>> GetEnvironmentVariables(
        IResource resource,
        DistributedApplicationOperation operation = DistributedApplicationOperation.Run)
    {
        var configuration = await ExecutionConfigurationBuilder
            .Create(resource)
            .WithEnvironmentVariablesConfig()
            .BuildAsync(
                new DistributedApplicationExecutionContext(operation),
                NullLogger.Instance,
                TestContext.Current.CancellationToken);

        configuration.Exception.Should().BeNull();
        return configuration.EnvironmentVariables.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);
    }

    private static string CreateConnectionString(
        string deviceId,
        string moduleId,
        string? gatewayHostName)
        => "HostName=myhub.azure-devices.net;" +
           (gatewayHostName is null ? string.Empty : $"GatewayHostName={gatewayHostName};") +
           $"DeviceId={deviceId};ModuleId={moduleId};SharedAccessKey={(moduleId == "$edgeHub" ? "edgehubkey" : "modulekey")}";

    public static List<WaitType> GetEdgeHubWaitTypes(
        IResourceBuilder<ContainerResource> module)
        => module.Resource.Annotations
            .OfType<WaitAnnotation>()
            .Where(x => x.Resource is IoTEdgeHubResource)
            .Select(x => x.WaitType)
            .ToList();
}