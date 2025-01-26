# Introduction

This repository contains various host and client integration packages for .NET Aspire.

# Table of Content

- [Introduction](#introduction)
- [Table of Content](#table-of-content)
- [Hosting integrations](#hosting-integrations)
  - [Atc.Aspire.Hosting.Azure.Kusto](#atcaspirehostingazurekusto)
- [Client integrations](#client-integrations)
  - [Atc.Aspire.Azure.Kusto](#atcaspireazurekusto)
- [Requirements](#requirements)
- [How to contribute](#how-to-contribute)

# Hosting integrations

## Atc.Aspire.Hosting.Azure.Kusto

[![NuGet Version](https://img.shields.io/nuget/v/atc.aspire.azure.hosting.kusto.svg?logo=nuget&style=for-the-badge)](https://www.nuget.org/packages/atc.aspire.hosting.azure.kusto)

The `Atc.Aspire.Hosting.Azure.Kusto` package provides extension methods and resource definitions for the .NET Aspire AppHost to support running [Kusto Emulator](https://learn.microsoft.com/en-us/azure/data-explorer/kusto-emulator-overview) containers.

Read more in the package [README.md](./src/Atc.Aspire.Hosting.Azure.Kusto/README.md)

# Client integrations

## Atc.Aspire.Azure.Kusto
[![NuGet Version](https://img.shields.io/nuget/v/atc.aspire.azure.kusto.svg?logo=nuget&style=for-the-badge)](https://www.nuget.org/packages/atc.aspire.azure.kusto)

The `Atc.Aspire.Azure.Kusto` package wraps the [Atc.Kusto](https://github.com/atc-net/atc-kusto) package as an Aspire Client Integration.

Read more in the package [README.md](./src/Atc.Aspire.Azure.Kusto/README.md)

# Requirements

* [.NET 9 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/9.0)
* [.NET Aspire](https://learn.microsoft.com/en-us/dotnet/aspire/fundamentals/setup-tooling?tabs=windows&pivots=visual-studio#install-net-aspire)

# How to contribute

[Contribution Guidelines](https://atc-net.github.io/introduction/about-atc#how-to-contribute)

[Coding Guidelines](https://atc-net.github.io/introduction/about-atc#coding-guidelines)
