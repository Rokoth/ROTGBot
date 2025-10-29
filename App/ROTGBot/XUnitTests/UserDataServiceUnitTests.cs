using Microsoft.Extensions.Configuration;
using Moq;
using ROTGBot.Db.Interface;
using ROTGBot.Db.Model;
using ROTGBot.Service;
using System.Linq;

namespace XUnitTests
{
    public class UserDataServiceUnitTests
    {
        private IConfiguration configuration;

        public UserDataServiceUnitTests()
        {
            ConfigurationBuilder builder = new();
            builder.AddJsonFile("appsettings.json");
            configuration = builder.Build();
        }

        [Fact]
        public async Task GetNotifyModerators_Exists_Two_Moderators_From_Four_USers_Async()
        {
            var _repoMock = new Mock<IRepository<User>>();
            var _repoRoleMock = new Mock<IRepository<Role>>();
            var _repouserRoleMock = new Mock<IRepository<UserRole>>();

            var user1Id = Guid.NewGuid();
            var user2Id = Guid.NewGuid();
            var user3Id = Guid.NewGuid();
            var user4Id = Guid.NewGuid();

            var moderGuid = Guid.NewGuid();
            var userGuid = Guid.NewGuid();

            _repoMock.Setup(s => s.GetAsync(It.IsAny<Filter<User>>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new List<User>()
                {
                    new()
                    {
                        Id = user1Id,
                        IsDeleted = false,
                        IsNotify = true
                    },
                    new()
                    {
                        Id = user2Id,
                        IsDeleted = false,
                        IsNotify = true
                    },
                    new()
                    {
                        Id = user3Id,
                        IsDeleted = false,
                        IsNotify = true
                    },
                    new()
                    {
                        Id = user4Id,
                        IsDeleted = false,
                        IsNotify = true
                    }
                }));

            _repouserRoleMock.Setup(s => s.GetAsync(It.IsAny<Filter<UserRole>>(), It.IsAny<CancellationToken>()))
                .Returns<Filter<UserRole>, CancellationToken>((f,t) => Task.FromResult(GetUserRoles(f,
                [user1Id, user2Id, user3Id, user4Id], 
                [user1Id , user2Id],
                moderGuid, 
                userGuid)));

            _repoRoleMock.Setup(s => s.GetAsync(It.IsAny<Filter<Role>>(), It.IsAny<CancellationToken>()))
                .Returns<Filter<Role>, CancellationToken>((f, t) => Task.FromResult(GetRoles(f,                
                moderGuid,
                userGuid)));

            var buttonsService = new UserDataService(_repoMock.Object, _repoRoleMock.Object, _repouserRoleMock.Object);

            var result = await buttonsService.GetNotifyModerators(new CancellationToken());

            Assert.NotNull(result);
            Assert.Equal(2, result.Count());
        }

        [Fact]
        public async Task GetOrAddUser_UserExists_NoUpdate_Success_Async()
        {
            var _repoMock = new Mock<IRepository<User>>();
            var _repoRoleMock = new Mock<IRepository<Role>>();
            var _repouserRoleMock = new Mock<IRepository<UserRole>>();

            var user1Id = Guid.NewGuid();
            
            var moderGuid = Guid.NewGuid();
            var userGuid = Guid.NewGuid();

            _repoMock.Setup(s => s.GetAsync(It.IsAny<Filter<User>>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new List<User>()
                {
                    new()
                    {
                        Id = user1Id,
                        IsDeleted = false,
                        IsNotify = true,
                        ChatId = 1
                    }
                }));

            _repouserRoleMock.Setup(s => s.GetAsync(It.IsAny<Filter<UserRole>>(), It.IsAny<CancellationToken>()))
                .Returns<Filter<UserRole>, CancellationToken>((f, t) => Task.FromResult(new List<UserRole>
                {
                    new UserRole()
                    {
                        Id = Guid.NewGuid(),
                        IsDeleted = false,
                        RoleId = userGuid,
                        UserId = user1Id
                    }
                }));

            _repoRoleMock.Setup(s => s.GetAsync(It.IsAny<Filter<Role>>(), It.IsAny<CancellationToken>()))
                .Returns<Filter<Role>, CancellationToken>((f, t) => Task.FromResult(GetRoles(f,
                moderGuid,
                userGuid)));

            var buttonsService = new UserDataService(_repoMock.Object, _repoRoleMock.Object, _repouserRoleMock.Object);

            var result = await buttonsService.GetOrAddUser(1, "test","test", 1, new CancellationToken());

            Assert.NotNull(result);
            _repoMock.Verify(m => m.AddAsync(It.IsAny<User>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
            _repoRoleMock.Verify(m => m.AddAsync(It.IsAny<Role>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
            _repoMock.Verify(m => m.UpdateAsync(It.IsAny<User>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        private static List<Role> GetRoles(Filter<Role> f, Guid moderGuid, Guid userGuid)
        {
            var result = new List<Role>
            {
                new()
                {
                    Id = moderGuid,
                    IsDeleted = false,
                    Name = "moderator"
                },
                new()
                {
                    Id = userGuid,
                    IsDeleted = false,
                    Name = "user"
                }
            };

            return [.. result.AsQueryable().Where(f.Selector!)];
        }

        private static List<UserRole> GetUserRoles(Filter<UserRole> f, List<Guid> allGuids, List<Guid> moderGuids, Guid moderGuid, Guid userGuid)
        {
            var result = new List<UserRole>();

            foreach(var userId in allGuids)
            {
                result.Add(new UserRole()
                {
                    Id = Guid.NewGuid(),
                    IsDeleted = false,
                    RoleId = userGuid,
                    UserId = userId
                });

                if(moderGuids.Contains(userId))
                {
                    result.Add(new UserRole()
                    {
                        Id = Guid.NewGuid(),
                        IsDeleted = false,
                        RoleId = moderGuid,
                        UserId = userId
                    });
                }
            }

            return [.. result.AsQueryable().Where(f.Selector!)];
        }
    }
}