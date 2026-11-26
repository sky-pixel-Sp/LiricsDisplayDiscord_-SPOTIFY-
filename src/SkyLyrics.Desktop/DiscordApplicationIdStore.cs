using System.Globalization;
using System.IO;

namespace SkyLyrics.Desktop;

/// <summary>
/// Stores only the public Discord Application ID in the current user's local profile.
/// No Discord token, password, or OAuth credential is stored.
/// </summary>
public static class DiscordApplicationIdStore
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SkyLyrics",
        "discord-app-id.txt");

    public static string? Load()
    {
        try
        {
            if (!File.Exists(FilePath))
                return null;

            var value = File.ReadAllText(FilePath).Trim();
            return IsValidApplicationId(value) ? value : null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    public static void Save(string applicationId)
    {
        if (!IsValidApplicationId(applicationId))
            throw new ArgumentException("A valid numeric Discord Application ID is required.", nameof(applicationId));

        var directory = Path.GetDirectoryName(FilePath)
            ?? throw new InvalidOperationException("Could not resolve the SkyLyrics settings directory.");

        Directory.CreateDirectory(directory);
        File.WriteAllText(FilePath, applicationId.Trim());
    }

    public static bool IsValidApplicationId(string? value) =>
        value is { Length: >= 16 and <= 22 } &&
        ulong.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out _);
}
