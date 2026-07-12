using CommandLine;

namespace Krosoft.Amqp.CLI;

internal static class Program
{
    private static async Task<int> Main(params string[] args)
    {
        PrintBanner();
        return await Parser.Default.ParseArguments<Options.InfoOptions,
                               Options.QueuesOptions,
                               Options.ResetOptions,
                               Options.MessageOptions,
                               Options.MessagesOptions>(args)
                           .MapResult(
                                      (Options.InfoOptions opts) => ProgramAmqp.Info(opts),
                                      (Options.QueuesOptions opts) => ProgramAmqp.Queues(opts),
                                      (Options.ResetOptions opts) => ProgramAmqp.Reset(opts),
                                      (Options.MessageOptions opts) => ProgramAmqp.Message(opts),
                                      (Options.MessagesOptions opts) => ProgramAmqp.Messages(opts),
                                      _ => Task.FromResult(-1));
    }

    private static void PrintBanner()
    {
        const string banner = """

                                _  __                     __ _   
                               | |/ /                    / _| |  
                               | ' / _ __ ___  ___  ___ | |_| |_ 
                               |  < | '__/ _ \/ __|/ _ \|  _| __|
                               | . \| | | (_) \__ \ (_) | | | |_ 
                               |_|\_\_|  \___/|___/\___/|_|  \__| 
                               
                               AMQP CLI Tool

                              """;
        Console.WriteLine(banner);
    }
}