using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Configuration;

namespace ROTGBot
{
    public static class CustomExtensionMethods
    {
        public static IConfigurationBuilder AddDbConfiguration(this IConfigurationBuilder builder)
        {            
            var connectionString = builder.Build().GetConnectionString("MainConnection") ?? throw new ArgumentNullException("connectionString");
            return builder.AddConfigDbProvider(options => options.UseNpgsql(connectionString));            
        }

        public static IConfigurationBuilder AddConfigDbProvider(this IConfigurationBuilder configuration, Action<DbContextOptionsBuilder> setup)
        {
            return configuration.Add(new ConfigDbSource(setup));
        }
    }
}
