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
    }
}