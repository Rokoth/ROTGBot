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
        /// 0.0.16.2.3
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task CreateNews_First_Success_Async()
        {
            var _repoMock = new Mock<IRepository<News>>();
            var _repoUserMock = new Mock<IRepository<User>>();
            var _repoMessageMock = new Mock<IRepository<NewsMessage>>();
            var _loggerMock = new Mock<ILogger<NewsDataService>>();

            _repoMock.Setup(s => s.GetAsync(It.IsAny<Filter<News>>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new List<News>()));

            _repoMock.Setup(s => s.AddAsync(It.IsAny<News>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new News()));

            var newsService = new NewsDataService(_repoMock.Object, _repoMessageMock.Object, _repoUserMock.Object, _loggerMock.Object);

            var result = await newsService.CreateNews(1, Guid.NewGuid(), 1, 1, "news", "test", false, new CancellationToken());

            Assert.True(result);
            _repoMock.Verify(s => s.AddAsync(It.Is<News>(s => s.Number == 1), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        /// <summary>
        /// 0.0.16.2.1
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task GetCurrentNews_Success_ExistsNews_Async()
        {
            var _repoMock = new Mock<IRepository<News>>();
            var _repoUserMock = new Mock<IRepository<User>>();
            var _repoMessageMock = new Mock<IRepository<NewsMessage>>();
            var _loggerMock = new Mock<ILogger<NewsDataService>>();

            var res = new News()
            {
                ChatId = 1,
                CreatedDate = DateTime.Now,
                Description = "test",
                UserId = Guid.NewGuid(),
                GroupId = 1,
                Id = Guid.NewGuid(),
                IsDeleted = false,
                IsModerate = true,
                IsMulti = false,
                ModeratorId = Guid.NewGuid(),
                Number = 4,
                State = "create",
                ThreadId = 5,
                Title = "test",
                Type = "news"                
            };

            _repoMock.Setup(s => s.GetAsync(It.IsAny<Filter<News>>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new List<News>() { res }));

            var newsService = new NewsDataService(_repoMock.Object, _repoMessageMock.Object, _repoUserMock.Object, _loggerMock.Object);

            var result = await newsService.GetCurrentNews(Guid.NewGuid(), new CancellationToken());

            Assert.NotNull(result);
            Assert.Equal(res.Id, result.Id);
            Assert.Equal(res.ChatId, result.ChatId);
            Assert.Equal(res.CreatedDate, result.CreatedDate);
            Assert.Equal(res.Description, result.Description);
            Assert.Equal(res.UserId, result.UserId);
            Assert.Equal(res.GroupId, result.GroupId);            
            Assert.Equal(res.IsModerate, result.IsModerate);
            Assert.Equal(res.IsMulti, result.IsMulti);            
            Assert.Equal(res.Number, result.Number);
            Assert.Equal(res.State, result.State);
            Assert.Equal(res.ThreadId, result.ThreadId);
            Assert.Equal(res.Title, result.Title);
            Assert.Equal(res.Type, result.Type);
        }

        /// <summary>
        /// 0.0.16.2.5
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task AddNewMessageForNews_ErrorOnEmptyText_Async()
        {
            var _repoMock = new Mock<IRepository<News>>();
            var _repoUserMock = new Mock<IRepository<User>>();
            var _repoMessageMock = new Mock<IRepository<NewsMessage>>();
            var _loggerMock = new Mock<ILogger<NewsDataService>>();

            _repoMessageMock.Setup(s => s.AddAsync(It.IsAny<NewsMessage>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new NewsMessage()));

            var newsService = new NewsDataService(_repoMock.Object, _repoMessageMock.Object, _repoUserMock.Object, _loggerMock.Object);

           
            await Assert.ThrowsAsync<ArgumentException>(() => newsService.AddNewMessageForNews(1, Guid.NewGuid(), null, new CancellationToken()));
        }
    }
}