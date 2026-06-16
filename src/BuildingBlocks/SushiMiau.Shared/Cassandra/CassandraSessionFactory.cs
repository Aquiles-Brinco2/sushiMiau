using Cassandra;
using Microsoft.Extensions.Options;

namespace SushiMiau.Shared.Cassandra;

public sealed class CassandraSessionFactory : IDisposable
{
    private readonly CassandraOptions _options;
    private readonly Lazy<ISession> _session;
    private ICluster? _cluster;

    public CassandraSessionFactory(IOptions<CassandraOptions> options)
    {
        _options = options.Value;
        _session = new Lazy<ISession>(CreateSession);
    }

    public ISession Session => _session.Value;

    private ISession CreateSession()
    {
        Exception? lastError = null;

        for (var attempt = 1; attempt <= _options.StartupRetries; attempt++)
        {
            try
            {
                var builder = Cluster.Builder()
                    .AddContactPoints(_options.ContactPoints)
                    .WithPort(_options.Port);

                if (!string.IsNullOrWhiteSpace(_options.Username))
                {
                    builder = builder.WithCredentials(_options.Username, _options.Password ?? string.Empty);
                }

                _cluster = builder.Build();
                var session = _cluster.Connect();
                session.Execute(
                    $"CREATE KEYSPACE IF NOT EXISTS {_options.Keyspace} " +
                    "WITH replication = { 'class': 'SimpleStrategy', 'replication_factor': 1 }");
                session.ChangeKeyspace(_options.Keyspace);

                return session;
            }
            catch (Exception ex)
            {
                lastError = ex;
                Thread.Sleep(TimeSpan.FromSeconds(_options.StartupRetryDelaySeconds));
            }
        }

        throw new InvalidOperationException("No se pudo conectar a Cassandra.", lastError);
    }

    public void Dispose()
    {
        if (_session.IsValueCreated)
        {
            _session.Value.Dispose();
        }

        _cluster?.Dispose();
    }
}
