namespace Atc.Aspire.Hosting.Azure.Kusto.ApiService.Options;

public sealed class ConfigureScalarOptions : IConfigureOptions<ScalarOptions>
{
    public void Configure(ScalarOptions options)
    {
        options.Title = "Atc.Aspire.Hosting.Azure.Kusto.ApiService";
        options.DarkMode = true;
        options.DefaultHttpClient = new KeyValuePair<ScalarTarget, ScalarClient>(ScalarTarget.CSharp, ScalarClient.HttpClient);
    }
}