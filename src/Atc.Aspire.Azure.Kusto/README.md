# Introduction

[![NuGet Version](https://img.shields.io/nuget/v/atc.aspire.azure.kusto.svg?logo=nuget&style=for-the-badge)](https://www.nuget.org/packages/atc.aspire.azure.kusto)

The `Atc.Aspire.Azure.Kusto` package provides an Aspire Client Integration for Azure Data Explorer (Kusto), wrapping the [Atc.Kusto](https://github.com/atc-net/atc-kusto) package. It simplifies integration by providing configuration-driven setup and health checks.

# Table of Content

- [Introduction](#introduction)
- [Table of Content](#table-of-content)
- [Getting Started](#getting-started)
  - [Installation](#installation)
  - [Wire-Up](#wire-up)
  - [Usage](#usage)
  - [Configuration](#configuration)
    - [Using a Connection String](#using-a-connection-string)
    - [Using Aspire Configuration](#using-aspire-configuration)
    - [Inline Configuration](#inline-configuration)
- [Requirements](#requirements)
- [How to contribute](#how-to-contribute)

# Getting Started

## Installation

Install the package via NuGet:

```sh
dotnet add package Atc.Aspire.Azure.Kusto
```

## Wire-Up

Register the Azure Data Explorer client in your Program.cs:

```csharp
builder.ConfigureAzureDataExplorer("kusto");
```

This will configure the integration using the ConnectionStrings:kusto entry in your application configuration.

## Usage

After registration, you can inject IKustoProcessor into your services:

For more information on consuming the IKustoProcessor, refer to the [Atc.Kusto documentation](https://github.com/atc-net/atc-kusto).

```csharp
public class MyService(IKustoProcessor kustoProcessor)
{
    public async Task ProcessQueryAsync()
    {
        var result = await processor.ExecuteQuery(
            new TodoQuery(),
            cancellationToken);
        Console.WriteLine(result);
    }
}
```

## Configuration

### Using a Connection String

Provide the Kusto connection string under ConnectionStrings in your configuration:

```json
{
    "ConnectionStrings": {
        "kusto": "https://your-cluster.kusto.windows.net"
    }
}
```

### Using Aspire Configuration

Alternatively, configure settings under the Aspire:Kusto:Client section:

```json
{
  "Aspire": {
    "Kusto": {
      "Client": {
        "HostAddress": "https://your-cluster.kusto.windows.net",
        "DatabaseName": "MyDatabase",
        "DisableHealthChecks": false,
        "HealthCheckTimeout": 4000
      }
    }
  }
}
```

### Inline Configuration

You can also configure settings inline when registering the service:

```csharp
builder.ConfigureAzureDataExplorer("kusto", settings =>
{
    settings.DatabaseName = "MyDatabase";
});
```

# Requirements

- [.NET 9 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/9.0)
- [.NET Aspire](https://learn.microsoft.com/en-us/dotnet/aspire/fundamentals/setup-tooling?tabs=windows&pivots=visual-studio#install-net-aspire)

# How to contribute

[Contribution Guidelines](https://atc-net.github.io/introduction/about-atc#how-to-contribute)

[Coding Guidelines](https://atc-net.github.io/introduction/about-atc#coding-guidelines)