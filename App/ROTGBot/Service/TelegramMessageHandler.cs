using Microsoft.Extensions.Logging;
using Telegram.BotAPI.AvailableTypes;
using Telegram.BotAPI.GettingUpdates;

namespace ROTGBot.Service
{


    public class TelegramMessageHandler(
        ILogger<TelegramMessageHandler> logger,
        IGroupsDataService groupsDataService,
        IUserDataService userDataService,
        IHandler<Message> messageHandler,
        IHandler<CallbackQuery> callbackHandler,
        IDataHandler dataHandler,
        ITelegramBotWrapper wrapper) : ITelegramMessageHandler
    {
        private readonly ILogger<TelegramMessageHandler> _logger = logger;

        private readonly IGroupsDataService _groupsDataService = groupsDataService;
        private readonly IUserDataService _userDataService = userDataService;
        private readonly ITelegramBotWrapper client = wrapper;
        private readonly IHandler<Message> _messageHandler = messageHandler;
        private readonly IHandler<CallbackQuery> _callbackHandler = callbackHandler;
        private readonly IDataHandler _dataHandler = dataHandler;

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
                    await HandleMyChatMember(update.MyChatMember, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка при обработке события");
                    success = false;
                }
            }
            return success;
        }

        

        private async Task HandleMyChatMember(ChatMemberUpdated? myChatMember, CancellationToken cancellationToken)
        {
            if (myChatMember == null)
                return;

            var chatId = myChatMember.Chat.Id;
            var description = $"{myChatMember.Chat.Title} : {myChatMember.Chat.FirstName} {myChatMember.Chat.LastName} (@{myChatMember.Chat.Username})";
            var title = myChatMember.Chat.Title;

            await _groupsDataService.AddGroupIfNotExists(chatId, title, description, cancellationToken);
        }                
    }
}
