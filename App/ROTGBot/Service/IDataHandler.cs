using ROTGBot.Contract.Model;

namespace ROTGBot.Service
{
    public interface IDataHandler
    {
        Task<bool> HandleData(long chatId, User user, string? dataReq, CancellationToken token);
    }
}