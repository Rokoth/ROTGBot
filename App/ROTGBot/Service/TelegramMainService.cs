using Common;
using Microsoft.Extensions.Configuration;
using Telegram.BotAPI;
using Telegram.BotAPI.AvailableMethods;
using Telegram.BotAPI.GettingUpdates;

namespace ROTGBot.Service
{
    public class TelegramMainService : ITelegramMainService
    {
        private readonly string botToken = "token";
                
        private readonly ITelegramMessageHandler _telegramMessageHandler;


        public TelegramMainService(
            ITelegramMessageHandler telegramMessageHandler,
            IConfiguration configuration)
        {
            _telegramMessageHandler = telegramMessageHandler;         
            var botSettings = configuration.GetSection("BotSettings").Get<BotSettings>();
            botToken = botSettings?.Token ?? botToken;
        }

        public async Task<int> Execute(int offset)
        {
            var cancellationToken = new CancellationTokenSource(60000).Token;
            var client = new TelegramBotClient(botToken);

            var updates = await client.GetUpdatesAsync(offset, cancellationToken: cancellationToken);
            if ((updates?.Any()) != true)
            {
                return offset;
            }

            await _telegramMessageHandler.HandleUpdates(client, updates, cancellationToken);
            return updates.Last().UpdateId + 1;
        }

        public async Task SetCommands()
        {
            var client = new TelegramBotClient(botToken);
            _ = await client.SetMyCommandsAsync(new SetMyCommandsArgs([new("start", "Начать работу")]));
        }
    }
}
