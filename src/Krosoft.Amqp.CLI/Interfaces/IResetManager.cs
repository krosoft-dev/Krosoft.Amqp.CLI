namespace Krosoft.Amqp.CLI.Interfaces;

internal interface IResetManager
{
    Task<int> Reset(string profilePath);
}
