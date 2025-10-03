namespace Krosoft.Amqp.CLI.Interfaces;

internal interface IAmqpManager
{
    Task<int> Queues();
  
    Task<int> Info();
}