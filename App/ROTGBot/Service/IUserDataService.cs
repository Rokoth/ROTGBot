using ROTGBot.Contract.Model;

namespace ROTGBot.Service
{
    public interface IUserDataService
    {
        Task<IEnumerable<Contract.Model.User>> GetNotifyModerators(CancellationToken token);
        Task<Contract.Model.User?> GetOrAddUser(long tgId, string tgUserName, string tgFullName, long? chatId, CancellationToken cancellationToken);
        Task SetRole(string login, Contract.Model.RoleEnum role, CancellationToken token);
        Task<bool> SwitchUserNotify(Guid userId, CancellationToken token);
        Task SetUserSendDate(Guid userId, CancellationToken token);
        Task<Contract.Model.User> GetUser(Guid userId, CancellationToken token);
        Task<List<Contract.Model.User>> GetUsers(Contract.Filters.Filter<User> filter, CancellationToken token);
        Task<List<UserRole>> GetUserRoles(Guid userId, CancellationToken cancellationToken);
        Task DeleteUserRole(UserRole userRole);
    }
}