namespace Atc.Aspire.Hosting.Azure.IoTEdge.Internal;

/// <summary>
/// Validates device and module ids against the IoT Hub identity registry rules: up to 128 ASCII 7-bit alphanumeric
/// characters plus <c>- . % _ * ? ! ( ) , : = @ $ '</c>.
/// </summary>
internal static partial class IoTHubIdentifier
{
    private const int MaxLength = 128;

    public static bool IsValid(string? id)
        => !string.IsNullOrEmpty(id) &&
           id.Length <= MaxLength &&
           AllowedCharacters().IsMatch(id);

    public static void EnsureValidDeviceId(
        string deviceId,
        string paramName)
    {
        if (!IsValid(deviceId))
        {
            throw new ArgumentException(
                $"'{deviceId}' is not a valid IoT Hub device id. It must be 1-128 characters: letters, digits and - . % _ * ? ! ( ) , : = @ $ '",
                paramName);
        }
    }

    public static void EnsureValidModuleId(
        string moduleId,
        string paramName)
    {
        if (!IsValid(moduleId))
        {
            throw new ArgumentException(
                $"'{moduleId}' is not a valid IoT Hub module id. It must be 1-128 characters: letters, digits and - . % _ * ? ! ( ) , : = @ $ '",
                paramName);
        }

        // $edgeAgent and $edgeHub are the IoT Edge runtime modules.
        if (moduleId.StartsWith('$'))
        {
            throw new ArgumentException(
                $"'{moduleId}' is reserved for the IoT Edge runtime. Module ids starting with '$' can't be used for modules.",
                paramName);
        }
    }

    [GeneratedRegex(@"^[A-Za-z0-9\-.%_*?!(),:=@$']+$", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex AllowedCharacters();
}