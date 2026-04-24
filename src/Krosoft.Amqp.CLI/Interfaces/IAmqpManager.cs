namespace Krosoft.Amqp.CLI.Interfaces;

internal interface IAmqpManager
{
    Task<int> Queues();
  
    Task<int> Info(string profilePath);
    Task<int> Queues2();
    Task<int> Queues3();
}