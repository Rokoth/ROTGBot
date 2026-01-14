using Microsoft.Extensions.Configuration;
using ROTGBot.Service;
using Npgsql;
using Moq;
using Telegram.BotAPI;
using Telegram.BotAPI.GettingUpdates;
using Telegram.BotAPI.AvailableMethods;

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
        /// 0.0.12.2.5
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task SetCommands_Success_Async()
        {
            var handlerService = new Mock<ITelegramMessageHandler>();
            var wrapperService = new Mock<ITelegramBotWrapper>();
            handlerService.Setup(s => s.HandleUpdates(It.IsAny<IEnumerable<Update>>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            wrapperService.Setup(s => s.GetUpdatesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync([]);

            wrapperService.Setup(s => s.SetMyCommandsAsync(It.IsAny<SetMyCommandsArgs>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Task.CompletedTask);

            var tgMainService = new TelegramMainService(handlerService.Object, wrapperService.Object);

            var result = await tgMainService.SetCommands(new CancellationToken());

            Assert.True(result);

        }
    }
}