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
        /// 0.0.12.2.3
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task CreateNews_Success_Async()
        {
            var _repoMock = new Mock<IRepository<News>>();
            var _repoUserMock = new Mock<IRepository<User>>();
            var _repoMessageMock = new Mock<IRepository<NewsMessage>>();
            var _loggerMock = new Mock<ILogger<NewsDataService>>();

            var assert = new News()
            {
                ChatId = 1,
                CreatedDate = DateTime.Now,
                Description = "test",
                GroupId = 2,
                GroupName = "",
                Id = Guid.NewGuid(),
                IsDeleted = false,
                IsModerate = false,
                IsMulti = false,
                ModeratorId = null,
                Number = 3,
                State = "accepted",
                ThreadId = 4,
                ThreadName = "",
                Title = "",
                Type = "news",
                UserId = Guid.NewGuid()
            };

            _repoMock.Setup(s => s.GetAsync(It.IsAny<Filter<News>>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new List<News>()
                {
                    assert
                }));

            _repoMock.Setup(s => s.AddAsync(It.IsAny<News>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new News()));

            var newsService = new NewsDataService(_repoMock.Object, _repoMessageMock.Object, _repoUserMock.Object, _loggerMock.Object);

            var result = await newsService.CreateNews(1, Guid.NewGuid(), 1, 1, "news", "test", false, new CancellationToken());

            Assert.NotNull(result);
            Assert.Equal(assert.Number + 1, result.Number);
        }
    }
}