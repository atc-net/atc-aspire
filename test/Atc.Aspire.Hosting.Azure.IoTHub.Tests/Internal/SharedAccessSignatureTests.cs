namespace Atc.Aspire.Hosting.Azure.IoTHub.Tests.Internal;

public sealed class SharedAccessSignatureTests
{
    [Fact]
    public void Create_ResourcePolicyAndExpiry_SignsTheEncodedResourceAndExpiryWithTheKey()
    {
        // Arrange
        var key = new byte[32];
        key[0] = 1;
        var expiresAt = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000);

        // Act
        var token = SharedAccessSignature.Create("myhub.azure-devices.net/messages/events", "service", Convert.ToBase64String(key), expiresAt);

        // Assert
        using var hmac = new HMACSHA256(key);
        var signature = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes("myhub.azure-devices.net%2Fmessages%2Fevents\n1800000000")));

        token.Should().Be(
            $"SharedAccessSignature sr=myhub.azure-devices.net%2Fmessages%2Fevents&sig={System.Net.WebUtility.UrlEncode(signature)}&se=1800000000&skn=service");
    }
}