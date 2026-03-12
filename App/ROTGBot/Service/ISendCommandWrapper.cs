using ROTGBot.Contract.Model;

namespace ROTGBot.Service
{
    public interface ISendMessageWrapper
    {
        Task SendMenuButtons(long chatId, User user, string type, CancellationToken token);
        Task SendUserRemember(long chatId, News? news, CancellationToken token);
    }
}