using System.Text.Json;
using Krosoft.Amqp.CLI.Models;

namespace Krosoft.Amqp.CLI.Helpers;

internal static class ProfileLoader
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    internal static async Task<(Profile? profile, string? error)> LoadAsync(string path)
    {
        if (!File.Exists(path))
        {
            return (null, $"Profil introuvable : {path}");
        }

        try
        {
            var json = await File.ReadAllTextAsync(path);
            var profile = JsonSerializer.Deserialize<Profile>(json, Options);
            return profile is null
                ? (null, "Profil invalide ou vide.")
                : (profile, null);
        }
        catch (Exception ex)
        {
            return (null, $"Erreur de lecture du profil : {ex.Message}");
        }
    }
}