namespace Atc.Aspire.Hosting.Azure.Kusto.ApiService.Contracts;

public record Todo(
    int Id,
    string Title,
    string Description,
    Status Status,
    Priority Priority,
    DateTimeOffset Created,
    DateTimeOffset? Closed);

public enum Status
{
    Pending,
    Started,
    Ended,
}

public enum Priority
{
    Low,
    Medium,
    High,
}