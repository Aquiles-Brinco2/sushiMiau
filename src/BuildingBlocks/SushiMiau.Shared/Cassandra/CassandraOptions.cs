namespace SushiMiau.Shared.Cassandra;

public sealed class CassandraOptions
{
    public string[] ContactPoints { get; set; } = ["localhost"];
    public int Port { get; set; } = 9042;
    public string Keyspace { get; set; } = "sushi_miau";
    public string? Username { get; set; }
    public string? Password { get; set; }
    public int StartupRetries { get; set; } = 20;
    public int StartupRetryDelaySeconds { get; set; } = 3;
}
