namespace Atc.Aspire.Hosting.Azure.Kusto.Tests;

[Trait(Traits.Category, Traits.Categories.Integration)]
public class AppHostTests(AspireIntegrationTestFixture<Projects.Atc_Aspire_Hosting_Azure_Kusto_AppHost> fixture) : IClassFixture<AspireIntegrationTestFixture<Projects.Atc_Aspire_Hosting_Azure_Kusto_AppHost>>
{
    [Fact]
    public async Task ResourceStartsAndRespondsOk()
    {
        // Arrange
        const string resourceName = "kusto-emulator";
        await fixture.ResourceNotificationService
            .WaitForResourceHealthyAsync(resourceName)
            .WaitAsync(TimeSpan.FromMinutes(5));

        var httpClient = fixture.CreateHttpClient(resourceName);

        // Act
        var response = await httpClient.GetAsync("/");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ApiServiceGetTodoData()
    {
        // Arrange
        const string resourceName = "apiservice";

        await fixture.ResourceNotificationService
            .WaitForResourceHealthyAsync("kusto-emulator")
            .WaitAsync(TimeSpan.FromMinutes(5));

        await fixture.ResourceNotificationService
            .WaitForResourceHealthyAsync(resourceName)
            .WaitAsync(TimeSpan.FromMinutes(5));

        var httpClient = fixture.CreateHttpClient(resourceName);

        // Act & Assert
        var getResponse = await httpClient.GetAsync("/todo");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var data = await getResponse.Content.ReadFromJsonAsync<List<object>>();
        data
            .Should().NotBeNull()
            .And.NotBeEmpty()
            .And.HaveCount(3);
    }
}