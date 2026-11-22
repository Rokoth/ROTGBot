using Telegram.BotAPI.AvailableTypes;

namespace ROTGBot.Service
{
    public class ChatMemberHandler(IGroupsDataService groupsDataService) : IHandler<ChatMemberUpdated>
    {
        IGroupsDataService _groupsDataService = groupsDataService;

        public async Task<bool> Handle(ChatMemberUpdated? message, CancellationToken cancellationToken)
        {
            if (message == null)
                return false;

            var chatId = message.Chat.Id;
            var description = $"{message.Chat.Title} : {message.Chat.FirstName} {message.Chat.LastName} (@{message.Chat.Username})";
            var title = message.Chat.Title;

            await _groupsDataService.AddGroupIfNotExists(chatId, title, description, cancellationToken);

            return true;
        }
    }
}
