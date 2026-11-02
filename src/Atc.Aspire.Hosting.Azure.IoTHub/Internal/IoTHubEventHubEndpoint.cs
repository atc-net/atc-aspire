namespace Atc.Aspire.Hosting.Azure.IoTHub.Internal;

/// <summary>
/// The Event Hub-compatible endpoint of an IoT Hub's built-in <c>events</c> endpoint.
/// IoT Hub doesn't expose it in the service connection string, but redirects an AMQP receiver to it.
/// </summary>
/// <param name="FullyQualifiedNamespace">The Event Hubs namespace host, e.g. <c>iothub-ns-myhub-123-abc.servicebus.windows.net</c>.</param>
/// <param name="EventHubName">The Event Hub-compatible name.</param>
internal sealed partial record IoTHubEventHubEndpoint(
    string FullyQualifiedNamespace,
    string EventHubName)
{
    private const int AmqpsPort = 5671;
    private const int WebSocketsPort = 443;
    private const string WebSocketsPath = "/$iothub/websocket";
    private const string WebSocketsSubProtocol = "AMQPWSB10";
    private const string RedirectHostNameKey = "hostname";
    private const string RedirectAddressKey = "address";

    private static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Builds the Event Hubs connection string for the endpoint, with the IoT Hub's shared access policy:
    /// IoT Hub accepts the same policy on its Event Hub-compatible endpoint.
    /// </summary>
    /// <param name="iotHub">The IoT Hub connection info.</param>
    /// <returns>The Event Hubs connection string.</returns>
    public string GetConnectionString(IoTHubConnectionInfo iotHub)
    {
        ArgumentNullException.ThrowIfNull(iotHub);

        return $"Endpoint=sb://{FullyQualifiedNamespace}/;SharedAccessKeyName={iotHub.SharedAccessKeyName};SharedAccessKey={iotHub.SharedAccessKey};EntityPath={EventHubName}";
    }

    /// <summary>
    /// Asks IoT Hub for its Event Hub-compatible endpoint: opens an AMQP receiver on the built-in endpoint,
    /// which IoT Hub refuses with a redirect to the Event Hub that holds the events.
    /// </summary>
    /// <param name="iotHub">The IoT Hub connection info.</param>
    /// <param name="options">Whether to connect over WebSockets.</param>
    /// <param name="timeProvider">The clock for the token expiry.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The endpoint.</returns>
    /// <exception cref="InvalidOperationException">IoT Hub refused the connection, didn't redirect, or didn't answer in time.</exception>
    public static async Task<IoTHubEventHubEndpoint> ResolveAsync(
        IoTHubConnectionInfo iotHub,
        IoTHubReaderOptions options,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(iotHub);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(timeProvider);

        var token = SharedAccessSignature.Create(
            $"{iotHub.HostName}/messages/events",
            iotHub.SharedAccessKeyName,
            iotHub.SharedAccessKey,
            timeProvider.GetUtcNow().Add(TokenLifetime));

        var settings = CreateSettings($"{iotHub.SharedAccessKeyName}@sas.root.{iotHub.HubName}", token);
        var transportSettings = CreateTransportSettings(iotHub.HostName, options.UseWebSockets);

        // A connection that stalls would leave the bridge starting forever.
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Timeout);

        AmqpConnection? connection = null;

        try
        {
            // The WebSocket transport needs a finite timeout, which the overload taking a cancellation token doesn't pass.
            var transport = await new AmqpTransportInitiator(settings, transportSettings)
                .ConnectTaskAsync(Timeout)
                .WaitAsync(timeout.Token);

            connection = new AmqpConnection(transport, settings, new AmqpConnectionSettings
            {
                ContainerId = Guid.NewGuid().ToString("N"),
                HostName = iotHub.HostName,
            });

            await connection.OpenAsync(timeout.Token);

            var session = connection.CreateSession(new AmqpSessionSettings());
            await session.OpenAsync(timeout.Token);

            var link = new ReceivingAmqpLink(new AmqpLinkSettings
            {
                LinkName = $"atc-aspire-redirect-{Guid.NewGuid():N}",
                Role = true,
                TotalLinkCredit = 1,
                AutoSendFlow = true,
                SettleType = SettleMode.SettleOnSend,
                Source = new Source { Address = $"amqps://{iotHub.HostName}/messages/events/$management" },
                Target = new Target { Address = connection.Settings.ContainerId },
            });

            link.AttachTo(session);
            await link.OpenAsync(timeout.Token);
        }
        catch (AmqpException ex) when (Equals(ex.Error.Condition, AmqpErrorCode.LinkRedirect))
        {
            return FromRedirect(
                ex.Error.Info[RedirectHostNameKey] as string,
                ex.Error.Info[RedirectAddressKey] as string);
        }
        catch (AmqpException ex)
        {
            throw new InvalidOperationException(
                $"IoT Hub '{iotHub.HostName}' refused the connection to its built-in events endpoint: {ex.Error.Description ?? ex.Message} " +
                "Check the connection string's key and that its policy has service connect permission.",
                ex);
        }
        catch (Exception ex) when (ex is TimeoutException || (ex is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            throw new InvalidOperationException(
                $"IoT Hub '{iotHub.HostName}' didn't answer over {options.TransportDescription} within {Timeout.TotalSeconds:0} seconds.{GetWebSocketsHint(options)}",
                ex);
        }
        catch (Exception ex) when (ex is SocketException or IOException or WebSocketException or AuthenticationException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException(
                $"Could not connect to IoT Hub '{iotHub.HostName}' over {options.TransportDescription}: {ex.Message}{GetWebSocketsHint(options)}",
                ex);
        }
        finally
        {
            connection?.SafeClose();
        }

        throw new InvalidOperationException(
            $"IoT Hub '{iotHub.HostName}' didn't redirect to its Event Hub-compatible endpoint.");
    }

    /// <summary>
    /// Reads the endpoint from the redirect IoT Hub answers with.
    /// </summary>
    /// <param name="hostName">The redirect host name, e.g. <c>iothub-ns-myhub-123-abc.servicebus.windows.net</c>.</param>
    /// <param name="address">The redirect address, e.g. <c>amqps://iothub-ns-myhub-123-abc.servicebus.windows.net:5671/myhub/$management</c>.</param>
    /// <returns>The endpoint.</returns>
    /// <exception cref="InvalidOperationException">The redirect has no host name, or the address has no Event Hub name.</exception>
    public static IoTHubEventHubEndpoint FromRedirect(
        string? hostName,
        string? address)
    {
        if (string.IsNullOrWhiteSpace(hostName))
        {
            throw new InvalidOperationException("IoT Hub redirected to its Event Hub-compatible endpoint without a host name.");
        }

        var match = address is null
            ? null
            : RedirectAddress().Match(address);

        if (match is null || !match.Success)
        {
            throw new InvalidOperationException(
                $"IoT Hub redirected to '{address}', which has no Event Hub name.");
        }

        return new IoTHubEventHubEndpoint(hostName, match.Groups["eventHubName"].Value);
    }

    /// <summary>
    /// Creates the transport to IoT Hub: TLS on port 5671, or a WebSocket on port 443 at the path IoT Hub serves AMQP on.
    /// </summary>
    /// <param name="hostName">The IoT Hub host name.</param>
    /// <param name="useWebSockets">Whether to connect over WebSockets.</param>
    /// <returns>The transport settings.</returns>
    internal static TransportSettings CreateTransportSettings(
        string hostName,
        bool useWebSockets)
    {
        if (useWebSockets)
        {
            // WebSocketTransportSettings.Proxy is left unset on purpose: the WebSocket then uses the system's proxy settings
            // (the HTTPS_PROXY environment variable, otherwise Windows' proxy settings).
            return new WebSocketTransportSettings
            {
                Uri = new UriBuilder(Uri.UriSchemeWss, hostName, WebSocketsPort, WebSocketsPath).Uri,
                SubProtocol = WebSocketsSubProtocol,
            };
        }

        return new TlsTransportSettings(new TcpTransportSettings { Host = hostName, Port = AmqpsPort })
        {
            TargetHost = hostName,
        };
    }

    private static string GetWebSocketsHint(IoTHubReaderOptions options)
        => options.UseWebSockets
            ? string.Empty
            : $" If the network blocks port {AmqpsPort}, connect over WebSockets with WithWebSockets().";

    private static AmqpSettings CreateSettings(
        string userName,
        string token)
    {
        var version = new AmqpVersion(1, 0, 0);

        var sasl = new SaslTransportProvider();
        sasl.Versions.Add(version);
        sasl.AddHandler(new SaslPlainHandler { AuthenticationIdentity = userName, Password = token });

        var amqp = new AmqpTransportProvider();
        amqp.Versions.Add(version);

        var settings = new AmqpSettings();
        settings.TransportProviders.Add(sasl);
        settings.TransportProviders.Add(amqp);
        return settings;
    }

    // The Event Hub name is the path segment before $management, after the port.
    [GeneratedRegex(@":\d+/(?<eventHubName>[^/]+)/\$management$", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex RedirectAddress();
}