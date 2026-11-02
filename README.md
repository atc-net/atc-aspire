# Introduction

This repository contains various host and client integration packages for [Aspire](https://aspire.dev).

# Table of Content

- [Introduction](#introduction)
- [Table of Content](#table-of-content)
- [Hosting integrations](#hosting-integrations)
  - [Atc.Aspire.Hosting.Azure.Kusto](#atcaspirehostingazurekusto)
  - [Atc.Aspire.Hosting.Azure.IoTEdge](#atcaspirehostingazureiotedge)
  - [Atc.Aspire.Hosting.Azure.IoTHub](#atcaspirehostingazureiothub)
- [Client integrations](#client-integrations)
  - [Atc.Aspire.Azure.Kusto](#atcaspireazurekusto)
- [Requirements](#requirements)
- [How to contribute](#how-to-contribute)

# Hosting integrations

## Atc.Aspire.Hosting.Azure.Kusto

[![NuGet Version](https://img.shields.io/nuget/v/atc.aspire.hosting.azure.kusto.svg?logo=nuget&style=for-the-badge)](https://www.nuget.org/packages/atc.aspire.hosting.azure.kusto)

The `Atc.Aspire.Hosting.Azure.Kusto` package provides extension methods and resource definitions for the Aspire AppHost to support running [Kusto Emulator](https://learn.microsoft.com/en-us/azure/data-explorer/kusto-emulator-overview) containers.

Read more in the package [README.md](./src/Atc.Aspire.Hosting.Azure.Kusto/README.md)

## Atc.Aspire.Hosting.Azure.IoTEdge

[![NuGet Version](https://img.shields.io/nuget/v/atc.aspire.hosting.azure.iotedge.svg?logo=nuget&style=for-the-badge)](https://www.nuget.org/packages/atc.aspire.hosting.azure.iotedge)

The `Atc.Aspire.Hosting.Azure.IoTEdge` package runs Azure IoT Edge modules in the Aspire AppHost against a real Azure IoT Hub, either through a local edgeHub container or directly. It provisions the device and module identities, applies the deployment manifest's routes and desired properties, and adds dashboard commands for direct methods, desired properties and module twins.

Read more in the package [README.md](./src/Atc.Aspire.Hosting.Azure.IoTEdge/README.md)

## Atc.Aspire.Hosting.Azure.IoTHub

[![NuGet Version](https://img.shields.io/nuget/v/atc.aspire.hosting.azure.iothub.svg?logo=nuget&style=for-the-badge)](https://www.nuget.org/packages/atc.aspire.hosting.azure.iothub)

The `Atc.Aspire.Hosting.Azure.IoTHub` package forwards the device lifecycle events, twin change events and telemetry of a real Azure IoT Hub to Event Hubs in the Aspire AppHost, e.g. the Event Hubs emulator, so the AppHost's services can read them locally.

Read more in the package [README.md](./src/Atc.Aspire.Hosting.Azure.IoTHub/README.md)

# Client integrations

## Atc.Aspire.Azure.Kusto
[![NuGet Version](https://img.shields.io/nuget/v/atc.aspire.azure.kusto.svg?logo=nuget&style=for-the-badge)](https://www.nuget.org/packages/atc.aspire.azure.kusto)

The `Atc.Aspire.Azure.Kusto` package wraps the [Atc.Kusto](https://github.com/atc-net/atc-kusto) package as an Aspire Client Integration.

Read more in the package [README.md](./src/Atc.Aspire.Azure.Kusto/README.md)

# Requirements

* [.NET 10 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)
* [Aspire 13.6 or later](https://aspire.dev/get-started/prerequisites/)

# How to contribute

[Contribution Guidelines](https://atc-net.github.io/introduction/about-atc#how-to-contribute)

[Coding Guidelines](https://atc-net.github.io/introduction/about-atc#coding-guidelines)
