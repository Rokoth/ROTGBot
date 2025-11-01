using Microsoft.Extensions.Configuration;
using ROTGBot.Service;
using Npgsql;
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

        /// <summary>
        /// 0.0.22.2.1
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task Execute_One_Update_Async()
        {
            var handlerService = new Mock<ITelegramMessageHandler>();
            var wrapperService = new Mock<ITelegramBotWrapper>();
            handlerService.Setup(s => s.HandleUpdates(It.IsAny<IEnumerable<Update>>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            wrapperService.Setup(s => s.GetUpdatesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => [
                    new Update()
                    {
                        UpdateId = 4
                    }
                ]);

            var tgMainService = new TelegramMainService(handlerService.Object, wrapperService.Object);

            var result = await tgMainService.Execute(1);

            Assert.Equal(5, result);
        }
    }
}