using Microsoft.EntityFrameworkCore;

namespace ROTGBot
{
    public static class CustomExtensionMethods
    {
        public static IConfigurationBuilder AddDbConfiguration(this IConfigurationBuilder builder)
            => builder.AddConfigDbProvider(options => options.UseNpgsql(builder.GetConnectionString()));
                
        private static string? GetConnectionString(this IConfigurationBuilder builder)
            => builder.Build().GetConnectionString("MainConnection");

        public static IConfigurationBuilder AddConfigDbProvider(this IConfigurationBuilder configuration, Action<DbContextOptionsBuilder> setup)
            => configuration.Add(new ConfigDbSource(setup));
    }
}
