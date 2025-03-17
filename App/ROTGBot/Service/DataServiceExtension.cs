using Microsoft.Extensions.DependencyInjection;

namespace ROTGBot.Service
{
    public static class DataServiceExtension
    {
        public static IServiceCollection AddDataServices(this IServiceCollection services)
        {
            //services.AddScoped<IUserDataService, UserDataService>();
            //services.AddScoped<IUserHistoryDataService, UserHistoryDataService>();            
            services.AddScoped<ITelegramMainService, TelegramMainService>();
            services.AddHostedService<TelegramHostedService>();
            return services;
        }


    }
}
