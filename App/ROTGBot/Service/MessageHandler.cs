using Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ROTGBot.Contract.Model;
using Telegram.BotAPI.AvailableTypes;

namespace ROTGBot.Service
{
    public class MessageHandler : IHandler<Message>
    {
        private readonly ILogger<MessageHandler> _logger;

        private readonly IGroupsDataService _groupsDataService;
        private readonly IUserDataService _userDataService;
        private readonly INewsDataService _newsDataService;
        private readonly IButtonsDataService _buttonsDataService;
        private readonly ITelegramBotWrapper client;
        private readonly IDataHandler _dataHandler;
        private readonly ISendMessageWrapper _sendMessageWrapper;

        private const string HelloMessage = "Привет, {0}! Для работы нажмите кнопку меню - Старт или введите /start";

        private readonly int TimeoutSpan = 10;

        public MessageHandler(
            ILogger<MessageHandler> logger,
            IGroupsDataService groupsDataService,
            IUserDataService userDataService,
            INewsDataService newsDataService,
            IButtonsDataService buttonsDataService,
            IDataHandler dataHandler,
            ISendMessageWrapper sendMessageWrapper,
            IConfiguration configuration,
            ITelegramBotWrapper wrapper)
        {
            _logger = logger;
            _groupsDataService = groupsDataService;
            _userDataService = userDataService;
            _newsDataService = newsDataService;
            _buttonsDataService = buttonsDataService;
            var botSettings = configuration.GetSection("BotSettings").Get<BotSettings>();
            client = wrapper;
            _dataHandler = dataHandler;
            _sendMessageWrapper = sendMessageWrapper;
        }


        public async Task Handle(Message? message, CancellationToken cancellationToken)
        {
            if (message == null)
                return;

            _logger.LogInformation("Message update: {name}. {message}", message.Chat.Username, message.Text);

            if (message.From == null)
            {
                await SendTestConnectionMessage(message, "Не удалось получить информацию по отправителю", cancellationToken);
                return;
            }

            if (message.Chat?.Id == null || message.Chat?.Type != "private")
            {
                return;
            }

            var tgUser = message.From;

            var user = await _userDataService.GetOrAddUser(tgUser.Id, tgUser.Username ?? "NoName", $"{tgUser.FirstName} {tgUser.LastName} (@{tgUser.Username})", message.Chat.Id, cancellationToken);

            if (user == null)
                return;

            var userNews = await _newsDataService.GetCurrentNews(user.Id, cancellationToken);

            if (user.IsAdmin && message.IsTopicMessage == true)
            {
                await _buttonsDataService.AddNewButton(
                    message.Chat.Id,
                    message.MessageThreadId,
                    message.Chat.Title ?? $"{message.Chat.FirstName} {message.Chat.LastName}",
                    (message.ForumTopicCreated ?? message.ReplyToMessage?.ForumTopicCreated)?.Name,
                    cancellationToken);
            }

            if (message.Text == "/start")
            {
                await StartCommandHandle(user.ChatId, user, userNews, "all", cancellationToken);
            }
            else if (userNews != null)
            {
                await _newsDataService.AddNewMessageForNews(message.MessageId, userNews.Id, message.Text ?? "", cancellationToken);

                if (userNews.Type == "news")
                {
                    if (userNews.IsMulti)
                    {
                        var sendButtons = new List<List<InlineKeyboardButton>>()
                        {
                            new()
                            {
                                new InlineKeyboardButton("Подтвердить отправку")
                                {
                                    CallbackData = "SendNews"
                                },
                                new InlineKeyboardButton("Отменить")
                                {
                                    CallbackData = "DeleteNews"
                                }
                            }
                        };

                        ReplyMarkup replyMarkup = new InlineKeyboardMarkup(sendButtons);

                        await client.SendMessageAsync(user.ChatId,
                            "Сообщение принято. Вы можете отправить ещё одно или несколько сообщений, или нажмите кнопку Подтвердить отправку, если отправили все нужные данные; " +
                            "для отмены отправки нажмите Отменить.",
                            replyMarkup: replyMarkup, token: cancellationToken);
                    }
                    else
                    {
                        await _dataHandler.HandleData(user.ChatId, user, "SendNews", cancellationToken);
                    }
                }

                if (userNews.Type == "addbutton")
                {
                    await _dataHandler.HandleData(user.ChatId, user, "AddButton", cancellationToken);
                }

                if (userNews.Type == "deletebutton")
                {
                    await _dataHandler.HandleData(user.ChatId, user, "DeleteButton", cancellationToken);
                }

                if (userNews.Type == "editbutton")
                {
                    await _dataHandler.HandleData(user.ChatId, user, "EditButtonApprove", cancellationToken);
                }
            }
            else if (message.IsTopicMessage != true)
            {
                await SendTestConnectionMessage(message, string.Format(HelloMessage, user.Name), cancellationToken);
            }
        }

        private async Task SendTestConnectionMessage(Message message, string addInfo, CancellationToken token)
        {
            await client.SendMessageAsync(message.Chat.Id, addInfo, token);
        }

        private async Task StartCommandHandle(long chatId, Contract.Model.User user, News? userNews, string type, CancellationToken cancellationToken)
        {
            if (userNews != null)
            {
                await _sendMessageWrapper.SendUserRemember(chatId, userNews, cancellationToken);
            }
            else
            {
                await _sendMessageWrapper.SendMenuButtons(chatId, user, type, cancellationToken);
            }
        }
    }
}
