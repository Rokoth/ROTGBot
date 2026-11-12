using Telegram.BotAPI.AvailableMethods;
using Telegram.BotAPI.AvailableTypes;

namespace ROTGBot.Service
{
    public class CallbackHandler(
        IUserDataService userDataService,       
        IDataHandler dataHandler,
        ITelegramBotWrapper wrapper): IHandler<CallbackQuery>
    {        
        private readonly IUserDataService _userDataService = userDataService;
        private readonly ITelegramBotWrapper client = wrapper;
        private readonly IDataHandler _dataHandler = dataHandler;


        public async Task<bool> Handle(CallbackQuery? callbackQuery, CancellationToken token)
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
    }
}
