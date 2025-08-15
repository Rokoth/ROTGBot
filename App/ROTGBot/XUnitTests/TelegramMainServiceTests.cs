using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Microsoft.Extensions.Configuration;
using Common;
using ROTGBot.Service;
using Microsoft.EntityFrameworkCore;
using ROTGBot.Db.Interface;
using ROTGBot.Db.Repository;
using Microsoft.Extensions.Logging;
using Npgsql;
using ROTGBot.Db.Model;
using Moq;
using Telegram.BotAPI;
using Telegram.BotAPI.GettingUpdates;

namespace XUnitTests
{    
    public class TelegramMainServiceUnitTests 
    {
        private IConfiguration configuration;

        public TelegramMainServiceUnitTests()
        {
            ConfigurationBuilder builder = new ConfigurationBuilder();
            builder.AddJsonFile("appsettings.json");
            configuration = builder.Build();
        }

        [Fact]
        public async Task Execute_No_Updates_Async()
        {
            var handlerService = new Mock<ITelegramMessageHandler>();
            var wrapperService = new Mock<ITelegramBotWrapper>();
            handlerService.Setup(s => s.HandleUpdates(It.IsAny<IEnumerable<Update>>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            wrapperService.Setup(s => s.GetUpdatesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync([]);

            var tgMainService = new TelegramMainService(handlerService.Object, wrapperService.Object);

            var result = await tgMainService.Execute(1);

            Assert.Equal(1, result);
        }
    }

    public class ButtonsDataServiceUnitTests
    {
        private IConfiguration configuration;

        public ButtonsDataServiceUnitTests()
        {
            ConfigurationBuilder builder = new ConfigurationBuilder();
            builder.AddJsonFile("appsettings.json");
            configuration = builder.Build();
        }

        [Fact]
        public async Task AddNewButton_NoButtons_Async()
        {
            var _repoMock = new Mock<IRepository<NewsButton>>();

            _repoMock.Setup(s => s.GetAsync(It.IsAny<Filter<NewsButton>>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new List<NewsButton>()));

            _repoMock.Setup(s => s.AddAsync(It.IsAny<NewsButton>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new NewsButton()));

            var buttonsService = new ButtonsDataService(_repoMock.Object);

            var result = await buttonsService.AddNewButton(1, 1, "chat", "chat", new CancellationToken());

            Assert.True(result);
        }
    }

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

            serviceCollection.AddDbContext<ROTGBot.Db.Context.DbPgContext>(opt => opt.UseNpgsql(ConnectionString));
            serviceCollection.AddScoped<IRepository<ROTGBot.Db.Model.User>, Repository<ROTGBot.Db.Model.User>>();
            serviceCollection.AddScoped<IRepository<ROTGBot.Db.Model.Role>, Repository<ROTGBot.Db.Model.Role>>();
            serviceCollection.AddScoped<IRepository<ROTGBot.Db.Model.UserRole>, Repository<ROTGBot.Db.Model.UserRole>>();
            serviceCollection.AddScoped<IRepository<ROTGBot.Db.Model.News>, Repository<ROTGBot.Db.Model.News>>();
            serviceCollection.AddScoped<IRepository<ROTGBot.Db.Model.NewsMessage>, Repository<ROTGBot.Db.Model.NewsMessage>>();
            serviceCollection.AddScoped<IRepository<ROTGBot.Db.Model.Groups>, Repository<ROTGBot.Db.Model.Groups>>();
            serviceCollection.AddScoped<IRepository<ROTGBot.Db.Model.NewsButton>, Repository<ROTGBot.Db.Model.NewsButton>>();
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