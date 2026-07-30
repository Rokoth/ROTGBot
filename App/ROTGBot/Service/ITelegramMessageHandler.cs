using Telegram.BotAPI;
using Telegram.BotAPI.GettingUpdates;

namespace ROTGBot.Service
{
    public interface ITelegramMessageHandler
    {
        Task<bool> HandleUpdates(IEnumerable<Update> updates, CancellationToken cancellationToken);
    }
}