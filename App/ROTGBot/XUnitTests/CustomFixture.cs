using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Microsoft.Extensions.Configuration;
using Common;
using ROTGBot.Service;
using Microsoft.EntityFrameworkCore;
using ROTGBot.DB.Interface;
using ROTGBot.DB.Repository;

namespace XUnitTests
{
    public class CustomFixture : IDisposable
    {
        public string ConnectionString { get; private set; }
        public string RootConnectionString { get; private set; }
        public string DatabaseName { get; private set; }
        public ServiceProvider ServiceProvider { get; private set; }

        public CustomFixture()
        {
            Log.Logger = new LoggerConfiguration()
             .MinimumLevel.Verbose()
             .WriteTo.File(Path.Combine(Directory.GetCurrentDirectory(), "test-log.txt"))
             .CreateLogger();

            var serviceCollection = new ServiceCollection();
            ConfigurationBuilder builder = new ConfigurationBuilder();
            builder.AddJsonFile("appsettings.json");
            var config = builder.Build();

            serviceCollection.Configure<CommonOptions>(config);
            serviceCollection.AddLogging(configure => configure.AddSerilog());

            serviceCollection.AddDbContext<ROTGBot.DB.Context.DbPgContext>(opt => opt.UseNpgsql(ConnectionString));
            serviceCollection.AddScoped<IRepository<ROTGBot.DB.Model.User>, Repository<ROTGBot.DB.Model.User>>();
            serviceCollection.AddScoped<IRepository<ROTGBot.DB.Model.Role>, Repository<ROTGBot.DB.Model.Role>>();
            serviceCollection.AddScoped<IRepository<ROTGBot.DB.Model.UserRole>, Repository<ROTGBot.DB.Model.UserRole>>();
            serviceCollection.AddScoped<IRepository<ROTGBot.DB.Model.News>, Repository<ROTGBot.DB.Model.News>>();
            serviceCollection.AddScoped<IRepository<ROTGBot.DB.Model.NewsMessage>, Repository<ROTGBot.DB.Model.NewsMessage>>();
            serviceCollection.AddScoped<IRepository<ROTGBot.DB.Model.Groups>, Repository<ROTGBot.DB.Model.Groups>>();
            serviceCollection.AddScoped<IRepository<ROTGBot.DB.Model.NewsButton>, Repository<ROTGBot.DB.Model.NewsButton>>();
            serviceCollection.AddDataServices();

            ServiceProvider = serviceCollection.BuildServiceProvider();
        }

        public void Dispose()
        {
            
        }

        //public User CreateUser(string nameMask, string descriptionMask, string loginMask, string passwordMask, Guid formulaId)
        //{

        //    var id = Guid.NewGuid();
        //    var user = new User()
        //    {
        //        Name = string.Format(nameMask, id),
        //        Id = id,
        //        Description = string.Format(descriptionMask, id),
        //        IsDeleted = false,
        //        ChatId = 1,
        //        IsNotify = true,
        //        TGId = 1,
        //        TGLogin = "tgtestuser"
        //    };
        //    return user;
        //}
    }
}