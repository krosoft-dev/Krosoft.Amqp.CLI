using CommandLine;

namespace Krosoft.Amqp.CLI;

internal static class Options
{
    [Verb("info", HelpText = "Affiche les informations du broker AMQP.")]
    internal class InfoOptions
    {
        [Option('p', "profile", Required = true, HelpText = "Chemin vers le fichier de profil JSON.")]
        public string Profile { get; set; } = string.Empty;
    }

    [Verb("queues", HelpText = "Exécuter une commande git.")]
    internal class QueuesOptions
    {
        [Option('l', "list", Required = false, Default = false, HelpText = "Exécuter un 'git pull' sur le dépôt.")]
        public bool List { get; set; }
    }

    [Verb("reset", HelpText = "Arrête les containers, purge les files AMQP, et redémarre les containers.")]
    internal class ResetOptions
    {
        [Option('p', "profile", Required = true, HelpText = "Chemin vers le fichier de profil JSON.")]
        public string Profile { get; set; } = string.Empty;
    }
}