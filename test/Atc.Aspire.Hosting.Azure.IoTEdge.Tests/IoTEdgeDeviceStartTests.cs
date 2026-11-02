namespace Atc.Aspire.Hosting.Azure.IoTEdge.Tests;

public sealed class IoTEdgeDeviceStartTests : IDisposable
{
    // A well-formed connection string; the tests fail before anything calls IoT Hub.
    // The key is built at runtime, so the source holds nothing that secret scanners take for a real connection string.
    private static readonly string ConnectionString =
        $"HostName=myhub.azure-devices.net;SharedAccessKeyName=iothubowner;SharedAccessKey={Convert.ToBase64String(new byte[32])}";

    private readonly DirectoryInfo manifestDirectory = Directory.CreateTempSubdirectory("atc-iotedge-start-tests-");

    public void Dispose()
        => manifestDirectory.Delete(recursive: true);

    [Fact]
    public async Task Start_EmptyConnectionString_FailsToStartWithTheReason()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder();
        var device = builder.AddIoTEdgeDevice("edge-device", builder.AddParameter("iothub-connection-string", string.Empty, secret: true));

        // Act
        var state = await StartAsync(builder, device);

        // Assert
        state.Should().Be(KnownResourceStates.FailedToStart);

        await FluentActions.Awaiting(() => device.Resource.WaitForProvisioningAsync(TestContext.Current.CancellationToken))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*'iothub-connection-string' has no IoT Hub connection string*");
    }

    [Fact]
    public async Task Start_InvalidDeviceIdParameter_FailsToStartWithTheReason()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder();
        var device = AddDevice(builder)
            .WithDeviceId(builder.AddParameter("edge-device-id", "my device"));

        // Act
        var state = await StartAsync(builder, device);

        // Assert
        state.Should().Be(KnownResourceStates.FailedToStart);

        await FluentActions.Awaiting(() => device.Resource.WaitForProvisioningAsync(TestContext.Current.CancellationToken))
            .Should().ThrowAsync<ArgumentException>()
            .WithMessage("*not a valid IoT Hub device id*");
    }

    [Fact]
    public async Task Start_InvalidManifestJson_FailsToStartWithTheReason()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder();
        var device = AddDevice(builder)
            .WithDeploymentManifest(WriteManifest("{ not json"));

        // Act
        var state = await StartAsync(builder, device);

        // Assert
        state.Should().Be(KnownResourceStates.FailedToStart);

        await FluentActions.Awaiting(() => device.Resource.WaitForProvisioningAsync(TestContext.Current.CancellationToken))
            .Should().ThrowAsync<FormatException>()
            .WithMessage("The deployment manifest is not valid JSON.");
    }

    [Fact]
    public async Task Start_ModuleMissingFromManifest_FailsToStartNamingTheModule()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder();
        var device = AddDevice(builder)
            .WithDeploymentManifest(WriteManifest(
                """
                {
                  "modulesContent": {
                    "$edgeAgent": { "properties.desired": { "modules": { "othermodule": { "type": "docker" } } } }
                  }
                }
                """));

        builder.AddContainer("module", "some/image", "1.0").AsIoTEdgeModule(device);

        // Act
        var state = await StartAsync(builder, device);

        // Assert
        state.Should().Be(KnownResourceStates.FailedToStart);

        await FluentActions.Awaiting(() => device.Resource.WaitForProvisioningAsync(TestContext.Current.CancellationToken))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("The module(s) 'module' are not in the deployment manifest*");
    }

    [Fact]
    public async Task Start_EdgeHubConfigurationWithoutSchemaVersion_FailsToStartWithTheReason()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder();
        var device = AddDevice(builder)
            .WithDeploymentManifest(WriteManifest(
                """
                {
                  "modulesContent": {
                    "$edgeHub": { "properties.desired": { "routes": { "toCloud": "FROM /messages/* INTO $upstream" } } }
                  }
                }
                """))
            .WithEdgeHub();

        // Act
        var state = await StartAsync(builder, device);

        // Assert
        state.Should().Be(KnownResourceStates.FailedToStart);

        await FluentActions.Awaiting(() => device.Resource.WaitForProvisioningAsync(TestContext.Current.CancellationToken))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("The desired properties of $edgeHub in the deployment manifest*have no schemaVersion*");
    }

    private static IResourceBuilder<IoTEdgeDeviceResource> AddDevice(
        IDistributedApplicationBuilder builder)
        => builder.AddIoTEdgeDevice("edge-device", builder.AddParameter("iothub-connection-string", ConnectionString, secret: true));

    private static async Task<string?> StartAsync(
        IDistributedApplicationBuilder builder,
        IResourceBuilder<IoTEdgeDeviceResource> device)
    {
        // Raises the event Aspire raises when the AppHost starts, without starting anything else.
        await using var app = builder.Build();

        var eventing = app.Services.GetRequiredService<IDistributedApplicationEventing>();
        var notifications = app.Services.GetRequiredService<ResourceNotificationService>();

        await eventing.PublishAsync(
            new InitializeResourceEvent(
                device.Resource,
                eventing,
                app.Services.GetRequiredService<ResourceLoggerService>(),
                notifications,
                app.Services),
            TestContext.Current.CancellationToken);

        return notifications.TryGetCurrentState(device.Resource.Name, out var resourceEvent)
            ? resourceEvent.Snapshot.State?.Text
            : null;
    }

    private string WriteManifest(string json)
    {
        var path = Path.Combine(manifestDirectory.FullName, "deployment.json");
        File.WriteAllText(path, json);
        return path;
    }
}