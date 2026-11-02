namespace Atc.Aspire.Hosting.Azure.IoTEdge.Internal;

/// <summary>
/// Environment variable names understood by the edgeHub container and by the IoT device SDK
/// when a module runs outside the IoT Edge runtime.
/// </summary>
/// <remarks>
/// <para>
/// The edgeHub names are edgeHub's own configuration keys, not a general standard. They are defined in
/// <see href="https://github.com/Azure/iotedge/blob/main/edge-hub/core/src/Microsoft.Azure.Devices.Edge.Hub.Service/Constants.cs">Constants.cs</see>
/// and read in
/// <see href="https://github.com/Azure/iotedge/blob/main/edge-hub/core/src/Microsoft.Azure.Devices.Edge.Hub.Service/DependencyManager.cs">DependencyManager.cs</see>
/// of the edgeHub source.
/// </para>
/// <para>
/// On a real device, edgeAgent sets these variables when it starts the edgeHub container, and the certificate files come
/// from the IoT Edge security daemon. Here the integration plays the part of edgeAgent, so it sets the variables itself
/// and points them at the generated test certificates.
/// </para>
/// <para>
/// edgeHub runs with <c>configSource=twin</c>, as on a real device: it reads its routes (with priority and time to live) and
/// its store and forward settings from the desired properties of its <c>$edgeHub</c> twin, which provisioning sets with a
/// single-device deployment, and it
/// reports its connected clients in the reported properties. With <c>configSource=local</c> it would read each route as a
/// plain string from its environment and report nothing.
/// </para>
/// <para>
/// The module names (<see cref="EdgeHubConnectionString"/> and <see cref="EdgeModuleCaCertificateFile"/>) are read by the
/// IoT device SDK in
/// <see href="https://github.com/Azure/azure-iot-sdk-csharp/blob/main/iothub/device/src/Edge/EdgeModuleClientFactory.cs">EdgeModuleClientFactory.cs</see>.
/// </para>
/// </remarks>
internal static class EdgeHubEnvironmentVariableNames
{
    /// <summary>
    /// The module connection string read by <c>ModuleClient.CreateFromEnvironmentAsync</c>.
    /// When set, the SDK bypasses the IoT Edge workload API (IOTEDGE_* variables).
    /// </summary>
    public const string EdgeHubConnectionString = "EdgeHubConnectionString";

    /// <summary>
    /// The CA certificate (PEM) that modules use to trust the edgeHub server certificate.
    /// </summary>
    public const string EdgeModuleCaCertificateFile = "EdgeModuleCACertificateFile";

    /// <summary>
    /// The $edgeHub module connection string used by the edgeHub container to connect upstream.
    /// </summary>
    public const string IotHubConnectionString = "IotHubConnectionString";

    /// <summary>
    /// The edgeHub server certificate (PFX without password).
    /// </summary>
    public const string EdgeModuleHubServerCertificateFile = "EdgeModuleHubServerCertificateFile";

    /// <summary>
    /// The edgeHub server CA chain (PEM).
    /// </summary>
    public const string EdgeModuleHubServerCaChainCertificateFile = "EdgeModuleHubServerCAChainCertificateFile";

    /// <summary>
    /// Selects where edgeHub reads its configuration from: <c>twin</c>, or anything else for local configuration.
    /// </summary>
    public const string ConfigSource = "configSource";

    /// <summary>
    /// The value for <see cref="ConfigSource"/> that makes edgeHub read its configuration from its <c>$edgeHub</c> twin.
    /// </summary>
    public const string ConfigSourceTwin = "twin";

    /// <summary>
    /// The protocol edgeHub uses towards IoT Hub (Amqp, AmqpWs, Mqtt or MqttWs).
    /// </summary>
    public const string UpstreamProtocol = "UpstreamProtocol";

    /// <summary>
    /// The directory edgeHub stores its state in (store and forward messages, the cache of identities). Defaults to a folder in the container.
    /// </summary>
    public const string StorageFolder = "StorageFolder";
}