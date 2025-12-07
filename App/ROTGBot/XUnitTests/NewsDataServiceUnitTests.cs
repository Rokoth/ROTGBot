using Microsoft.Extensions.Configuration;
using ROTGBot.Service;
using ROTGBot.Db.Interface;
using Microsoft.Extensions.Logging;
using ROTGBot.Db.Model;
using Moq;

namespace XUnitTests
{
    public class NewsDataServiceUnitTests
    {
        private IConfiguration configuration;

        public NewsDataServiceUnitTests()
        {
            ConfigurationBuilder builder = new ConfigurationBuilder();
            builder.AddJsonFile("appsettings.json");
            configuration = builder.Build();
        }

        [Fact]
        public async Task AddNewMessageForNews_Ready_Async()
        {
            var _repoMock = new Mock<IRepository<News>>();
            var _repoUserMock = new Mock<IRepository<User>>();
            var _repoMessageMock = new Mock<IRepository<NewsMessage>>();
            var _loggerMock = new Mock<ILogger<NewsDataService>>();
                       
            _repoMessageMock.Setup(s => s.AddAsync(It.IsAny<NewsMessage>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new NewsMessage()));

            var newsService = new NewsDataService(_repoMock.Object, _repoMessageMock.Object, _repoUserMock.Object, _loggerMock.Object);

            var result = await newsService.AddNewMessageForNews(1, Guid.NewGuid(), "test", new CancellationToken());

            Assert.True(result);
        }

        [Fact]
        public async Task GetCurrentNews_Success_NoNews_Async()
        {
            var _repoMock = new Mock<IRepository<News>>();
            var _repoUserMock = new Mock<IRepository<User>>();
            var _repoMessageMock = new Mock<IRepository<NewsMessage>>();
            var _loggerMock = new Mock<ILogger<NewsDataService>>();

            _repoMock.Setup(s => s.GetAsync(It.IsAny<Filter<News>>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new List<News>()));

            var newsService = new NewsDataService(_repoMock.Object, _repoMessageMock.Object, _repoUserMock.Object, _loggerMock.Object);

            var result = await newsService.GetCurrentNews(Guid.NewGuid(), new CancellationToken());

            Assert.Null(result);
        }

        /// <summary>
        /// 0.0.18.2.4
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task GetNewsById_Success_Async()
        {
            var _repoMock = new Mock<IRepository<News>>();
            var _repoUserMock = new Mock<IRepository<User>>();
            var _repoMessageMock = new Mock<IRepository<NewsMessage>>();
            var _loggerMock = new Mock<ILogger<NewsDataService>>();

            var dbNews = new News()
            {
                ChatId = 1,
                CreatedDate = DateTime.Now,
                Description = "Test",
                GroupId = 2,
                Id = Guid.NewGuid(),
                IsDeleted = false,
                IsModerate = true,
                IsMulti = false,
                ModeratorId = Guid.NewGuid(),
                State = "accepted",
                Number = 1,
                ThreadId = 3,
                Title = "test",
                Type = "news",
                UserId = Guid.NewGuid()
            };

            _repoMock.Setup(s => s.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(dbNews));

            var newsService = new NewsDataService(_repoMock.Object, _repoMessageMock.Object, _repoUserMock.Object, _loggerMock.Object);

            var result = await newsService.GetNewsById(Guid.NewGuid(), new CancellationToken());

            Assert.NotNull(result);
            Assert.Equal(dbNews.ChatId, result.ChatId);
            Assert.Equal(dbNews.CreatedDate, result.CreatedDate);
            Assert.Equal(dbNews.Description, result.Description);
            Assert.Equal(dbNews.GroupId, result.GroupId);
            Assert.Equal(dbNews.Id, result.Id);
            Assert.Equal(dbNews.IsModerate, result.IsModerate);
            Assert.Equal(dbNews.IsMulti, result.IsMulti);
            Assert.Equal(dbNews.Number, result.Number);
            Assert.Equal(dbNews.ThreadId, result.ThreadId);
            Assert.Equal(dbNews.State, result.State);
            Assert.Equal(dbNews.Title, result.Title);
            Assert.Equal(dbNews.Type, result.Type);
            Assert.Equal(dbNews.UserId, result.UserId);
        }
    }
}