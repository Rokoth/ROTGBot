using Telegram.BotAPI;
using Telegram.BotAPI.GettingUpdates;

namespace ROTGBot.Service
{
    public interface ITelegramMessageHandler
    {
        Task HandleUpdates(TelegramBotClient client, IEnumerable<Update> updates, CancellationToken cancellationToken);
    }
}