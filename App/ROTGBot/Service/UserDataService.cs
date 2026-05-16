using ROTGBot.Contract.Model;
using ROTGBot.Db.Interface;
using ROTGBot.Db.Model;
using System.Data;
using System.Linq.Dynamic.Core.Tokenizer;
using Telegram.BotAPI.AvailableTypes;
using User = Telegram.BotAPI.AvailableTypes.User;

namespace ROTGBot.Service
{
    public class UserDataService(IRepository<Db.Model.User> userRepo,
        IRepository<Db.Model.Role> roleRepo,
        IRepository<Db.Model.UserRole> userRoleRepo) : IUserDataService
    {
        private readonly IRepository<Db.Model.User> _userRepo = userRepo;
        private readonly IRepository<Db.Model.Role> _roleRepo = roleRepo;
        private readonly IRepository<Db.Model.UserRole> _userRoleRepo = userRoleRepo;

        public async Task<Contract.Model.User?> GetOrAddUser(long tgId, string tgUserName, string tgFullName, long? chatId, CancellationToken cancellationToken)
        {
            var user = (await _userRepo.GetAsync(new Filter<Db.Model.User>()
            {
                Selector = s => s.TGId == tgId
            }, cancellationToken)).FirstOrDefault();

            if (user == null)
            {
                if(chatId == null)
                {
                    return null;
                }
                user = await _userRepo.AddAsync(new Db.Model.User()
                {
                    Id = Guid.NewGuid(),
                    Description = tgFullName,//$"{tguser.FirstName} {tguser.LastName} (@{tguser.Username})",
                    IsDeleted = false,
                    Name = tgFullName,
                    TGLogin = tgUserName,
                    TGId = tgId,
                    ChatId = chatId.Value,
                    LastSendDate = DateTime.Now.AddHours(-1)
                }, true, cancellationToken);

                var userRole = (await _roleRepo.GetAsync(new Filter<Db.Model.Role>() { Selector = s => s.Name == "user" }, cancellationToken)).First();

                await _userRoleRepo.AddAsync(new Db.Model.UserRole()
                {
                    Id = Guid.NewGuid(),
                    IsDeleted = false,
                    RoleId = userRole.Id,
                    UserId = user.Id
                }, true, cancellationToken);
            }
            else if(chatId != null && user.ChatId != chatId)
            {
                user.ChatId = chatId.Value;
                await _userRepo.UpdateAsync(user, true, cancellationToken);
            }
            return await Map(user, cancellationToken);
        }

        private async Task<Contract.Model.User> Map(Db.Model.User user, CancellationToken cancellationToken)
        {
            var roles = (await GetUserRoleNames(user.Id, cancellationToken)).Select(s => Enum.Parse<RoleEnum>(s))?.ToList() ?? [RoleEnum.user];
            return new Contract.Model.User()
            {
                ChatId = user.ChatId,
                Description = user.Description,
                Id = user.Id,
                IsNotify = user.IsNotify,
                Name = user.Name,
                Roles = roles,
                TGId = user.TGId,
                TGLogin = user.TGLogin,
                LastSendDate = user.LastSendDate
            };
        }

        private async Task<string[]> GetUserRoleNames(Guid userId, CancellationToken token)
        {
            string[] roles = [];
            var userRoles = (await _userRoleRepo.GetAsync(new Filter<Db.Model.UserRole>() { Selector = s => s.UserId == userId }, token)).Select(s => s.RoleId).Distinct().ToArray();
            if (userRoles.Length != 0)
            {
                roles = [.. (await _roleRepo.GetAsync(new Filter<Db.Model.Role>() { Selector = s => userRoles.Contains(s.Id) }, token)).Select(s => s.Name)];
            }

            return roles;
        }

        public async Task<IEnumerable<Contract.Model.User>> GetNotifyModerators(CancellationToken token)
        {
            var result = await _userRepo.GetAsync(new Filter<Db.Model.User>()
            {
                Selector = s => !s.IsDeleted && s.IsNotify
            }, token);

            var users = new List<Contract.Model.User>();

            foreach(var res in result)
            {
                users.Add(await Map(res, token));
            }

            return users.Where(s => s.IsModerator);
        }

        public async Task SetRole(string login, RoleEnum role, CancellationToken token)
        {
            var user = (await _userRepo.GetAsync(new Filter<Db.Model.User>()
            {
                Selector = s => s.TGLogin != null && s.TGLogin == login
            }, token)).FirstOrDefault();

            if (user != null)
            {
                var newRole = (await _roleRepo.GetAsync(new Filter<Db.Model.Role>() { Selector = s => s.Name == Enum.GetName(typeof(RoleEnum), role) }, token)).First();

                await _userRoleRepo.AddAsync(new Db.Model.UserRole()
                {
                    Id = Guid.NewGuid(),
                    IsDeleted = false,
                    RoleId = newRole.Id,
                    UserId = user.Id
                }, true, token);
            }
        }

        public async Task<bool> SwitchUserNotify(Guid userId, CancellationToken token)
        {
            var user = await _userRepo.GetAsync(userId, token);
            user.IsNotify = !user.IsNotify;
            await _userRepo.UpdateAsync(user, true, token);
            return user.IsNotify;
        }

        public async Task SetUserSendDate(Guid userId, CancellationToken token)
        {
            var user = await _userRepo.GetAsync(userId, token);
            user.LastSendDate = DateTime.Now;
            await _userRepo.UpdateAsync(user, true, token);            
        }

        public async Task<Contract.Model.User> GetUser(Guid userId, CancellationToken token)
        {
            var user = await _userRepo.GetAsync(userId, token);
            return await Map(user, token);
        }

        public async Task<List<Contract.Model.User>> GetUsers(Contract.Filters.Filter<Contract.Model.User> filter, CancellationToken token)
        {
            var users = await _userRepo.GetAsync(new Filter<Db.Model.User>()
            {
                Page = filter.Page,
                Size = filter.Size,
                Sort = filter.Sort,
                Selector = s => (string.IsNullOrEmpty(filter.Name)
                || s.Name.Contains(filter.Name, StringComparison.OrdinalIgnoreCase) 
                || s.TGLogin.Contains(filter.Name, StringComparison.OrdinalIgnoreCase))
            }, token);
            
            var result = new List<Contract.Model.User>();
            await foreach(var item in Map(users, token))
            {
                result.Add(item);
            }
            return result;
        }

        private async IAsyncEnumerable<Contract.Model.User> Map(List<Db.Model.User> users, CancellationToken token)
        {           
            foreach(var item in users)
            {
                yield return await Map(item, token);                
            }
        }

        public async Task<List<Contract.Model.UserRole>> GetUserRoles(Guid userId, CancellationToken token)
        {
            //var 
            List<Contract.Model.UserRole> result = [];
            var userRoles = (await _userRoleRepo.GetAsync(new Filter<Db.Model.UserRole>() { Selector = s => s.UserId == userId }, token)).Distinct().ToArray();
            var roles = await _roleRepo.GetAsync(new Filter<Db.Model.Role>(), token);

            foreach(var userRole in userRoles)
            {

                result.Add(new Contract.Model.UserRole()
                {
                    RoleId = userRole.RoleId,
                    RoleName = 
                });
            }
        }

        public async Task DeleteUserRole(Contract.Model.UserRole userRole, CancellationToken token)
        {
            var userRoles = (await _userRoleRepo.GetAsync(new Filter<Db.Model.UserRole>() 
            { 
                Selector = s => s.UserId == userRole.UserId && s.IsDeleted == false 
            }, token)).Distinct().ToArray();

            var toDelete = userRoles.FirstOrDefault(s => s.RoleId == userRole.UserId);

            if (toDelete == null)
            {
                throw new Exception("Данная роль пользователю не назначена");
            }

            await _userRoleRepo.DeleteAsync(toDelete, true, token);
        }

        public async Task UnblockUser(Guid id, CancellationToken cancellationToken)
        {
            var user = await _userRepo.GetAsync(id, cancellationToken);
            user.IsBlocked = true;            
        }

        public async Task<bool> BlockUser(Guid id, CancellationToken cancellationToken)
        {
            var user = await _userRepo.GetAsync(id, cancellationToken);
            user.IsBlocked = true;
            return true;
        }

        public Task<List<Contract.Model.Role>> GetRoles(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
