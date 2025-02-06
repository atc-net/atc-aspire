namespace Atc.Aspire.Azure.Kusto.Tests;

public sealed class AspireKustoSettingsTests
{
    [Fact]
    public void HostAddressIsNullByDefault() =>
        Assert.Null(new AspireKustoSettings().HostAddress);

    [Fact]
    public void DatabaseNameIsNullByDefault() =>
        Assert.Null(new AspireKustoSettings().DatabaseName);

    [Fact]
    public void CredentialIsNullByDefault() =>
        Assert.Null(new AspireKustoSettings().Credential);

    [Fact]
    public void DisableHealthChecksIsFalseByDefault() =>
        Assert.False(new AspireKustoSettings().DisableHealthChecks);

    [Fact]
    public void HealthCheckTimeoutIsNullByDefault() =>
        Assert.Null(new AspireKustoSettings().HealthCheckTimeout);
}