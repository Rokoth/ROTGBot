using Microsoft.Extensions.Configuration;
using ROTGBot.Service;
using Moq;
using Telegram.BotAPI.GettingUpdates;

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
            
            wrapperService.Setup(s => s.GetUpdatesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => [
                    new Update()
                    {
                        UpdateId = 4
                    }
                ]);

            var tgMainService = new TelegramMessageHandler(handlerService.Object, wrapperService.Object);

            var result = await tgMainService.Execute(1);

            Assert.Equal(5, result);
        }
    }
}