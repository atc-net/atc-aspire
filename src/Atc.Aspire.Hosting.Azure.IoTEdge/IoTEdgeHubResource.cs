namespace Atc.Aspire.Hosting.Azure.IoTEdge;

/// <summary>
/// Represents a local edgeHub container that the modules of an emulated IoT Edge device connect through.
/// </summary>
public sealed class IoTEdgeHubResource : ContainerResource
{
    /// <summary>
    /// The container registry of the edgeHub image.
    /// </summary>
    public const string DefaultRegistry = "mcr.microsoft.com";

    /// <summary>
    /// The edgeHub image.
    /// </summary>
    public const string DefaultImage = "azureiotedge-hub";

    /// <summary>
    /// The edgeHub image tag (IoT Edge 1.6 LTS).
    /// </summary>
    public const string DefaultTag = "1.6";

    /// <summary>
    /// The name of the MQTT endpoint.
    /// </summary>
    public const string MqttEndpointName = "mqtt";

    /// <summary>
    /// The name of the AMQP endpoint.
    /// </summary>
    public const string AmqpEndpointName = "amqp";

    /// <summary>
    /// The name of the endpoint for MQTT and AMQP over WebSockets.
    /// </summary>
    public const string HttpsEndpointName = "https";

    /// <summary>
    /// The MQTT port edgeHub listens on.
    /// </summary>
    public const int MqttPort = 8883;

    /// <summary>
    /// The AMQP port edgeHub listens on.
    /// </summary>
    public const int AmqpPort = 5671;

    /// <summary>
    /// The port edgeHub listens on for MQTT and AMQP over WebSockets.
    /// </summary>
    public const int HttpsPort = 443;

    /// <summary>
    /// The directory the certificates are mounted at inside containers.
    /// </summary>
    public const string ContainerCertificateDirectory = "/iotedge/certs";

    /// <summary>
    /// The directory edgeHub stores its state in inside the container, when a data volume is added.
    /// </summary>
    public const string ContainerStorageDirectory = "/iotedge/storage";

    /// <summary>
    /// Initializes a new instance of the <see cref="IoTEdgeHubResource"/> class.
    /// </summary>
    /// <param name="name">The resource name, which is also the host name containers use to reach edgeHub.</param>
    /// <param name="device">The device this edgeHub belongs to.</param>
    /// <param name="certificates">The edgeHub test certificates on the host.</param>
    public IoTEdgeHubResource(
        string name,
        IoTEdgeDeviceResource device,
        EdgeHubCertificateFiles certificates)
        : base(name)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(certificates);

        Device = device;
        Certificates = certificates;
    }

    /// <summary>
    /// Gets the device this edgeHub belongs to.
    /// </summary>
    public IoTEdgeDeviceResource Device { get; }

    /// <summary>
    /// Gets the edgeHub test certificates on the host.
    /// </summary>
    public EdgeHubCertificateFiles Certificates { get; }

    /// <summary>
    /// Gets the host name containers use to reach edgeHub on the Aspire container network.
    /// </summary>
    public string ContainerHostName => Name;

    /// <summary>
    /// Gets the edgeHub MQTT endpoint.
    /// </summary>
    public EndpointReference MqttEndpoint => new(this, MqttEndpointName);
}