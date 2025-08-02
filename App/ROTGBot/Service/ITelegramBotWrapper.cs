using System.Linq.Dynamic.Core.Tokenizer;
using System.Threading;
using Telegram.BotAPI.AvailableMethods;
using Telegram.BotAPI.AvailableTypes;
using Telegram.BotAPI.GettingUpdates;

namespace ROTGBot.Service
{
    public interface ITelegramBotWrapper
    {
        Task<IEnumerable<Update>> GetUpdatesAsync(int offset, CancellationToken token);

        Task SetMyCommandsAsync(SetMyCommandsArgs args, CancellationToken token);

        Task AnswerCallbackQueryAsync(AnswerCallbackQueryArgs args, CancellationToken token);

        Task SendMessageAsync(long chatId, string message, CancellationToken token);

        Task SendMessageAsync(long chatId, string message, ReplyMarkup replyMarkup, CancellationToken token);

        Task ForwardMessagesAsync(long chatId, long forwardChatId, IEnumerable<int> messageIds, CancellationToken token);

        Task ForwardMessagesAsync(long chatId, long forwardChatId, IEnumerable<int> messageIds, int? threadId, CancellationToken token);
    }
}