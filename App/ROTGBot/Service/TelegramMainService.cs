using System.Threading;
using Telegram.BotAPI.AvailableMethods;

namespace ROTGBot.Service
{
    public class TelegramMainService : ITelegramMainService
    {       
                
        private readonly ITelegramMessageHandler _telegramMessageHandler;
        private readonly ITelegramBotWrapper _client;

        public TelegramMainService(
            ITelegramMessageHandler telegramMessageHandler,
            ITelegramBotWrapper client)
        {
            _telegramMessageHandler = telegramMessageHandler;
            _client = client;
        }

        public async Task<int> Execute(int offset)
        {
            var cancellationToken = new CancellationTokenSource(60000).Token;
            
            var updates = await _client.GetUpdatesAsync(offset, cancellationToken);
            if ((updates?.Any()) != true)
            {
                return offset;
            }

            await _telegramMessageHandler.HandleUpdates(updates, cancellationToken);
            return updates.Last().UpdateId + 1;
        }

        public async Task SetCommands()
        {
            var cancellationToken = new CancellationTokenSource(60000).Token;
            await _client.SetMyCommandsAsync(new SetMyCommandsArgs([new("start", "Начать работу")]), cancellationToken);
        }
    }
}
