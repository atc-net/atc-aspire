namespace Atc.Aspire.Hosting.Azure.IoTEdge;

/// <summary>
/// The identity of a provisioned IoT Edge device.
/// </summary>
/// <param name="IotHubHostName">The IoT Hub host name, e.g. myhub.azure-devices.net.</param>
/// <param name="DeviceId">The edge device id.</param>
public sealed record IoTEdgeDeviceIdentity(
    string IotHubHostName,
    string DeviceId);