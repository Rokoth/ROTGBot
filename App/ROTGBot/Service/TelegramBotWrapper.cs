using Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Telegram.BotAPI;
using Telegram.BotAPI.AvailableMethods;
using Telegram.BotAPI.AvailableTypes;
using Telegram.BotAPI.GettingUpdates;

namespace ROTGBot.Service
{
    public class TelegramBotWrapper : ITelegramBotWrapper
    {
        private readonly string botToken = "token";
        private TelegramBotClient telegramBot;
        private ILogger<TelegramBotWrapper> _logger;

        public TelegramBotWrapper(IConfiguration configuration, ILogger<TelegramBotWrapper> logger)
        {
            var botSettings = configuration.GetSection("BotSettings").Get<BotSettings>();
            botToken = botSettings?.Token ?? botToken;
            _logger = logger;
        }

        public Task AnswerCallbackQueryAsync(AnswerCallbackQueryArgs args, CancellationToken token)
            => Execute(client => client.AnswerCallbackQueryAsync(args, cancellationToken: token));           

        public Task ForwardMessagesAsync(long chatId, long forwardChatId, IEnumerable<int> messageIds, CancellationToken token)
            => Execute(client => client.ForwardMessagesAsync(chatId, forwardChatId, messageIds, cancellationToken: token));

        public Task ForwardMessagesAsync(long chatId, long forwardChatId, IEnumerable<int> messageIds, int? threadId, CancellationToken token)
            => Execute(client => client.ForwardMessagesAsync(chatId, forwardChatId, messageIds, threadId, cancellationToken: token));

        public Task<IEnumerable<Update>> GetUpdatesAsync(int offset, CancellationToken token)
            => Execute(client => client.GetUpdatesAsync(offset, cancellationToken: token));

        public Task SendDocumentAsync(SendDocumentArgs args, CancellationToken token)
            => Execute(client => client.SendDocumentAsync(args, cancellationToken: token));

        public Task SendMessageAsync(long chatId, string message, CancellationToken token)
            => Execute(client => client.SendMessageAsync(chatId, message, cancellationToken: token));

        public Task SendMessageAsync(long chatId, string message, ReplyMarkup replyMarkup, CancellationToken token)
            => Execute(client => client.SendMessageAsync(chatId, message, replyMarkup: replyMarkup, cancellationToken: token));

        public Task SendMessageAsync(long chatId, string message, int? threadId, CancellationToken token)
        => Execute(client => client.SendMessageAsync(chatId, message, messageThreadId: threadId, cancellationToken: token));

        public Task SetMyCommandsAsync(SetMyCommandsArgs args, CancellationToken token)
            => Execute(client => client.SetMyCommandsAsync(args, cancellationToken: token));

        private async Task<T> Execute<T>(Func<TelegramBotClient, Task<T>> execFunc)
        {
            telegramBot ??= new TelegramBotClient(botToken);

            try
            {
                var result = await execFunc(telegramBot);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при вызове клиента");
                telegramBot = new TelegramBotClient(botToken);
                var result = await execFunc(telegramBot);
                return result;
            }
        }

    }
}
