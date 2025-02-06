# Introduction

[![NuGet Version](https://img.shields.io/nuget/v/atc.aspire.azure.hosting.kusto.svg?logo=nuget&style=for-the-badge)](https://www.nuget.org/packages/atc.aspire.hosting.azure.kusto)

The `Atc.Aspire.Hosting.Azure.Kusto` package provides an Aspire Hosting Integration for running Azure Data Explorer ([Kusto Emulator](https://learn.microsoft.com/en-us/azure/data-explorer/kusto-emulator-overview)) containers within a .NET Aspire distributed application. It enables easy setup of Kusto instances with health checks, connection string handling, and default configurations.

# Table of Content

- [Introduction](#introduction)
- [Table of Content](#table-of-content)
- [Getting Started](#getting-started)
  - [Installation](#installation)
  - [Wire-Up](#wire-up)
  - [Configuration](#configuration)
    - [Using a Custom Port](#using-a-custom-port)
    - [Configuring Data Volumes](#configuring-data-volumes)
  - [HealthChecks](#healthchecks)
- [Requirements](#requirements)
- [How to contribute](#how-to-contribute)

# Getting Started

## Installation

Install the package via NuGet:

```sh
dotnet add package Atc.Aspire.Hosting.Azure.Kusto
```

## Wire-Up

Register a Kusto emulator container in your AppHost's Program.cs:

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var kusto = builder.AddKustainer("kusto-emulator");

builder.Build().Run();
```

This will configure the emulator with a default HTTP port and include it in your distributed application.

If your service needs access to the emulator, reference it in your project:

```csharp
var myService = builder.AddProject<Projects.MyService>().WithReference(kusto);
```

Your MyService project can now resolve the Kusto connection string automatically.

## Configuration

### Using a Custom Port

By default, the emulator runs on port 8080. You can override this by specifying a custom port:

```csharp
var kusto = builder.AddKustainer("kusto-emulator", httpPort: 9090);
```

### Configuring Data Volumes

To persist data across container restarts, you can attach a named volume:

```csharp
kusto.WithDataVolume();
```

Alternatively, specify a custom volume name:

```csharp
kusto.WithDataVolume("my-kusto-data");
```

## HealthChecks

The emulator includes built-in health checks. These are automatically added but can be configured if needed:

```csharp
builder.Services.AddHealthChecks()
    .AddCheck<KustoHealthCheck>("kusto_check");
```

# Requirements

- [.NET 9 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/9.0)
- [.NET Aspire](https://learn.microsoft.com/en-us/dotnet/aspire/fundamentals/setup-tooling?tabs=windows&pivots=visual-studio#install-net-aspire)

# How to contribute

[Contribution Guidelines](https://atc-net.github.io/introduction/about-atc#how-to-contribute)

[Coding Guidelines](https://atc-net.github.io/introduction/about-atc#coding-guidelines)
