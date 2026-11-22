using Microsoft.Extensions.Logging;
using Telegram.BotAPI.AvailableTypes;
using Telegram.BotAPI.GettingUpdates;

namespace ROTGBot.Service
{
    public class TelegramMessageHandler(
        ILogger<TelegramMessageHandler> logger,       
        IHandler<Message> messageHandler,
        IHandler<CallbackQuery> callbackHandler,
        IHandler<ChatMemberUpdated> chatMemberHandler) : ITelegramMessageHandler
    {
        private readonly ILogger<TelegramMessageHandler> _logger = logger;
                
        private readonly IHandler<Message> _messageHandler = messageHandler;
        private readonly IHandler<CallbackQuery> _callbackHandler = callbackHandler;
        private readonly IHandler<ChatMemberUpdated> _chatMemberHandler = chatMemberHandler;

        public async Task<bool> HandleUpdates(IEnumerable<Update> updates, CancellationToken cancellationToken)
        {            
            ArgumentNullException.ThrowIfNull(updates);
            var success = true;
            foreach (var update in updates)
            {
                try
                {
                    await _messageHandler.Handle(update.Message, cancellationToken);
                    await _callbackHandler.Handle(update.CallbackQuery, cancellationToken);
                    await _chatMemberHandler.Handle(update.MyChatMember, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка при обработке события");
                    success = false;
                }
            }
            return success;
        }       
    }
}
