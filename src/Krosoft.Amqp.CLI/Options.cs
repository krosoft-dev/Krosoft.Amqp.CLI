using CommandLine;

namespace Krosoft.Amqp.CLI;

internal static class Options
{
    [Verb("git-pull", HelpText = "Exécuter un 'git pull' sur le dépôt.")]
    internal class GitPullOptions;

    [Verb("git-clean", HelpText = "Exécuter un 'git clean' sur le dépôt.")]
    internal class GitCleanOptions;

    [Verb("git", HelpText = "Exécuter une commande git.")]
    internal class RunOptions
    {
        [Option('c', "clean", Required = false, Default = false, HelpText = "Exécuter un 'git pull' sur le dépôt.")]
        public bool Clean { get; set; }

        [Option('p', "pull", Required = false, Default = false, HelpText = "Exécuter un 'git clean' sur le dépôt.")]
        public bool Pull { get; set; }
    }
}