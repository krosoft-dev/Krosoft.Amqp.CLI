namespace Krosoft.Amqp.CLI.Interfaces;

internal interface IGitManager
{
    Task<int> Pull();
    Task<int> Clean();
}