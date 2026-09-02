# Introduction

[![NuGet Version](https://img.shields.io/nuget/v/atc.aspire.hosting.azure.kusto.svg?logo=nuget&style=for-the-badge)](https://www.nuget.org/packages/atc.aspire.hosting.azure.kusto)

The `Atc.Aspire.Hosting.Azure.Kusto` package provides an Aspire Hosting Integration for running Azure Data Explorer ([Kusto Emulator](https://learn.microsoft.com/en-us/azure/data-explorer/kusto-emulator-overview)) containers within a .NET Aspire distributed application. It enables easy setup of Kusto instances with databases, creation scripts, health checks, connection string handling, dashboard commands, and default configurations.

# Table of Content

- [Introduction](#introduction)
- [Table of Content](#table-of-content)
- [Getting Started](#getting-started)
  - [Installation](#installation)
  - [Wire-Up](#wire-up)
  - [Databases](#databases)
    - [Creation Scripts](#creation-scripts)
  - [Configuration](#configuration)
    - [Using a Custom Port](#using-a-custom-port)
    - [Configuring Data Volumes](#configuring-data-volumes)
  - [Connection Properties](#connection-properties)
  - [Dashboard Commands](#dashboard-commands)
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

This will configure the emulator with a default HTTP port and a pinned emulator image tag, and include it in your distributed application.

If your service needs access to the emulator, reference it in your project:

```csharp
var myService = builder.AddProject<Projects.MyService>().WithReference(kusto);
```

Your MyService project can now resolve the Kusto connection string automatically.

## Databases

Add one or more read-write databases to the emulator. Each database is created automatically once the emulator becomes healthy and is exposed as its own resource with its own connection string (which includes an `Initial Catalog`), so it can be referenced directly by a consuming project:

```csharp
var kusto = builder.AddKustainer();

var database = kusto.AddDatabase("kusto", "NetDefaultDB");

builder.AddProject<Projects.MyService>("apiservice")
    .WithReference(database)
    .WaitFor(database);
```

The second `databaseName` argument is optional and defaults to the resource name. `KustoContainerResource.DefaultDbName` (`NetDefaultDB`) is a convenient default.

### Creation Scripts

Use `WithCreationScript` to run a KQL script against the database once it is available — for example to create tables and seed data. Prefer idempotent control commands such as `.set-or-replace` so repeated runs (e.g. with a persisted data volume) stay consistent:

```csharp
var database = kusto
    .AddDatabase("kusto", KustoContainerResource.DefaultDbName)
    .WithCreationScript(
        """
        .set-or-replace Todo <|
            datatable(Id: int, Title: string, Description: string, Status: string, Created: datetime, Priority: string, Closed: datetime)
            [
                1, "Watch Netflix", "Watch the new show", "Pending", datetime(2025-01-28T10:00:00Z), "Low", datetime(null),
                2, "Make food", "Try out the new dish from the Netflix show", "Pending", datetime(2025-01-27T15:30:00Z), "Medium", datetime(null),
                3, "Coding", "Code up the new feature in atc-aspire kusto package", "Ended", datetime(2025-01-26T09:15:00Z), "High", datetime(2025-01-27T12:00:00Z)
            ]
        """);
```

The database itself is always created first; the creation script then runs against it, so the script only needs to define tables, functions, or data.

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

## Connection Properties

Connection information is exposed as structured properties on the Aspire dashboard. The emulator resource publishes its cluster `Uri`, and each database additionally publishes its `DatabaseName`, making it easy to inspect endpoints from the dashboard.

## Dashboard Commands

The emulator resource adds commands to the Aspire dashboard for opening the current cluster directly:

- **Open in Kusto Explorer (Desktop)** — launches the [Kusto.Explorer](https://learn.microsoft.com/en-us/azure/data-explorer/kusto/tools/kusto-explorer) desktop client (Windows only).
- **Open in Kusto Explorer (Web)** — opens the [Azure Data Explorer web UI](https://dataexplorer.azure.com).

## HealthChecks

The integration adds two-tier health checks automatically: a cluster-level check on the emulator resource and a database-level check on each added database. A built-in resilience pipeline retries transient failures while databases and scripts are being provisioned.

# Requirements

- [.NET 9 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/9.0)
- [.NET Aspire](https://learn.microsoft.com/en-us/dotnet/aspire/fundamentals/setup-tooling?tabs=windows&pivots=visual-studio#install-net-aspire)

# How to contribute

[Contribution Guidelines](https://atc-net.github.io/introduction/about-atc#how-to-contribute)

[Coding Guidelines](https://atc-net.github.io/introduction/about-atc#coding-guidelines)
