using RabbitMQ.Client;

namespace Paperless.Infrastructure.Processing;

internal static class RabbitTransport
{
    internal const string Exchange = "paperless.processing";
    internal const string Queue = "paperless.document-preparation";
    internal const string DeadExchange = "paperless.processing.dead";
    internal const string DeadQueue = "paperless.document-preparation.dead";
    internal const string RoutingKey = "prepare";
    internal const string DeadRoutingKey = "dead";
    internal const string IndexExchange = "paperless.indexing";
    internal const string IndexQueue = "paperless.document-indexing";
    internal const string IndexDeadExchange = "paperless.indexing.dead";
    internal const string IndexDeadQueue = "paperless.document-indexing.dead";
    internal const string IndexRoutingKey = "index";

    internal static async Task<IConnection> ConnectAsync(ProcessingOptions options, string name, CancellationToken cancellationToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = options.HostName,
            Port = options.Port,
            UserName = options.UserName,
            Password = options.Password,
            VirtualHost = options.VirtualHost,
            AutomaticRecoveryEnabled = false,
            // Leave room for IPv4 fallback and channel setup inside the publisher's total deadline.
            RequestedConnectionTimeout = TimeSpan.FromSeconds(options.BrokerTimeoutSeconds / 3d),
            ContinuationTimeout = TimeSpan.FromSeconds(options.BrokerTimeoutSeconds),
            RequestedHeartbeat = TimeSpan.FromSeconds(30),
            Ssl = new SslOption { Enabled = options.UseSsl, ServerName = options.HostName }
        };
        return await factory.CreateConnectionAsync(name, cancellationToken);
    }

    internal static async Task DeclareAsync(IChannel channel, CancellationToken cancellationToken)
    {
        await channel.ExchangeDeclareAsync(DeadExchange, ExchangeType.Direct, durable: true, cancellationToken: cancellationToken);
        await channel.QueueDeclareAsync(DeadQueue, durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object?> { ["x-queue-type"] = "quorum", ["x-overflow"] = "reject-publish", ["x-max-length"] = 10000 },
            cancellationToken: cancellationToken);
        await channel.QueueBindAsync(DeadQueue, DeadExchange, DeadRoutingKey, cancellationToken: cancellationToken);
        await channel.ExchangeDeclareAsync(Exchange, ExchangeType.Direct, durable: true, cancellationToken: cancellationToken);
        await channel.QueueDeclareAsync(Queue, durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-queue-type"] = "quorum",
                ["x-dead-letter-exchange"] = DeadExchange,
                ["x-dead-letter-routing-key"] = DeadRoutingKey,
                ["x-dead-letter-strategy"] = "at-least-once",
                ["x-overflow"] = "reject-publish",
                ["x-delivery-limit"] = 20,
                ["x-max-length"] = 10000
            }, cancellationToken: cancellationToken);
        await channel.QueueBindAsync(Queue, Exchange, RoutingKey, cancellationToken: cancellationToken);
        await channel.ExchangeDeclareAsync(IndexDeadExchange, ExchangeType.Direct, durable: true, cancellationToken: cancellationToken);
        await channel.QueueDeclareAsync(IndexDeadQueue, durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object?> { ["x-queue-type"] = "quorum", ["x-overflow"] = "reject-publish", ["x-max-length"] = 10000 },
            cancellationToken: cancellationToken);
        await channel.QueueBindAsync(IndexDeadQueue, IndexDeadExchange, DeadRoutingKey, cancellationToken: cancellationToken);
        await channel.ExchangeDeclareAsync(IndexExchange, ExchangeType.Direct, durable: true, cancellationToken: cancellationToken);
        await channel.QueueDeclareAsync(IndexQueue, durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-queue-type"] = "quorum",
                ["x-dead-letter-exchange"] = IndexDeadExchange,
                ["x-dead-letter-routing-key"] = DeadRoutingKey,
                ["x-dead-letter-strategy"] = "at-least-once",
                ["x-overflow"] = "reject-publish",
                ["x-delivery-limit"] = 20,
                ["x-max-length"] = 10000
            }, cancellationToken: cancellationToken);
        await channel.QueueBindAsync(IndexQueue, IndexExchange, IndexRoutingKey, cancellationToken: cancellationToken);
    }
}
