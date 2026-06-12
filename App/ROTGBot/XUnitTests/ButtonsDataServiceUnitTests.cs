using Microsoft.Extensions.Configuration;
using ROTGBot.Service;
using ROTGBot.Db.Interface;
using ROTGBot.Db.Model;
using Moq;

namespace XUnitTests
{
    public class ButtonsDataServiceUnitTests
    {
        private IConfiguration configuration;

        public ButtonsDataServiceUnitTests()
        {
            ConfigurationBuilder builder = new ConfigurationBuilder();
            builder.AddJsonFile("appsettings.json");
            configuration = builder.Build();
        }

        [Fact]
        public async Task AddNewButton_NoButtons_Async()
        {
            var _repoMock = new Mock<IRepository<NewsButton>>();

            _repoMock.Setup(s => s.GetAsync(It.IsAny<Filter<NewsButton>>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new List<NewsButton>()));

            _repoMock.Setup(s => s.AddAsync(It.IsAny<NewsButton>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new NewsButton()));

            var buttonsService = new ButtonsDataService(_repoMock.Object);

            var result = await buttonsService.AddNewButton(1, 1, "chat", "chat", new CancellationToken());

            Assert.True(result);
        }

        [Fact]
        public async Task AddNewButton_ButtonExists_Async()
        {
            var _repoMock = new Mock<IRepository<NewsButton>>();

            _repoMock.Setup(s => s.GetAsync(It.IsAny<Filter<NewsButton>>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new List<NewsButton>()
                {
                    new NewsButton()
                    {
                        ButtonName = "chat",
                        ButtonNumber = 1,
                        ChatId = 1,
                        ChatName = "chat",
                        Id = Guid.NewGuid(),
                        IsDeleted = false,
                        ThreadId = 1,
                        ThreadName = "chat",
                        ToSend = true
                    }
                }));

            _repoMock.Setup(s => s.AddAsync(It.IsAny<NewsButton>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new NewsButton()));

            var buttonsService = new ButtonsDataService(_repoMock.Object);

            var result = await buttonsService.AddNewButton(1, 1, "chat", "chat", new CancellationToken());

            Assert.False(result);
        }

        [Fact]
        public async Task AddNewButton_ChatButtonIsNull_Error_Async()
        {
            var _repoMock = new Mock<IRepository<NewsButton>>();

            _repoMock.Setup(s => s.GetAsync(It.IsAny<Filter<NewsButton>>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new List<NewsButton>()
                {
                    new()
                    {
                        ButtonName = "chat",
                        ButtonNumber = 1,
                        ChatId = 1,
                        ChatName = "chat",
                        Id = Guid.NewGuid(),
                        IsDeleted = false,
                        ThreadId = 1,
                        ThreadName = "chat",
                        ToSend = true
                    }
                }));

            _repoMock.Setup(s => s.AddAsync(It.IsAny<NewsButton>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new NewsButton()));

            var buttonsService = new ButtonsDataService(_repoMock.Object);

            await Assert.ThrowsAsync<ArgumentException>(() => buttonsService.AddNewButton(1, 1, null, "chat", new CancellationToken()));
        }

        /// <summary>
        /// 0.0.24.2.4
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task GetAllButtons_Success_Async()
        {
            var _repoMock = new Mock<IRepository<NewsButton>>();

            var button1 = GenerateButton(1);
            var button2 = GenerateButton(2);
            var button3 = GenerateButton(3);

            _repoMock.Setup(s => s.GetAsync(It.IsAny<Filter<NewsButton>>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new List<NewsButton>()
                {
                    button1, button2, button3
                }));

            _repoMock.Setup(s => s.AddAsync(It.IsAny<NewsButton>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new NewsButton()));

            var buttonsService = new ButtonsDataService(_repoMock.Object);

            var result = await buttonsService.GetAllButtons(new CancellationToken());

            Assert.Equal(3, result.Count);

            foreach(var testButton in new NewsButton[] { button1, button2, button3 })
            {
                var actual = result.FirstOrDefault(s => s.ButtonNumber == testButton.ButtonNumber);
                Assert.NotNull(actual);

                Assert.Equal(testButton.ThreadName, actual.ThreadName);
                Assert.Equal(testButton.ChatName, actual.ChatName);
                Assert.Equal(testButton.ButtonName, actual.ButtonName);
                Assert.Equal(testButton.ThreadId, actual.ThreadId);
                Assert.Equal(testButton.ChatId, actual.ChatId);
                Assert.Equal(testButton.ParentId, actual.ParentId);
                Assert.Equal(testButton.IsParent, actual.IsParent);
                Assert.Equal(testButton.ToSend, actual.ToSend);
            }
        }

        private static NewsButton GenerateButton(int buttonNumber)
        {
            return new()
            {
                ButtonName = $"ButtonName{buttonNumber}",
                ButtonNumber = buttonNumber,
                ChatId = buttonNumber,
                ChatName = $"ChatName{buttonNumber}",
                Id = Guid.NewGuid(),
                IsDeleted = false,
                ThreadId = buttonNumber,
                ThreadName = $"ThreadName{buttonNumber}",
                ToSend = true
            };
        }

        /// <summary>
        /// 0.0.24.2.3
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task AddParentButton_Success_Async()
        {
            var _repoMock = new Mock<IRepository<NewsButton>>();

            _repoMock.SetupSequence(s => s.GetAsync(It.IsAny<Filter<NewsButton>>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new List<NewsButton>()))
                .Returns(Task.FromResult(new List<NewsButton>()
                {
                    new()
                    {
                        ButtonName = "chat",
                        ButtonNumber = 1,
                        ChatId = 1,
                        ChatName = "chat",
                        Id = Guid.NewGuid(),
                        IsDeleted = false,
                        ThreadId = 1,
                        ThreadName = "chat",
                        ToSend = true
                    }
                }));

            _repoMock.Setup(s => s.AddAsync(It.IsAny<NewsButton>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new NewsButton()));

            var buttonsService = new ButtonsDataService(_repoMock.Object);

            var result = await buttonsService.AddParentButton("test", null, new CancellationToken());

            Assert.True(result);
        }
    }
}