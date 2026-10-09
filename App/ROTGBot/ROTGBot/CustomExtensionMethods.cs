using Microsoft.EntityFrameworkCore;

namespace ROTGBot
{
    public static class CustomExtensionMethods
    {
        public static IConfigurationBuilder AddDbConfiguration(this IConfigurationBuilder builder, string connectionString)
            => builder.AddConfigDbProvider(options => options.UseNpgsql(connectionString));
                
        public static IConfigurationBuilder AddConfigDbProvider(this IConfigurationBuilder configuration, Action<DbContextOptionsBuilder> setup)
            => configuration.Add(new ConfigDbSource(setup));
    }
}
