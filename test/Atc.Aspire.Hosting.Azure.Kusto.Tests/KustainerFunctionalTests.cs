namespace Atc.Aspire.Hosting.Azure.Kusto.Tests;

[Trait(Traits.Category, Traits.Categories.Integration)]
public class KustainerFunctionalTests
{
    private readonly ITestOutputHelper output;

    public KustainerFunctionalTests(ITestOutputHelper output)
    {
        this.output = output;
    }

    [Fact]
    public async Task VerifyKustainerResource()
    {
        using var builder = TestDistributedApplicationBuilder.Create(output);
        var kustainer = builder.AddKustainer();

        await using var app = await builder.BuildAsync();
        await app.StartAsync();

        await app.WaitForTextAsync("Connect to this node by using the connection string", kustainer.Resource.Name);

        var connectionString = await kustainer.Resource.ConnectionStringExpression.GetValueAsync(default);
        Assert.False(string.IsNullOrEmpty(connectionString));
    }
}