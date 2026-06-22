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

    [Verb("messages", HelpText = "Liste les messages présents dans une file AMQP.")]
    internal class MessagesOptions
    {
        [Option('p', "profile", Required = true, HelpText = "Chemin vers le fichier de profil JSON.")]
        public string Profile { get; set; } = string.Empty;

        [Option('q', "queue", Required = true, HelpText = "Nom de la file à parcourir.")]
        public string Queue { get; set; } = string.Empty;
    }

    [Verb("message", HelpText = "Télécharge localement un message d'une file AMQP par son messageID.")]
    internal class MessageOptions
    {
        [Option('p', "profile", Required = true, HelpText = "Chemin vers le fichier de profil JSON.")]
        public string Profile { get; set; } = string.Empty;

        [Option('q', "queue", Required = true, HelpText = "Nom de la file à parcourir.")]
        public string Queue { get; set; } = string.Empty;

        [Option('i', "id", Required = true, HelpText = "messageID interne du message à télécharger.")]
        public string Id { get; set; } = string.Empty;

        [Option('o', "out", Required = false, HelpText = "Chemin du fichier de sortie. Par défaut : message_<id>.json")]
        public string? Out { get; set; }
    }
}