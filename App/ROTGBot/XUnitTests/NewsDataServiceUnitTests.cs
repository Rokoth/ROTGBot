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
        /// 0.0.24.2.1
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task GetNewsById_Error_NoNews_Async()
        {
            var _repoMock = new Mock<IRepository<News>>();
            var _repoUserMock = new Mock<IRepository<User>>();
            var _repoMessageMock = new Mock<IRepository<NewsMessage>>();
            var _loggerMock = new Mock<ILogger<NewsDataService>>();

            News? news = null;

            _repoMock.Setup(s => s.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(news));

            var newsService = new NewsDataService(_repoMock.Object, _repoMessageMock.Object, _repoUserMock.Object, _loggerMock.Object);

            var result = await newsService.GetNewsById(Guid.NewGuid(), new CancellationToken());

            Assert.Null(result);
        }

        /// <summary>
        /// 0.0.24.2.5
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task CreateNews_Success_Async()
        {
            var _repoMock = new Mock<IRepository<News>>();
            var _repoUserMock = new Mock<IRepository<User>>();
            var _repoMessageMock = new Mock<IRepository<NewsMessage>>();
            var _loggerMock = new Mock<ILogger<NewsDataService>>();

            News? news = null;

            _repoMock.Setup(s => s.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(news));

            _repoMock.Setup(s => s.AddAsync(It.IsAny<News>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(news));

            var newsService = new NewsDataService(_repoMock.Object, _repoMessageMock.Object, _repoUserMock.Object, _loggerMock.Object);

            var result = await newsService.CreateNews(1, Guid.NewGuid(), 1, 1, "news", "test", false, new CancellationToken());

            Assert.NotNull(result);
        }
    }
}