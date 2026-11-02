namespace Atc.Aspire.Hosting.Azure.IoTEdge;

/// <summary>
/// Marks a resource as an IoT Edge module on an emulated IoT Edge device.
/// </summary>
/// <param name="Device">The device the module belongs to.</param>
/// <param name="ModuleResource">The Aspire resource running the module.</param>
/// <param name="ModuleId">The module id in IoT Hub.</param>
public sealed record IoTEdgeModuleAnnotation(
    IoTEdgeDeviceResource Device,
    IResource ModuleResource,
    string ModuleId) : IResourceAnnotation;