using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SushiMiau.Shared.Cassandra;

public static class CassandraServiceCollectionExtensions
{
    public static IServiceCollection AddSushiMiauCassandra(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<CassandraOptions>(configuration.GetSection("Cassandra"));
        services.AddSingleton<CassandraSessionFactory>();
        services.AddSingleton(provider => provider.GetRequiredService<CassandraSessionFactory>().Session);

        return services;
    }
}
