using Microsoft.Extensions.Logging;
using Telegram.BotAPI.AvailableMethods;
using Telegram.BotAPI.AvailableTypes;
using Telegram.BotAPI.GettingUpdates;

namespace ROTGBot.Service
{
    public class TelegramMessageHandler(
        ILogger<TelegramMessageHandler> logger,
        IGroupsDataService groupsDataService,
        IUserDataService userDataService,
        IHandler<Message> messageHandler,
        IDataHandler dataHandler,
        ITelegramBotWrapper wrapper) : ITelegramMessageHandler
    {
        private readonly ILogger<TelegramMessageHandler> _logger = logger;

        private readonly IGroupsDataService _groupsDataService = groupsDataService;
        private readonly IUserDataService _userDataService = userDataService;
        private readonly ITelegramBotWrapper client = wrapper;
        private readonly IHandler<Message> _messageHandler = messageHandler;
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
                    await HandleCallback(update.CallbackQuery, cancellationToken);
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

        private async Task<bool> HandleCallback(CallbackQuery? callbackQuery, CancellationToken token)
        {
            if (callbackQuery == null)
                return false;

            var chatId = callbackQuery.Message?.Chat.Id;
            if (chatId == null)
            {
                return false;
            }

            var tgUser = callbackQuery.From;

            Contract.Model.User? user;
            if (callbackQuery.Message?.Chat?.Type != "private")
            {
                user = await _userDataService.GetOrAddUser(tgUser.Id, tgUser.Username, $"{tgUser.FirstName} {tgUser.LastName} (@{tgUser.Username})", null, token);
            }
            else
            {
                user = await _userDataService.GetOrAddUser(tgUser.Id, tgUser.Username, $"{tgUser.FirstName} {tgUser.LastName} (@{tgUser.Username})", chatId.Value, token);
            }
            if (user == null)
            {
                return false;
            }
            var data = callbackQuery.Data;
            if (data == null) return false;
            
            var result = await _dataHandler.HandleData(user.ChatId, user, data, token);
            await client.AnswerCallbackQueryAsync(new AnswerCallbackQueryArgs(callbackQuery.Id), token: token);
            return result;
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
