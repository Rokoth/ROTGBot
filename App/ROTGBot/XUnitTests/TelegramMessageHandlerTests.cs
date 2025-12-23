using Microsoft.Extensions.Configuration;
using ROTGBot.Service;
using Moq;
using Telegram.BotAPI.GettingUpdates;
using Microsoft.Extensions.Logging;

namespace XUnitTests
{
    public class TelegramMessageHandlerTests
    {
        private IConfiguration configuration;

        public TelegramMessageHandlerTests()
        {
            ConfigurationBuilder builder = new ConfigurationBuilder();
            builder.AddJsonFile("appsettings.json");
            configuration = builder.Build();
        }

        
        /// <summary>
        /// 0.0.22.2.5
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task HandleUpdates_Success_Async()
        {            
            var wrapperService = new Mock<ITelegramBotWrapper>();
            var groupsDataService = new Mock<IGroupsDataService>();
            var userDataService = new Mock<IUserDataService>();
            var newsDataService = new Mock<INewsDataService>();
            var buttonDataService = new Mock<IButtonsDataService>();
            var configuration = new Mock<IConfiguration>();
            var logger = new Mock<ILogger<TelegramMessageHandler>>();

            wrapperService.Setup(s => s.GetUpdatesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => [
                    new Update()
                    {
                        UpdateId = 4
                    }
                ]);

            var tgMainService = new TelegramMessageHandler(logger.Object, groupsDataService.Object, userDataService.Object, newsDataService.Object, buttonDataService.Object, configuration.Object, wrapperService.Object);

            var result = await tgMainService.HandleUpdates();

            Assert.Equal(5, result);
        }

        /// <summary>
        /// 0.0.22.2.4
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task HandleUpdates_Success_2_Async()
        {
            var wrapperService = new Mock<ITelegramBotWrapper>();
            var groupsDataService = new Mock<IGroupsDataService>();
            var userDataService = new Mock<IUserDataService>();
            var newsDataService = new Mock<INewsDataService>();
            var buttonDataService = new Mock<IButtonsDataService>();
            var configuration = new Mock<IConfiguration>();
            var logger = new Mock<ILogger<TelegramMessageHandler>>();

            wrapperService.Setup(s => s.GetUpdatesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => [
                    new Update()
                    {
                        UpdateId = 4
                    }
                ]);

            var tgMainService = new TelegramMessageHandler(logger.Object, groupsDataService.Object, userDataService.Object, newsDataService.Object, buttonDataService.Object, configuration.Object, wrapperService.Object);

            var result = await tgMainService.HandleUpdates(new List<Update>(), CancellationToken.None);

            Assert.Equal(5, result);
        }
    }
}