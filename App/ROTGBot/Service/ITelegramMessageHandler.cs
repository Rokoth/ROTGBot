using Telegram.BotAPI;
using Telegram.BotAPI.GettingUpdates;

namespace ROTGBot.Service
{
    public interface ITelegramMessageHandler
    {
        Task<(bool success, string result)> CreateAndSendPassword(string login, CancellationToken cancellationToken);
        Task HandleUpdates(IEnumerable<Update> updates, CancellationToken cancellationToken);
    }
}