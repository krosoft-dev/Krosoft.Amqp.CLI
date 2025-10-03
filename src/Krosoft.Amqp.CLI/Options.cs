using CommandLine;

namespace Krosoft.Amqp.CLI;

internal static class Options
{
    [Verb("info", HelpText = "Exécuter une commande git.")]
    internal class InfoOptions
    {
    }

    [Verb("queues", HelpText = "Exécuter une commande git.")]
    internal class QueuesOptions
    {
        [Option('l', "list", Required = false, Default = false, HelpText = "Exécuter un 'git pull' sur le dépôt.")]
        public bool List { get; set; }

  
    }
}