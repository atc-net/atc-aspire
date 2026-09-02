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
            .WaitForResourceHealthyAsync(resourceName, TestContext.Current.CancellationToken)
            .WaitAsync(TimeSpan.FromMinutes(5), TestContext.Current.CancellationToken);

        var httpClient = fixture.CreateHttpClient(resourceName);

        // Act
        var response = await httpClient.GetAsync("/", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ApiServiceGetTodoData()
    {
        // Arrange
        const string resourceName = "apiservice";

        await fixture.ResourceNotificationService
            .WaitForResourceHealthyAsync("kusto-emulator", TestContext.Current.CancellationToken)
            .WaitAsync(TimeSpan.FromMinutes(5), TestContext.Current.CancellationToken);

        await fixture.ResourceNotificationService
            .WaitForResourceHealthyAsync(resourceName, TestContext.Current.CancellationToken)
            .WaitAsync(TimeSpan.FromMinutes(5), TestContext.Current.CancellationToken);

        var httpClient = fixture.CreateHttpClient(resourceName);

        // Act & Assert
        var getResponse = await httpClient.GetAsync("/todo", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var data = await getResponse.Content.ReadFromJsonAsync<List<object>>(cancellationToken: TestContext.Current.CancellationToken);
        data
            .Should().NotBeNull()
            .And.NotBeEmpty()
            .And.HaveCount(3);
    }
}