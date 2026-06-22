namespace Krosoft.Amqp.CLI.Interfaces;

internal interface IAmqpManager
{
    Task<int> Queues();
  
    Task<int> Info(string profilePath);
    Task<int> Queues2();
    Task<int> Queues3();
    Task<int> DownloadMessage(string profilePath, string queue, string messageId, string? outPath);
    Task<int> ListMessages(string profilePath, string queue);
}