namespace Atc.Aspire.Hosting.Azure.IoTEdge.Tests.Internal;

public sealed class IoTEdgeModuleValidatorTests
{
    private const string SampleOpcPublisherImage = "mcr.microsoft.com/iotedge/opc-publisher:2.9.18";

    [Fact]
    public async Task GetModulesMissingFromManifest_SampleAppHostModules_ReturnsNone()
    {
        // Arrange
        var manifest = await LoadSampleTemplate();

        // Act
        var missing = IoTEdgeModuleValidator.GetModulesMissingFromManifest(
            manifest,
            ["opcpublisher", "simulationmodule", "opcpublishernodemanagermodule"]);

        // Assert
        missing.Should().BeEmpty();
    }

    [Fact]
    public async Task GetModulesMissingFromManifest_UnknownModule_ReturnsIt()
    {
        // Arrange
        var manifest = await LoadSampleTemplate();

        // Act
        var missing = IoTEdgeModuleValidator.GetModulesMissingFromManifest(
            manifest,
            ["opcpublisher", "unknownmodule"]);

        // Assert
        missing.Should().Equal("unknownmodule");
    }

    [Fact]
    public void GetModulesMissingFromManifest_ManifestWithoutModules_ReturnsNone()
    {
        // Arrange
        var manifest = DeploymentManifest.Parse("""{ "modulesContent": {} }""");

        // Act
        var missing = IoTEdgeModuleValidator.GetModulesMissingFromManifest(manifest, ["anymodule"]);

        // Assert
        missing.Should().BeEmpty();
    }

    [Fact]
    public async Task GetDifferingManifestImage_SampleOpcPublisherContainer_ReturnsNull()
    {
        // Arrange
        var manifest = await LoadSampleTemplate();
        var containerImage = ContainerImageName.GetReference(new ContainerImageAnnotation
        {
            Registry = "mcr.microsoft.com",
            Image = "iotedge/opc-publisher",
            Tag = "2.9.18",
        });

        // Act
        var differingImage = IoTEdgeModuleValidator.GetDifferingManifestImage(manifest, "opcpublisher", containerImage);

        // Assert
        differingImage.Should().BeNull();
    }

    [Theory]
    [InlineData("iotedge/opc-publisher:2.9.20")]
    [InlineData("mcr.microsoft.com/iotedge/opc-publisher")]
    public async Task GetDifferingManifestImage_OtherTagOrNoRegistry_ReturnsNull(
        string containerImage)
    {
        // Arrange
        var manifest = await LoadSampleTemplate();

        // Act
        var differingImage = IoTEdgeModuleValidator.GetDifferingManifestImage(manifest, "opcpublisher", containerImage);

        // Assert
        differingImage.Should().BeNull();
    }

    [Fact]
    public async Task GetDifferingManifestImage_OtherRepository_ReturnsManifestImage()
    {
        // Arrange
        var manifest = await LoadSampleTemplate();

        // Act
        var differingImage = IoTEdgeModuleValidator.GetDifferingManifestImage(
            manifest,
            "opcpublisher",
            "mcr.microsoft.com/iotedge/opc-plc:2.15.11");

        // Assert
        differingImage.Should().Be(SampleOpcPublisherImage);
    }

    [Theory]
    [InlineData("simulationmodule")]
    [InlineData("opcpublishernodemanagermodule")]
    [InlineData("notinmanifest")]
    public async Task GetDifferingManifestImage_PlaceholderOrNoImage_ReturnsNull(
        string moduleId)
    {
        // Arrange
        var manifest = await LoadSampleTemplate();

        // Act
        var differingImage = IoTEdgeModuleValidator.GetDifferingManifestImage(manifest, moduleId, "any/image:1.0");

        // Assert
        differingImage.Should().BeNull();
    }

    [Theory]
    [InlineData("""{ "libraries": { "Microsoft.Azure.Devices.Client/1.42.4": { "type": "package" } } }""", true)]
    [InlineData("""{ "libraries": { "Atc/4.0.0": { "type": "package" } } }""", false)]
    [InlineData("""{ "version": 3 }""", null)]
    [InlineData("not json", null)]
    public void ReferencesDeviceSdk_AssetsFile_ReturnsWhetherTheSdkIsAmongTheLibraries(
        string assetsJson,
        bool? expected)
    {
        // Arrange
        var projectDirectory = Directory.CreateTempSubdirectory("atc-iotedge-tests-");

        try
        {
            var objDirectory = Directory.CreateDirectory(Path.Combine(projectDirectory.FullName, "obj"));
            File.WriteAllText(Path.Combine(objDirectory.FullName, "project.assets.json"), assetsJson);

            // Act
            var referencesSdk = IoTEdgeModuleValidator.ReferencesDeviceSdk(Path.Combine(projectDirectory.FullName, "MyModule.csproj"));

            // Assert
            referencesSdk.Should().Be(expected);
        }
        finally
        {
            projectDirectory.Delete(recursive: true);
        }
    }

    [Fact]
    public void ReferencesDeviceSdk_NoAssetsFile_ReturnsNull()
    {
        // Arrange
        var projectDirectory = Directory.CreateTempSubdirectory("atc-iotedge-tests-");

        try
        {
            // Act
            var referencesSdk = IoTEdgeModuleValidator.ReferencesDeviceSdk(Path.Combine(projectDirectory.FullName, "MyModule.csproj"));

            // Assert
            referencesSdk.Should().BeNull();
        }
        finally
        {
            projectDirectory.Delete(recursive: true);
        }
    }

    private static Task<DeploymentManifest> LoadSampleTemplate()
        => DeploymentManifest.LoadAsync(
            Path.Combine("TestData", "deployment.template.json"),
            TestContext.Current.CancellationToken);
}