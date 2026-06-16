using ROTGBot.Contract.Model;

namespace ROTGBot.Service
{
    public interface IUserDataService
    {
        Task<IEnumerable<Contract.Model.User>> GetNotifyModerators(CancellationToken token);
        Task<Contract.Model.User?> GetOrAddUser(long tgId, string tgUserName, string tgFullName, long? chatId, CancellationToken cancellationToken);
        Task<bool> SetRole(string login, Contract.Model.RoleEnum role, CancellationToken token);
        Task<bool> SwitchUserNotify(Guid userId, CancellationToken token);
        Task SetUserSendDate(Guid userId, CancellationToken token);
        Task<Contract.Model.User> GetUser(Guid userId, CancellationToken token);
        Task<User> GetUserByNumber(int number, CancellationToken cancellationToken);
        Task<User> GetUserByLogin(string loginOrNumber, CancellationToken cancellationToken);
        Task<List<User>> GetUsers(int? count, int? daysCount, CancellationToken cancellationToken);
    }
}