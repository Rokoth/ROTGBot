using Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ROTGBot.Contract.Model;
using System.Linq.Dynamic.Core.Tokenizer;
using Telegram.BotAPI;
using Telegram.BotAPI.AvailableMethods;
using Telegram.BotAPI.AvailableTypes;
using Telegram.BotAPI.GettingUpdates;

namespace ROTGBot.Service
{
    public class TelegramMessageHandler : ITelegramMessageHandler
    {
        private const string HelloMessage = "Привет, {0}! Для работы нажмите кнопку меню - Старт или введите /start";

        private readonly ILogger<TelegramMessageHandler> _logger;

        private readonly IGroupsDataService _groupsDataService;
        private readonly IUserDataService _userDataService;
        private readonly INewsDataService _newsDataService;
        private readonly IButtonsDataService _buttonsDataService;
        private readonly ITelegramBotWrapper client;

        private readonly int TimeoutSpan = 10;

        public TelegramMessageHandler(
            ILogger<TelegramMessageHandler> logger,
            IGroupsDataService groupsDataService,
            IUserDataService userDataService,
            INewsDataService newsDataService,
            IButtonsDataService buttonsDataService,
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
        }

        public async Task HandleUpdates(IEnumerable<Update> updates, CancellationToken cancellationToken)
        {            
            ArgumentNullException.ThrowIfNull(updates);

            foreach (var update in updates)
            {
                try
                {
                    await HandleMessage(update.Message, cancellationToken);
                    await HandleCallback(update.CallbackQuery, cancellationToken);
                    await HandleMyChatMember(update.MyChatMember, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка при обработке события");
                }
            }
        }

        private async Task HandleMessage(Message? message, CancellationToken cancellationToken)
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

            var user = await _userDataService.GetOrAddUser(message.From, message.Chat.Id, cancellationToken);

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
                        await HandleData(user.ChatId, user, "SendNews", cancellationToken);
                    }
                }

                if (userNews.Type == "addbutton")
                {
                    await HandleData(user.ChatId, user, "AddButton", cancellationToken);
                }

                if (userNews.Type == "deletebutton")
                {
                    await HandleData(user.ChatId, user, "DeleteButton", cancellationToken);
                }

                if (userNews.Type == "editbutton")
                {
                    await HandleData(user.ChatId, user, "EditButtonApprove", cancellationToken);
                }
            }
            else if (message.IsTopicMessage != true)
            {
                await SendTestConnectionMessage(message, string.Format(HelloMessage, user.Name), cancellationToken);
            }
        }

        private async Task<bool> HandleCallback(CallbackQuery? callbackQuery, CancellationToken token)
        {
            if (callbackQuery == null)
                return false;

            var chatId = callbackQuery.Message?.Chat.Id;
            if (chatId == null)
            {
                return false;
            }

            Contract.Model.User? user = null;

            if (callbackQuery.Message?.Chat?.Type != "private")
            {
                user = await _userDataService.GetOrAddUser(callbackQuery.From, null, token);
            }
            else
            {
                user = await _userDataService.GetOrAddUser(callbackQuery.From, chatId.Value, token);
            }

            if (user == null)
            {
                return false;
            }

            var data = callbackQuery.Data;
            if (data == null) return false;
            var result = await HandleData(user.ChatId, user, data, token);
            await client.AnswerCallbackQueryAsync(new AnswerCallbackQueryArgs(callbackQuery.Id), token: token);

            return result;
        }

        private async Task<bool> HandleData(
            long? chatId,
            Contract.Model.User user,
            string? dataReq,
            CancellationToken token)
        {
            if (dataReq == null || dataReq == "-") return false;

            string data = dataReq;
            Guid? newsId = null;
            int? buttonNumber = null;
            int offset = 0;
            if (data.StartsWith("ApproveNews_") && Guid.TryParse(data.Split("_")[1], out Guid newsId1))
            {
                data = "ApproveNews";
                newsId = newsId1;
            }

            if (data.StartsWith("DeclineNews_") && Guid.TryParse(data.Split("_")[1], out Guid newsId2))
            {
                data = "DeclineNews";
                newsId = newsId2;
            }

            if (data.StartsWith("SendNewsChoice_") && int.TryParse(data.Split("_")[1], out int buttonNumber2))
            {
                data = "SendNewsChoice";
                buttonNumber = buttonNumber2;
            }

            if (data.StartsWith("ApproveNewsChoice_") && int.TryParse(data.Split("_")[1], out int offset1))
            {
                data = "ApproveNewsChoice";
                offset = offset1;
            }

            var roles = user.Roles;
            var userId = user.Id;

            return data switch
            {
                "SwitchNotify" => await SendWithCheckRights(user, chatId.Value, RoleEnum.moderator,
                                        (chId, userNews, tk) => SendSwitchNotifyHandle(chId, user.Id, tk), token),
                "SendNewsChoice" => await SendWithCheckRights(user, chatId.Value, RoleEnum.user,
                                        (chId, userNews, tk) => SendNewsChoiceHandle(chId, user, userNews, buttonNumber.Value, tk), token),
                "SendNews" => await SendWithCheckRights(user, chatId.Value, RoleEnum.user,
                                        (chId, userNews, tk) => SendNewsHandle(userId, chId, userNews, tk), token),
                "SendNewsMulti" => await SendWithCheckRights(user, chatId.Value, RoleEnum.user,
                                        (chId, userNews, tk) => SendNewsMultiHandle(chId, userNews, tk), token),
                "UserReport" => await SendWithCheckRights(user, chatId.Value, RoleEnum.user,
                                        (chId, userNews, tk) => GetUserReportHandle(chId, user, tk), token),
                "ModeratorReport" => await SendWithCheckRights(user, chatId.Value, RoleEnum.user,
                                        (chId, userNews, tk) => GetModeratorReportHandle(chId, user, tk), token),
                "AdminUserReport" => await SendWithCheckRights(user, chatId.Value, RoleEnum.user,
                                        (chId, userNews, tk) => GetAdminUserReportHandle(chId, user, tk), token),
                "AdminModeratorReport" => await SendWithCheckRights(user, chatId.Value, RoleEnum.user,
                                        (chId, userNews, tk) => GetAdminModeratorReportHandle(chId, user, tk), token),
                "DeleteNews" => await SendWithCheckRights(user, chatId.Value, RoleEnum.user,
                                        (chId, userNews, tk) => DeleteNewsHandle(chId, userNews, tk), token),
                "ApproveNewsChoice" => await SendWithCheckRights(user, chatId.Value, RoleEnum.moderator,
                                        (chId, userNews, tk) => SendNewsChoiceApproveHandle(chId, offset, tk), token),
                "ApproveNews" => await SendWithCheckRights(user, chatId.Value, RoleEnum.moderator,
                                        (chId, userNews, tk) => SendNewsApproveHandle(userId, chId, newsId.Value, tk), token),
                "DeclineNews" => await SendWithCheckRights(user, chatId.Value, RoleEnum.moderator,
                                        (chId, userNews, tk) => SendNewsDeclineHandle(userId, chId, newsId.Value, tk), token),
                "AddAdminChoice" => await SendWithCheckRights(user, chatId.Value, RoleEnum.administrator,
                                        (chId, userNews, tk) => SendAddAdminChoiceHandle(chId, user, userNews, tk), token),
                "AddModeratorChoice" => await SendWithCheckRights(user, chatId.Value, RoleEnum.administrator,
                                        (chId, userNews, tk) => SendAddModeratorChoiceHandle(chId, user, userNews, tk), token),
                "EditButtonsChoice" => await SendWithCheckRights(user, chatId.Value, RoleEnum.administrator,
                                        (chId, userNews, tk) => SendEditButtonsChoiceHandle(chId, user, userNews, tk), token),
                "AddButtonChoice" => await SendWithCheckRights(user, chatId.Value, RoleEnum.administrator,
                                        (chId, userNews, tk) => SendAddButtonChoiceHandle(chId, user, userNews, tk), token),
                "GetButtonChoice" => await SendWithCheckRights(user, chatId.Value, RoleEnum.administrator,
                                        (chId, userNews, tk) => SendGetButtonChoiceHandle(chId, user, userNews, tk), token),
                "DeleteButtonChoice" => await SendWithCheckRights(user, chatId.Value, RoleEnum.administrator,
                                        (chId, userNews, tk) => SendDeleteButtonChoiceHandle(chId, user, userNews, tk), token),
                "AddAdmin" => await SendWithCheckRights(user, chatId.Value, RoleEnum.administrator,
                                        (chId, userNews, tk) => AddAdminHandle(userId, chId, userNews, tk), token),
                "AddAdminDecline" => await SendWithCheckRights(user, chatId.Value, RoleEnum.administrator,
                                        (chId, userNews, tk) => AddAdminDeclineHandle(userId, chId, userNews, tk), token),
                "AddModerator" => await SendWithCheckRights(user, chatId.Value, RoleEnum.administrator,
                                        (chId, userNews, tk) => AddModeratorHandle(userId, chId, userNews, tk), token),
                "AddModeratorDecline" => await SendWithCheckRights(user, chatId.Value, RoleEnum.administrator,
                                        (chId, userNews, tk) => AddModeratorDeclineHandle(userId, chId, userNews, tk), token),
                "EditButton" => await SendWithCheckRights(user, chatId.Value, RoleEnum.administrator,
                                        (chId, userNews, tk) => EditButtonHandle(userId, chId, userNews, tk), token),
                "EditButtonApprove" => await SendWithCheckRights(user, chatId.Value, RoleEnum.administrator,
                                        (chId, userNews, tk) => EditButtonApproveHandle(chId, userNews, tk), token),
                "EditButtonDecline" => await SendWithCheckRights(user, chatId.Value, RoleEnum.administrator,
                                        (chId, userNews, tk) => EditButtonDeclineHandle(userId, chId, userNews, tk), token),
                "AddButton" => await SendWithCheckRights(user, chatId.Value, RoleEnum.administrator,
                                        (chId, userNews, tk) => AddButtonHandle(userId, chId, userNews, tk), token),
                "AddButtonDecline" => await SendWithCheckRights(user, chatId.Value, RoleEnum.administrator,
                                        (chId, userNews, tk) => AddButtonDeclineHandle(userId, chId, userNews, tk), token),
                "DeleteButton" => await SendWithCheckRights(user, chatId.Value, RoleEnum.administrator,
                                        (chId, userNews, tk) => DeleteButtonHandle(userId, chId, userNews, tk), token),
                "DeleteButtonDecline" => await SendWithCheckRights(user, chatId.Value, RoleEnum.administrator,
                                        (chId, userNews, tk) => DeleteButtonDeclineHandle(userId, chId, userNews, tk), token),
                "GetPDNOferta" => await SendWithCheckRights(user, chatId.Value, RoleEnum.user,
                                        (chId, userNews, tk) => SendPDNOferta(chId, userNews, tk), token),
                "GetDonateQR" => await SendWithCheckRights(user, chatId.Value, RoleEnum.user,
                                        (chId, userNews, tk) => SendDonateQR(chId, userNews, tk), token),
                "MenuAdmin" => await SendWithCheckRights(user, chatId.Value, RoleEnum.user,
                                        (chId, userNews, tk) => StartCommandHandle(chId, user, userNews, "admin", tk), token),
                "MenuModerator" => await SendWithCheckRights(user, chatId.Value, RoleEnum.user,
                                        (chId, userNews, tk) => StartCommandHandle(chId, user, userNews, "moderator", tk), token),
                "MenuUser" => await SendWithCheckRights(user, chatId.Value, RoleEnum.user,
                                        (chId, userNews, tk) => StartCommandHandle(chId, user, userNews, "user", tk), token),

                _ => await SendWithCheckRights(user, chatId.Value, RoleEnum.user,
                                        (chId, userNews, tk) => SendUserNotImplemented(chId, token), token),
            };
        }

        private async Task<bool> SendWithCheckRights(
            Contract.Model.User user,
            long chatId,
            RoleEnum role,
            Func<long, News?, CancellationToken, Task> succesAction,
            CancellationToken token)
        {
            var result = false;
            var userNews = await _newsDataService.GetCurrentNews(user.Id, token);
            if (!user.Roles.Contains(role))
            {
                await SendUserHasNoRights(chatId, token);
            }
            else
            {
                await succesAction(chatId, userNews, token);
                result = true;
            }

            return result;
        }

        private async Task DeleteNewsHandle( long chatId, News? userNews, CancellationToken token)
        {
            if (userNews != null)
            {
                await DeleteNewsMessageAccepted(chatId, userNews, token);
            }
            else
            {
                await DeleteNewsMessageNotFound(chatId, token);
            }
        }

        private async Task SendNewsHandle(Guid userId, long chatId, News? userNews, CancellationToken token)
        {
            if (userNews != null)
            {
                await SendNewsMessageAccepted(userId, chatId, userNews, token);
            }
            else
            {
                await SendNewsMessageNotFound(chatId, token);
            }
        }

        private async Task GetUserReportHandle( long chatId, Contract.Model.User user, CancellationToken token)
        {
            var report = await _newsDataService.GetUserReport(user.Id, token);
            await client.SendMessageAsync(chatId, $"Отчёт по отправленным Вами обращениям:\r\n {report}", token);
        }

        private async Task GetModeratorReportHandle( long chatId, Contract.Model.User user, CancellationToken token)
        {
            var report = await _newsDataService.GetModeratorReport(user.Id, token);
            await client.SendMessageAsync(chatId, $"Отчёт по обработанным Вами обращениям:\r\n {report}", token);
        }

        private async Task GetAdminUserReportHandle(long chatId, Contract.Model.User user, CancellationToken token)
        {
            var report = await _newsDataService.GetAdminUserReport(token);
            await client.SendMessageAsync(chatId, $"Отчёт по отправленным пользователями обращениям:\r\n {report}", token);
        }

        private async Task GetAdminModeratorReportHandle(long chatId, Contract.Model.User user, CancellationToken token)
        {
            var report = await _newsDataService.GetAdminModeratorReport(token);
            await client.SendMessageAsync(chatId, $"Отчёт по обработанным модераторами обращениям:\r\n {report}", token);
        }

        private async Task SendNewsMultiHandle( long chatId, News? userNews, CancellationToken token)
        {
            if (userNews != null)
            {
                await SetNewsMulti(chatId, userNews, token);
            }
            else
            {
                await SendNewsMessageNotFound(chatId, token);
            }
        }

        private async Task AddAdminHandle( Guid moderatorId, long chatId, News? userNews, CancellationToken token)
        {
            if (userNews != null)
            {
                await AddAdminAccepted(moderatorId, chatId, userNews, token);
            }
            else
            {
                await AddAdminMessageNotFound(chatId, token);
            }
        }

        private async Task EditButtonHandle( Guid moderatorId, long chatId, News? userNews, CancellationToken token)
        {
            if (userNews != null)
            {
                await EditButtonAccepted(moderatorId, chatId, userNews, token);
            }
            else
            {
                await EditButtonMessageNotFound(chatId, token);
            }
        }

        private async Task EditButtonApproveHandle( long chatId, News? userNews, CancellationToken token)
        {
            if (userNews != null)
            {
                await SendEditButtonsForUserApprove(chatId, userNews, token);
            }
            else
            {
                await EditButtonMessageNotFound(chatId, token);
            }
        }

        private async Task AddButtonHandle(Guid userId, long chatId, News? userNews, CancellationToken token)
        {
            if (userNews != null)
            {
                await AddButtonAccepted(userId, chatId, userNews, token);
            }
            else
            {
                await EditButtonMessageNotFound(chatId, token);
            }
        }

        private async Task DeleteButtonHandle(Guid userId, long chatId, News? userNews, CancellationToken token)
        {
            if (userNews != null)
            {
                await DeleteButtonAccepted(userId, chatId, userNews, token);
            }
            else
            {
                await EditButtonMessageNotFound(chatId, token);
            }
        }

        private async Task AddModeratorHandle( Guid moderatorId, long chatId, News? userNews, CancellationToken token)
        {
            if (userNews != null)
            {
                await AddModeratorAccepted(moderatorId, chatId, userNews, token);
            }
            else
            {
                await AddModeratorMessageNotFound(chatId, token);
            }
        }

        private async Task AddAdminDeclineHandle( Guid moderatorId, long chatId, News? userNews, CancellationToken token)
        {
            if (userNews != null)
            {
                await AddAdminModeratorDeclined(moderatorId, chatId, userNews, token);
            }
            else
            {
                await AddAdminMessageNotFound(chatId, token);
            }
        }

        private async Task EditButtonDeclineHandle( Guid moderatorId, long chatId, News? userNews, CancellationToken token)
        {
            if (userNews != null)
            {
                await AddAdminModeratorDeclined(moderatorId, chatId, userNews, token);
            }
            else
            {
                await EditButtonMessageNotFound(chatId, token);
            }
        }

        private async Task AddModeratorDeclineHandle( Guid moderatorId, long chatId, News? userNews, CancellationToken token)
        {
            if (userNews != null)
            {
                await AddAdminModeratorDeclined(moderatorId, chatId, userNews, token);
            }
            else
            {
                await AddModeratorMessageNotFound(chatId, token);
            }
        }

        private async Task AddButtonDeclineHandle( Guid moderatorId, long chatId, News? userNews, CancellationToken token)
        {
            if (userNews != null)
            {
                await AddAdminModeratorDeclined(moderatorId, chatId, userNews, token);
            }
            else
            {
                await EditButtonMessageNotFound(chatId, token);
            }
        }

        private async Task DeleteButtonDeclineHandle(Guid moderatorId, long chatId, News? userNews, CancellationToken token)
        {
            if (userNews != null)
            {
                await AddAdminModeratorDeclined(moderatorId, chatId, userNews, token);
            }
            else
            {
                await EditButtonMessageNotFound(chatId, token);
            }
        }

        private async Task SendPDNOferta( long chatId, News? userNews, CancellationToken token)
        {
            await client.SendMessageAsync(chatId, "Публичная оферта - согласие на обработку персональных данных", token: token);
            using var stream = new FileStream("PDNOferta.txt", FileMode.Open);
            await client.SendDocumentAsync(new SendDocumentArgs(chatId, new InputFile(stream, "PDNOferta.txt")),  token);
        }

        private async Task SendDonateQR( long chatId, News? userNews, CancellationToken token)
        {
            await client.SendMessageAsync(chatId, "Отправить пожертвование можно, используя ссылку", token: token);
            await client.SendMessageAsync(chatId, "https://t.me/c/1627860016/6606/746066", token: token);
            //await client.SendPhotoAsync(new SendPhotoArgs(chatId, ),  token);
        }

        private async Task SendNewsApproveHandle( Guid moderatorId, long chatId, Guid newsId, CancellationToken token)
        {
            var userNews = await _newsDataService.GetNewsById(newsId, token);
            if (userNews != null)
            {
                await SendNewsMessageApproved(moderatorId, chatId, userNews, token);
            }
            else
            {
                await SendNewsMessageNotFound(chatId, token);
            }
        }

        private async Task SendNewsDeclineHandle( Guid moderatorId, long chatId, Guid newsId, CancellationToken token)
        {
            var userNews = await _newsDataService.GetNewsById(newsId, token);
            if (userNews != null)
            {
                await SendNewsMessageDeclined(moderatorId, chatId, userNews, token);
            }
            else
            {
                await SendNewsMessageNotFound(chatId, token);
            }
        }

        private async Task SendNewsChoiceApproveHandle( long chatId, int offset, CancellationToken token)
        {
            var userNewses = await _newsDataService.GetNewsForApprove(token);

            var allCount = userNewses.Count;

            offset = Math.Min(offset, allCount - 1);

            var userNews = userNewses.Skip(offset).FirstOrDefault();

            if (userNews != null)
            {
                await SendNewsMessageForApprove(chatId, userNews, GetExistsPrev(offset), GetExistsNext(offset, allCount), offset, token);
            }
            else
            {
                await ApproveNewsMessageNotFound(chatId, token);
            }
        }

        private static bool GetExistsNext(int offset, int allCount) => offset < allCount - 1;

        private static bool GetExistsPrev(int offset) => offset > 0;

        private async Task SendNewsChoiceHandle( long chatId, Contract.Model.User user, News? userNews, int buttonNumber, CancellationToken token)
        {
            if (userNews != null)
            {
                await SendUserRemember(chatId, userNews, token);
            }
            else
            {
                await SendNewsMessageForUser(chatId, buttonNumber, user, token);
            }
        }

        private async Task SendSwitchNotifyHandle( long chatId, Guid userId, CancellationToken token)
        {
            var isNotify = await _userDataService.SwitchUserNotify(userId, token);

            await client.SendMessageAsync(chatId, $"Уведомления {(isNotify ? "включены" : "выключены")}", token);
        }

        private async Task SendAddAdminChoiceHandle( long chatId, Contract.Model.User user, News? userNews, CancellationToken token)
        {
            if (userNews != null)
            {
                await SendUserRemember(chatId, userNews, token);
            }
            else
            {
                await SendAddAdminForUser(chatId, user, token);
            }
        }

        private async Task SendAddModeratorChoiceHandle( long chatId, Contract.Model.User user, News? userNews, CancellationToken token)
        {
            if (userNews != null)
            {
                await SendUserRemember(chatId, userNews, token);
            }
            else
            {
                await SendAddModeratorForUser(chatId, user, token);
            }
        }

        private async Task SendEditButtonsChoiceHandle( long chatId, Contract.Model.User user, News? userNews, CancellationToken token)
        {
            if (userNews != null)
            {
                await SendUserRemember(chatId, userNews, token);
            }
            else
            {
                await SendEditButtonsForUser(chatId, user, token);
            }
        }

        private async Task SendAddButtonChoiceHandle( long chatId, Contract.Model.User user, News? userNews, CancellationToken token)
        {
            if (userNews != null)
            {
                await SendUserRemember(chatId, userNews, token);
            }
            else
            {
                await SendAddButtonForUser(chatId, user, token);
            }
        }

        private async Task SendGetButtonChoiceHandle( long chatId, Contract.Model.User user, News? userNews, CancellationToken token)
        {
            if (userNews != null)
            {
                await SendUserRemember(chatId, userNews, token);
            }
            else
            {
                await SendGetButtonForUser(chatId, user, token);
            }
        }

        private async Task SendDeleteButtonChoiceHandle( long chatId, Contract.Model.User user, News? userNews, CancellationToken token)
        {
            if (userNews != null)
            {
                await SendUserRemember(chatId, userNews, token);
            }
            else
            {
                await SendDeleteButtonForUser(chatId, user, token);
            }
        }

        private async Task SendNewsMessageAccepted( Guid moderatorId, long chatId, News userNews, CancellationToken token)
        {
            var messages = await _newsDataService.GetNewsMessages(userNews.Id, token);

            if (messages.Count == 0)
            {
                await client.SendMessageAsync(chatId, $"Ваше обращение №{userNews.Number} \"{userNews.Title}\" создано некорректно, отправьте не менее одного сообщения", token: token);
                return;
            }

            if (userNews.IsModerate)
            {
                await _newsDataService.SetNewsAccepted(userNews.Id, token);
                await client.SendMessageAsync(chatId, $"Ваше обращение №{userNews.Number} в раздел \"{userNews.Title}\" принято в обработку", token: token);
                await NotifyModerators(userNews, token);
            }
            else
            {
                await _newsDataService.SetNewsAccepted(userNews.Id, token);
                await _newsDataService.SetNewsApproved(userNews.Id, moderatorId, token);

                if (userNews.GroupId.HasValue)
                {
                    if (messages.Count != 0)
                    {
                        await client.SendMessageAsync(userNews.ChatId, $"Ваше обращение №{userNews.Number} в раздел \"{userNews.Title}\" принято в обработку", token);
                        await SendForwardMessageTitle(userNews, token);
                        await client.ForwardMessagesAsync(userNews.GroupId.Value, userNews.ChatId, messages.Select(s => (int)s.TGMessageId), (int?)userNews.ThreadId, token);
                    }
                    else
                    {
                        await client.SendMessageAsync(userNews.ChatId, $"Ваше обращение №{userNews.Number} в раздел \"{userNews.Title}\" создано некорректно, не отправлено ни одного сообщения. Требуется пересоздание",  token);
                    }
                }
                else
                {
                    await client.SendMessageAsync(userNews.ChatId, $"Ваше обращение №{userNews.Number} в раздел \"{userNews.Title}\" создано некорректно,  не задано направление. Требуется пересоздание",  token);
                }
            }
        }

        private async Task SendForwardMessageTitle( News userNews, CancellationToken token)
        {
            var user = await _userDataService.GetUser(userNews.UserId, token);
            var tgLogin = !string.IsNullOrEmpty(user.TGLogin) ? $"@{user.TGLogin}" : "Не определен";
            var userName = user.Name ?? "Не определен";
            await client.SendMessageAsync(userNews.GroupId.Value, $"Обращение №{userNews.Number} в раздел \"{userNews.Title}\" от пользователя {userName} (логин: {tgLogin})", (int?)userNews.ThreadId,  token);
        }

        private async Task SetNewsMulti( long chatId, News userNews, CancellationToken token)
        {
            await _newsDataService.SetNewsMulti(userNews.Id, token);
            await client.SendMessageAsync(chatId, $"Вашему обращению присвоен номер №{userNews.Number}. " +
                $"Отправьте одно или несколько сообщений, затем нажмите кнопку подтверждения отправки",  token);
        }

        private async Task NotifyModerators( News userNews, CancellationToken token)
        {
            var notifyModerators = await _userDataService.GetNotifyModerators(token);
            foreach (var moder in notifyModerators.Where(s => s.IsModerator))
            {
                await SendNewsMessageForApprove(moder.ChatId, userNews, false, false, 0, token);
            }
        }

        private async Task AddAdminAccepted( Guid moderatorId, long chatId, News userNews, CancellationToken token)
        {
            var messages = await _newsDataService.GetNewsMessages(userNews.Id, token);

            if (messages.Count == 0)
            {
                await client.SendMessageAsync(chatId, "Не отправлено ни одного логина", token);
                return;
            }

            await ParseAndSetRole(messages, RoleEnum.administrator, token);

            await _newsDataService.SetNewsApproved(userNews.Id, moderatorId, token);
            await client.SendMessageAsync(chatId, "Администраторы добавлены", token);
        }

        private async Task ParseAndSetRole(IEnumerable<NewsMessage> messages, RoleEnum role, CancellationToken token)
        {
            foreach (var message in messages)
            {
                var logins = message.TextValue?.Split(",").Select(s => s.Trim()).Select(s => s.TrimStart('@')).Where(s => s != string.Empty);
                if (logins == null || !logins.Any()) continue;

                foreach (var login in logins)
                {
                    await _userDataService.SetRole(login, role, token);
                }
            }
        }

        private async Task EditButtonAccepted( Guid moderatorId, long chatId, News userNews, CancellationToken token)
        {
            var messages = await _newsDataService.GetNewsMessages(userNews.Id, token);

            if (messages.Count == 0)
            {
                await client.SendMessageAsync(chatId, "Не отправлено ни одной кнопки", token);
                return;
            }

            var settings = ParseButtonsSettings(messages);

            if (settings.Count == 0)
            {
                await client.SendMessageAsync(chatId, "Не отправлено ни одной кнопки", token);
                return;
            }

            var groupped = settings.GroupBy(s => s.Number);
            if (groupped.Any(s => s.Count() > 1))
            {
                await client.SendMessageAsync(chatId, "Для некоторых кнопок отправлено больше одной настройки, перезапустите настройку", token);
                return;
            }

            var allButtons = await _buttonsDataService.GetAllButtons(token);

            foreach (var button in allButtons)
            {
                var newItem = settings.FirstOrDefault(s => s.Number == button.ButtonNumber);
                if (newItem != null)
                {
                    await _buttonsDataService.SetButtonSend(button.Id, newItem.Name, null, newItem.IsModerate, token);
                }
                else
                {
                    await _buttonsDataService.RemoveButtonSend(button.Id, token);
                }
            }

            await _newsDataService.SetNewsApproved(userNews.Id, moderatorId, token);
            await client.SendMessageAsync(chatId, "Кнопки сохранены", token);
        }

        private async Task AddButtonAccepted(Guid moderatorId, long chatId, News userNews, CancellationToken token)
        {
            var messages = await _newsDataService.GetNewsMessages(userNews.Id, token);

            if (messages.Count == 0)
            {
                await client.SendMessageAsync(chatId, "Не отправлено ни одной кнопки",  token);
                return;
            }

            if (messages.Count > 1)
            {
                await client.SendMessageAsync(chatId, "Ошибка обработки задания, отмените и попробуйте повторить",  token);
                return;
            }

            var settings = ParseButtonsSettings(messages.FirstOrDefault());

            if (settings == null)
            {
                await client.SendMessageAsync(chatId, "Не отправлено ни одной кнопки",  token);
                return;
            }

            var allButtons = await _buttonsDataService.GetAllButtons(token);

            var button = allButtons.FirstOrDefault(s => settings.Number == s.ButtonNumber);
            if (button != null)
            {
                await _buttonsDataService.SetButtonSend(button.Id, settings.Name, settings.Parent, settings.IsModerate, token);
            }
            else if (settings.IsParent)
            {
                await _buttonsDataService.AddParentButton(settings.Name!, settings.Parent, token);
            }

            await _newsDataService.SetNewsApproved(userNews.Id, moderatorId, token);
            await client.SendMessageAsync(chatId, "Кнопка сохранена",  token);
        }

        private async Task DeleteButtonAccepted(Guid moderatorId, long chatId, News userNews, CancellationToken token)
        {
            var messages = await _newsDataService.GetNewsMessages(userNews.Id, token);

            if (messages.Count == 0)
            {
                await client.SendMessageAsync(chatId, "Не отправлено ни одной кнопки",  token);
                return;
            }

            if (messages.Count > 1)
            {
                await client.SendMessageAsync(chatId, "Ошибка обработки задания, отмените и попробуйте повторить",  token);
                return;
            }

            var settings = ParseButtonsSettings(messages.FirstOrDefault());

            if (settings == null)
            {
                await client.SendMessageAsync(chatId, "Не отправлено ни одной кнопки",  token);
                return;
            }

            var allButtons = await _buttonsDataService.GetAllButtons(token);

            var button = allButtons.FirstOrDefault(s => settings.Number == s.ButtonNumber);
            if (button != null)
            {
                await _buttonsDataService.RemoveButtonSend(button.Id, token);
            }

            await _newsDataService.SetNewsApproved(userNews.Id, moderatorId, token);
            await client.SendMessageAsync(chatId, "Кнопка удалена",  token);
        }

        private static List<ButtonSetting> ParseButtonsSettings(IEnumerable<NewsMessage> messages)
        {
            var buttons = new List<string>();

            foreach (var message in messages.Where(s => s.TextValue != null))
            {
                var values = message.TextValue?.Split(["\r\n", ";"],
                    StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Where(s => s != null && s != string.Empty);

                if (values?.Any() == true)
                {
                    buttons.AddRange(values);
                }
            }

            var numbers = new List<ButtonSetting>();
            foreach (var item in buttons)
            {
                var itemElements = item.Split(":").Select(s => s.Trim()).ToArray();
                if (int.TryParse(itemElements[0], out int num))
                {
                    string? name = null;
                    int? parent = null;
                    bool isModer = false;
                    if (itemElements.Length > 1)
                    {
                        name = itemElements[1];
                    }
                    if (itemElements.Length > 2)
                    {
                        if (int.TryParse(itemElements[2], out int parNum))
                        {
                            parent = parNum;
                        }
                        else if (itemElements[2] == "m")
                        {
                            isModer = true;
                        }
                    }
                    if (itemElements.Length > 3 && itemElements[3] == "m")
                    {
                        isModer = true;
                    }

                    numbers.Add(new ButtonSetting()
                    {
                        Number = num,
                        Name = name,
                        Parent = parent,
                        IsModerate = isModer
                    });
                }
            }

            return numbers;
        }

        private static ButtonSetting? ParseButtonsSettings(NewsMessage? message)
        {

            var value = message?.TextValue?.Trim();

            if (string.IsNullOrEmpty(value))
            {
                return null;
            }

            var itemElements = value.Split(":").Select(s => s.Trim()).ToArray();
            if (int.TryParse(itemElements[0], out int num))
            {
                string? name = null;
                int? parent = null;
                bool isModer = false;
                if (itemElements.Length > 1)
                {
                    name = itemElements[1];
                }
                if (itemElements.Length > 2)
                {
                    if (int.TryParse(itemElements[2], out int parNum))
                    {
                        parent = parNum;
                    }
                    else if (itemElements[2] == "m")
                    {
                        isModer = true;
                    }
                }
                if (itemElements.Length > 3 && itemElements[3] == "m")
                {
                    isModer = true;
                }
                return new ButtonSetting()
                {
                    Number = num,
                    Name = name,
                    Parent = parent,
                    IsModerate = isModer
                };
            }
            else if (itemElements[0] == "_")
            {
                string? name = null;
                int? parent = null;
                if (itemElements.Length > 1)
                {
                    name = itemElements[1];
                }
                else
                {
                    name = "_";
                }
                if (itemElements.Length > 2 && int.TryParse(itemElements[2], out int parNum))
                {
                    parent = parNum;
                }
                return new ButtonSetting()
                {
                    Name = name,
                    Parent = parent,
                    IsParent = true
                };
            }

            return null;
        }

        private async Task AddAdminModeratorDeclined( Guid moderatorId, long chatId, News userNews, CancellationToken token)
        {
            await _newsDataService.SetNewsDeclined(userNews.Id, moderatorId, token);
            await client.SendMessageAsync(chatId, "Задание отменено", token);
        }

        private async Task AddModeratorAccepted( Guid moderatorId, long chatId, News userNews, CancellationToken token)
        {
            var messages = await _newsDataService.GetNewsMessages(userNews.Id, token);

            if (messages.Count == 0)
            {
                await client.SendMessageAsync(chatId, "Не отправлено ни одного логина", token);
                return;
            }

            await ParseAndSetRole(messages, RoleEnum.moderator, token);

            await _newsDataService.SetNewsApproved(userNews.Id, moderatorId, token);
            await client.SendMessageAsync(chatId, "Модераторы добавлены", token);
        }

        private async Task SendNewsMessageNotFound(long chatId, CancellationToken token)
        {
            await client.SendMessageAsync(chatId, "Нет неподтвержденных обращений", token);
        }

        private async Task AddAdminMessageNotFound(long chatId, CancellationToken token)
        {
            await client.SendMessageAsync(chatId, "Нет задач на добавление администратора", token);
        }

        private async Task EditButtonMessageNotFound(long chatId, CancellationToken token)
        {
            await client.SendMessageAsync(chatId, "Нет задач на добавление кнопок", token);
        }

        private async Task AddModeratorMessageNotFound(long chatId, CancellationToken token)
        {
            await client.SendMessageAsync(chatId, "Нет задач на добавление модератора", token);
        }

        private async Task SendUserHasNoRights(long chatId, CancellationToken token)
        {
            await client.SendMessageAsync(chatId, "У вас нет прав на это действие", token);
        }

        private async Task SendUserNotImplemented(long chatId, CancellationToken token)
        {
            await client.SendMessageAsync(chatId, "Действие не реализовано", token);
        }

        private async Task DeleteNewsMessageAccepted(long chatId, News userNews, CancellationToken token)
        {
            await _newsDataService.SetNewsDeleted(userNews.Id, token);
            await client.SendMessageAsync(chatId, $"Обращение №{userNews.Number} \"{userNews.Title}\" удалено", token);
        }

        private async Task DeleteNewsMessageNotFound(long chatId, CancellationToken token)
        {
            await client.SendMessageAsync(chatId, "Нет неподтвержденных обращений", token);
        }

        private async Task ApproveNewsMessageNotFound(long chatId, CancellationToken token)
        {
            await client.SendMessageAsync(chatId, "Нет неотправленных обращений", token);
        }

        private async Task SendNewsMessageForUser( long chatId, int buttonNumber, Contract.Model.User user, CancellationToken token)
        {
            var button = await _buttonsDataService.GetButtonByNumber(buttonNumber, token);

            if (button == null || !button.ToSend)
            {
                await client.SendMessageAsync(chatId, "Недействительное направление обращения, выберите другое", token);
                return;
            }

            if (button.IsParent)
            {
                var buttons = (await _buttonsDataService.GetActiveButtons(token)).Where(s => s.ParentId == buttonNumber);

                if (!buttons.Any())
                {
                    await client.SendMessageAsync(chatId, "Ненастроенная родительская кнопка, выберите другой вариант",  token);
                    return;
                }

                var sendButtons = new List<List<InlineKeyboardButton>>();

                foreach (var childbutton in buttons)
                {
                    var buttonName = childbutton.ButtonName ?? $"{childbutton.ChatName}:{childbutton.ThreadName}";
                    var buttonSend = new InlineKeyboardButton(buttonName)
                    {
                        CallbackData = $"SendNewsChoice_{childbutton.ButtonNumber}"
                    };
                    sendButtons.Add([buttonSend]);
                }

                if (button.ParentId == null)
                {
                    var buttonSend = new InlineKeyboardButton("Вернуться")
                    {
                        CallbackData = $"MenuUser"
                    };
                    sendButtons.Add([buttonSend]);
                }
                else
                {
                    var buttonSend = new InlineKeyboardButton("Вернуться")
                    {
                        CallbackData = $"SendNewsChoice_{button.ParentId}"
                    };
                    sendButtons.Add([buttonSend]);
                }

                ReplyMarkup replyMarkup = new InlineKeyboardMarkup(sendButtons);
                await client.SendMessageAsync(chatId, "Выберите, что хотите сделать", replyMarkup: replyMarkup,  token);
            }
            else
            {
                var span = (int)(DateTime.Now - user.LastSendDate).TotalMinutes;
                if (span < TimeoutSpan)
                {
                    await client.SendMessageAsync(chatId, $"Отправка сообщений ограничена по времени, повторите через {TimeoutSpan - span} минут",  token);
                    return;
                }

                await _newsDataService.CreateNews(chatId, user.Id, button.ChatId, button.ThreadId, "news", $"{GetButtonName(button, false)}", button.IsModerate, token);
                var userNews = await _newsDataService.GetCurrentNews(user.Id, token);
                var sendButtons = new List<List<InlineKeyboardButton>>()
                {
                    new()
                    {
                        new InlineKeyboardButton("Отправить обращение в нескольких сообщениях")
                        {
                            CallbackData = "SendNewsMulti"
                        },
                        new InlineKeyboardButton("Отменить")
                        {
                            CallbackData = "DeleteNews"
                        }
                    }
                };

                ReplyMarkup replyMarkup = new InlineKeyboardMarkup(sendButtons);

                await client.SendMessageAsync(chatId, $"Обращение №{userNews?.Number} в раздел \"{GetButtonName(button, false)}\". Отправьте сообщение, либо нажмите кнопку Отправить обращение в нескольких сообщениях, " +
                    "если требуется отправить несколько сообщений (в данном случае после отправки сообщений необходимо будет подтвердить отправку). " +
                    "Для отмены отправки нажмите Отменить", replyMarkup: replyMarkup,  token);

                await _userDataService.SetUserSendDate(user.Id, token);
            }
        }

        private async Task SendAddAdminForUser( long chatId, Contract.Model.User user, CancellationToken token)
        {
            await _newsDataService.CreateNews(chatId, user.Id, null, null, "addadmin", "Добавление администратора", false, token);

            var button1 = new InlineKeyboardButton("Добавить")
            {
                CallbackData = "AddAdmin"
            };
            ReplyMarkup replyMarkup = new InlineKeyboardMarkup(
                new List<List<InlineKeyboardButton>>()
                {
                    new()
                    {
                        button1
                    }
                });

            await client.SendMessageAsync(chatId,
                "Отправьте по одному логины пользователей, которых надо добавить в администраторы и нажмите кнопку Добавить",
                 replyMarkup,
                 token);
        }

        private async Task SendAddModeratorForUser( long chatId, Contract.Model.User user, CancellationToken token)
        {
            await _newsDataService.CreateNews(chatId, user.Id, null, null, "addmoderator", "Добавление модератора", false, token);

            var button1 = new InlineKeyboardButton("Добавить")
            {
                CallbackData = "AddModerator"
            };
            ReplyMarkup replyMarkup = new InlineKeyboardMarkup(
                new List<List<InlineKeyboardButton>>()
                {
                    new()
                    {
                        button1
                    }
                });

            await client.SendMessageAsync(chatId, 
                "Отправьте по одному логины пользователей, которых надо добавить в модераторы и нажмите кнопку Добавить",
                replyMarkup,
                 token);
        }

        private async Task SendEditButtonsForUser( long chatId, Contract.Model.User user, CancellationToken token)
        {
            var availableButtons = await _buttonsDataService.GetAllButtons(token);
            if (availableButtons.Count != 0)
            {
                await _newsDataService.CreateNews(chatId, user.Id, null, null, "editbutton", "Изменение кнопок", false, token);

                var buttonsView = GetButtonsView(availableButtons);

                await client.SendMessageAsync(chatId,
                    GetAddButtonsRules(buttonsView),
                     token);
            }
            else
            {
                await client.SendMessageAsync(chatId, "Нет доступных кнопок для добавления пользователю. " +
                    "Для добавления доступных кнопок добавьте бота в группу и отправьте в чат одно сообщение (для разбивки по темам - отправьте по одному сообщению в каждой из тем)." +
                    "Пользователь, отправляющий сообщения, должен быть администратором бота.",
                     token);
            }

        }

        private async Task SendEditButtonsForUserApprove( long chatId, News news, CancellationToken token)
        {
            var availableButtons = await _buttonsDataService.GetAllButtons(token);
            if (availableButtons.Count != 0)
            {
                var button1 = new InlineKeyboardButton("Подтвердить")
                {
                    CallbackData = "EditButton"
                };
                var button2 = new InlineKeyboardButton("Отменить")
                {
                    CallbackData = "EditButtonDecline"
                };
                ReplyMarkup replyMarkup = new InlineKeyboardMarkup(
                    new List<List<InlineKeyboardButton>>()
                    {
                    new()
                    {
                        button1, button2
                    }
                    });

                ReplyMarkup replyMarkupError = new InlineKeyboardMarkup(
                    new List<List<InlineKeyboardButton>>()
                    {
                    new()
                    {
                        button2
                    }
                    });

                var buttonsEditResult = await GetButtonsForAccepted(news!, token);

                if (!buttonsEditResult.Item1)
                {
                    await client.SendMessageAsync(chatId, $"При обработке задания произошла ошибка: {buttonsEditResult.Item2}." +
                        $" Повторите сообщение или нажмите кнопку Отмена для отмены задания",
                    replyMarkup: replyMarkupError,  token);
                }

                await client.SendMessageAsync(chatId, $"Будут произведены следующие действия с кнопками:  \n{buttonsEditResult}." +
                    "\nНажмите Подтвердить для сохранения или Отмена для отмены действия.",
                    replyMarkup: replyMarkup,
                     token);
            }
            else
            {
                await client.SendMessageAsync(chatId, "Нет доступных кнопок для добавления пользователю. " +
                    "Для добавления доступных кнопок добавьте бота в группу и отправьте в чат одно сообщение (для разбивки по темам - отправьте по одному сообщению в каждой из тем)." +
                    "Пользователь, отправляющий сообщения, должен быть администратором бота.",
                     token);
            }

        }

        private async Task<(bool, string)> GetButtonsForAccepted(News userNews, CancellationToken token)
        {
            var messages = await _newsDataService.GetNewsMessages(userNews.Id, token);

            if (messages.Count == 0)
            {
                return (false, "Не отправлено ни одной кнопки");
            }

            var settings = ParseButtonsSettings(messages);

            if (settings.Count == 0)
            {
                return (false, "Не отправлено ни одной кнопки");
            }

            var groupped = settings.GroupBy(s => s.Number);
            if (groupped.Any(s => s.Count() > 1))
            {
                return (false, "Для некоторых кнопок отправлено больше одной настройки");
            }

            var allButtons = await _buttonsDataService.GetAllButtons(token);

            List<string> onButtons = [];
            List<string> offButtons = new();

            foreach (var button in allButtons)
            {
                var newItem = settings.FirstOrDefault(s => s.Number == button.ButtonNumber);
                if (newItem != null && !button.ToSend)
                {
                    onButtons.Add($"{newItem.Number} : {newItem.Name}");
                }

                if (newItem == null && button.ToSend)
                {
                    offButtons.Add($"{button.ButtonNumber} : {button.ButtonName}");
                }
            }

            return (true, $"Будут добавлены следующие кнопки: {string.Join(", ", onButtons)}; отключены: {string.Join(", ", offButtons)}.");
        }

        private async Task SendAddButtonForUser( long chatId, Contract.Model.User user, CancellationToken token)
        {
            var availableButtons = await _buttonsDataService.GetAllButtons(token);
            if (availableButtons.Count != 0)
            {
                await _newsDataService.CreateNews(chatId, user.Id, null, null, "addbutton", "Добавление кнопки", false, token);

                var button2 = new InlineKeyboardButton("Отменить")
                {
                    CallbackData = "AddButtonDecline"
                };
                ReplyMarkup replyMarkup = new InlineKeyboardMarkup(
                    new List<List<InlineKeyboardButton>>()
                    {
                    new()
                    {
                        button2
                    }
                    });

                var buttonsView = GetButtonsView(availableButtons);

                await client.SendMessageAsync(chatId,
                    GetAddButtonsRules(buttonsView),
                    replyMarkup: replyMarkup,
                     token);
            }
            else
            {
                await client.SendMessageAsync(chatId, "Нет доступных кнопок для добавления пользователю. " +
                    "Для добавления доступных кнопок добавьте бота в группу и отправьте в чат одно сообщение (для разбивки по темам - отправьте по одному сообщению в каждой из тем)." +
                    "Пользователь, отправляющий сообщения, должен быть администратором бота.",
                     token);
            }

        }

        private async Task SendGetButtonForUser( long chatId, Contract.Model.User user, CancellationToken token)
        {
            var availableButtons = await _buttonsDataService.GetAllButtons(token);
            if (availableButtons.Count != 0)
            {
                var buttonsView = GetButtonsView(availableButtons);

                await client.SendMessageAsync(chatId,
                    GetButtonsRules(buttonsView),
                     token);
            }
            else
            {
                await client.SendMessageAsync(chatId, "Нет доступных кнопок для добавления пользователю. " +
                    "Для добавления доступных кнопок добавьте бота в группу и отправьте в чат одно сообщение (для разбивки по темам - отправьте по одному сообщению в каждой из тем)." +
                    "Пользователь, отправляющий сообщения, должен быть администратором бота.",
                     token);
            }

        }

        private static string? GetButtonsView(List<NewsButton> availableButtons, int? parentId = null, int level = 0)
        {
            var result = availableButtons.Where(s => s.ParentId == parentId);
            if (!result.Any())
                return null;

            return string.Join("\n", result.OrderBy(s => s.ButtonNumber)
                .Select(s => GetGroupView(availableButtons, level, s)));
        }

        private static string GetGroupView(List<NewsButton> availableButtons, int level, NewsButton currentButton)
        {
            string chButtonsView = string.Empty;
            var childButtons = GetButtonsView(availableButtons, currentButton.ButtonNumber, level + 1);
            if (childButtons != null)
            {
                chButtonsView = $"\r\n{GetButtonsView(availableButtons, currentButton.ButtonNumber, level + 1)}";
            }
            return $"{GetTabs(level)}{GetButtonName(currentButton, true)}{chButtonsView}";
        }

        public static string GetTabs(int count)
        {
            var result = "";
            for (int i = 0; i < count; i++)
            {
                result += "\t\t\t\t";
            }
            return result;
        }

        private static string GetButtonsRules(string buttonsView)
        {
            return $"Подключенные и доступные кнопки:  \n{buttonsView}. ";
        }

        private static string GetAddButtonsRules(string buttonsView)
        {
            return $"Подключенные и доступные кнопки:  \n{buttonsView}. \n\n" +
                $"Отправьте по шаблону ({{номер}} или {{номер:Наименование кнопки}}) одну из доступных и не подключенных кнопок для добавления." +
                $"\nЕсли кнопка уже была подключена - изменится ее наименование. \n\n" +
                $"Для добавления группы кнопок (родительской кнопки) отправьте запрос по шаблону {{_:Наименование кнопки}}.\n\n " +
                $"Для добавления доступной кнопки в группу кнопок отправьте запрос по шаблону {{номер:Наименование кнопки:Номер родительской кнопки}}. " +
                $"В качестве родительской могут быть использованы только групповые кнопки. Групповую кнопку также можно добавлять дочерней к другой групповой (родительской) кнопке. \n\n" +
                $"Если необходимо подключить модерацию на одну из кнопок (только для кнопок отправки обращения) - в конце запроса подключения добавьте {{:m}}" +
                $", например: {{номер:Наименование кнопки:Номер родительской кнопки:m}}" +
                $"\n\nЕсли нужных групп или тем нет в списке - " +
                "добавьте бота в группу и отправьте в чат одно сообщение (для разбивки по темам - отправьте по одному сообщению в каждой из тем). " +
                "\nПользователь, отправляющий сообщения, должен быть администратором бота.";
        }

        private static string GetButtonName(NewsButton button, bool withSettings)
        {
            var buttonName = button.ButtonName ?? "";
            if (!string.IsNullOrEmpty(button.ButtonName))
            {
                if (!string.IsNullOrEmpty(button.ChatName))
                {
                    if (!string.IsNullOrEmpty(button.ThreadName))
                    {
                        buttonName = $"{buttonName}({button.ChatName}:{button.ThreadName})";
                    }
                    else
                    {
                        buttonName = $"{buttonName}({button.ChatName})";
                    }
                }
            }
            else
            {
                if (!string.IsNullOrEmpty(button.ChatName))
                {
                    if (!string.IsNullOrEmpty(button.ThreadName))
                    {
                        buttonName = $"{button.ChatName}:{button.ThreadName}";
                    }
                    else
                    {
                        buttonName = $"{button.ChatName}";
                    }
                }
            }

            if (string.IsNullOrEmpty(buttonName))
            {
                buttonName = "Безымянная кнопка";
            }

            if (withSettings)
            {
                return $"{button.ButtonNumber}. {buttonName}. Подключена: {(button.ToSend ? "Да" : "Нет")}. Родительская: {(button.IsParent ? "Да" : "Нет")}";
            }
            else
            {
                return $"{button.ButtonNumber}. {buttonName}";
            }
        }

        private async Task SendDeleteButtonForUser( long chatId, Contract.Model.User user, CancellationToken token)
        {
            var availableButtons = await _buttonsDataService.GetAllButtons(token);
            if (availableButtons.Count != 0)
            {
                await _newsDataService.CreateNews(chatId, user.Id, null, null, "deletebutton", "Удаление кнопки", false, token);

                var button2 = new InlineKeyboardButton("Отменить")
                {
                    CallbackData = "DeleteButtonDecline"
                };
                ReplyMarkup replyMarkup = new InlineKeyboardMarkup(
                    new List<List<InlineKeyboardButton>>()
                    {
                    new()
                    {
                        button2
                    }
                    });


                var buttonsView = GetButtonsView(availableButtons);

                await client.SendMessageAsync(chatId, $"Подключенные и доступные кнопки:  \n{buttonsView}. \n\nОтправьте номер одной из кнопок" +
                    ". \nЕсли кнопка уже была отключена - ничего не произойдёт.",
                     replyMarkup,
                     token);
            }
            else
            {
                await client.SendMessageAsync(chatId, "Нет доступных кнопок для добавления пользователю. " +
                    "Для добавления доступных кнопок добавьте бота в группу и отправьте в чат одно сообщение (для разбивки по темам - отправьте по одному сообщению в каждой из тем)." +
                    "Пользователь, отправляющий сообщения, должен быть администратором бота.",
                     token);
            }

        }

        private async Task SendEditButtonForAdminRemember( long chatId, CancellationToken token)
        {
            var availableButtons = await _buttonsDataService.GetAllButtons(token);
            if (availableButtons.Count != 0)
            {
                var button1 = new InlineKeyboardButton("Сохранить")
                {
                    CallbackData = "EditButton"
                };
                var button2 = new InlineKeyboardButton("Отменить")
                {
                    CallbackData = "EditButtonDecline"
                };
                ReplyMarkup replyMarkup = new InlineKeyboardMarkup(
                    new List<List<InlineKeyboardButton>>()
                    {
                    new()
                    {
                        button1, button2
                    }
                    });

                var buttonsView = GetButtonsView(availableButtons);

                await client.SendMessageAsync(chatId, "У вас есть неподтвержденный запрос на изменение кнопок пользователя." +
                    " Отправьте по шаблону ({номер} или {номер:Наименование кнопки}) одну или несколько настроек (настройки разделяются либо знаком \";\"" +
                    "либо переносом строки либо отправляются в отдельном сообщении)" +
                    " и нажмите кнопку Сохранить, либо Отменить для отмены изменения кнопок", replyMarkup: replyMarkup, token);
            }
            else
            {
                var button2 = new InlineKeyboardButton("Отменить")
                {
                    CallbackData = "EditButtonDecline"
                };
                ReplyMarkup replyMarkup = new InlineKeyboardMarkup(
                    new List<List<InlineKeyboardButton>>()
                    {
                    new()
                    {
                        button2
                    }
                    });

                await client.SendMessageAsync(chatId, "У вас есть неподтвержденный запрос на изменение кнопок пользователя, но нет доступных кнопок для добавления пользователю. " +
                    "Для добавления доступных кнопок добавьте бота в группу и отправьте в чат одно сообщение (для разбивки по темам - отправьте по одному сообщению в каждой из тем)." +
                    "Пользователь, отправляющий сообщения, должен быть администратором бота. Для повторения запроса - нажмите Меню - Старт, для отмены запроса - нажмите Отменить",
                     replyMarkup: replyMarkup,  token);
            }
        }

        private async Task SendAddButtonForAdminRemember( long chatId, CancellationToken token)
        {
            var availableButtons = await _buttonsDataService.GetAllButtons(token);
            if (availableButtons.Count != 0)
            {
                var button2 = new InlineKeyboardButton("Отменить")
                {
                    CallbackData = "AddButtonDecline"
                };
                ReplyMarkup replyMarkup = new InlineKeyboardMarkup(
                    new List<List<InlineKeyboardButton>>()
                    {
                    new()
                    {
                        button2
                    }
                    });

                var buttonsView = GetButtonsView(availableButtons);

                await client.SendMessageAsync(chatId, "У вас есть неподтвержденный запрос на добавление кнопки пользователя." +
                    " Отправьте по шаблону ({номер} или {номер:Наименование кнопки}) одну из кнопок" +
                    " либо нажмите Отменить для отмены изменения кнопок", replyMarkup: replyMarkup,  token);
            }
            else
            {
                var button2 = new InlineKeyboardButton("Отменить")
                {
                    CallbackData = "AddButtonDecline"
                };
                ReplyMarkup replyMarkup = new InlineKeyboardMarkup(
                    new List<List<InlineKeyboardButton>>()
                    {
                    new()
                    {
                        button2
                    }
                    });

                await client.SendMessageAsync(chatId, "У вас есть неподтвержденный запрос на добавление кнопки пользователя, но нет доступных кнопок для добавления пользователю. " +
                    "Для добавления доступных кнопок добавьте бота в группу и отправьте в чат одно сообщение (для разбивки по темам - отправьте по одному сообщению в каждой из тем)." +
                    "Пользователь, отправляющий сообщения, должен быть администратором бота. Для повторения запроса - нажмите Меню - Старт, для отмены запроса - нажмите Отменить",
                     replyMarkup: replyMarkup,  token);
            }
        }

        private async Task SendDeleteButtonForAdminRemember( long chatId, CancellationToken token)
        {
            var availableButtons = await _buttonsDataService.GetAllButtons(token);
            if (availableButtons.Count != 0)
            {
                var button2 = new InlineKeyboardButton("Отменить")
                {
                    CallbackData = "DeleteButtonDecline"
                };
                ReplyMarkup replyMarkup = new InlineKeyboardMarkup(
                    new List<List<InlineKeyboardButton>>()
                    {
                    new()
                    {
                        button2
                    }
                    });

                var buttonsView = GetButtonsView(availableButtons);

                await client.SendMessageAsync(chatId, "У вас есть неподтвержденный запрос на удаление кнопки пользователя." +
                    "Отправьте номер кнопки, которую хотите удалить, либо Отменить для отмены изменения кнопок", replyMarkup: replyMarkup,  token);
            }
            else
            {
                var button2 = new InlineKeyboardButton("Отменить")
                {
                    CallbackData = "EditButtonDecline"
                };
                ReplyMarkup replyMarkup = new InlineKeyboardMarkup(
                    new List<List<InlineKeyboardButton>>()
                    {
                    new()
                    {
                        button2
                    }
                    });

                await client.SendMessageAsync(chatId, "У вас есть неподтвержденный запрос на удаление кнопки пользователя, но нет подключенных кнопок пользователя. " +
                    "Для добавления доступных кнопок добавьте бота в группу и отправьте в чат одно сообщение (для разбивки по темам - отправьте по одному сообщению в каждой из тем)." +
                    "Пользователь, отправляющий сообщения, должен быть администратором бота. Для повторения запроса - нажмите Меню - Старт, для отмены запроса - нажмите Отменить",
                     replyMarkup: replyMarkup,  token);
            }
        }


        private async Task SendNewsMessageForApprove( long chatId, News userNews,
            bool existsPrev, bool existsNext, int currentOffset, CancellationToken token)
        {
            var messages = await _newsDataService.GetNewsMessages(userNews.Id, token);

            if (messages.Count == 0)
            {
                await ClearNews(chatId, userNews, token);
                return;
            }

            var button1 = new InlineKeyboardButton("Подтвердить")
            {
                CallbackData = $"ApproveNews_{userNews.Id}"
            };
            var button2 = new InlineKeyboardButton("Отменить")
            {
                CallbackData = $"DeclineNews_{userNews.Id}"
            };
            var button3 = new InlineKeyboardButton("Предыдущее обращение")
            {
                CallbackData = $"ApproveNewsChoice_{currentOffset - 1}"
            };
            var button4 = new InlineKeyboardButton("Следующее обращение")
            {
                CallbackData = $"ApproveNewsChoice_{currentOffset + 1}"
            };

            List<InlineKeyboardButton> moveButtons = [];

            if (existsPrev) moveButtons.Add(button3);
            if (existsNext) moveButtons.Add(button4);

            var buttons = new List<List<InlineKeyboardButton>>()
            {
                new()
                {
                    button1, button2
                }
            };

            if (moveButtons.Count != 0)
            {
                buttons.Add(moveButtons);
            }

            ReplyMarkup replyMarkup = new InlineKeyboardMarkup(buttons);

            var userButton = await _buttonsDataService.GetButtonByThreadId(userNews.GroupId, userNews.ThreadId, token);

            if (userButton == null)
            {
                await ClearNews(chatId, userNews, token);
                return;
            }
            else
            {
                await client.SendMessageAsync(chatId, $"Обращение №{userNews.Number} для подтверждения в раздел \"{userButton.ChatName} : {userButton.ThreadName} ({userButton.ButtonName})\"",
                     token);
                await client.ForwardMessagesAsync(chatId, userNews.ChatId, messages.Select(s => (int)s.TGMessageId), token);
                await client.SendMessageAsync(chatId, $"Возможные действия с обращением:",
                    replyMarkup: replyMarkup,  token);

            }
        }

        private async Task ClearNews( long chatId, News userNews, CancellationToken token)
        {
            await client.SendMessageAsync(chatId, $"Обращение №{userNews.Number} в раздел \"{userNews.Title}\" для подтверждения создано некорректно, будет удалено", token);
            await client.SendMessageAsync(userNews.ChatId, $"Обращение №{userNews.Number} в раздел \"{userNews.Title}\" создано некорректно, будет удалено", token);
            await _newsDataService.SetNewsDeleted(userNews.Id, token);
        }

        private async Task SendNewsMessageApproved( Guid moderatorId, long chatId, News userNews, CancellationToken token)
        {
            await _newsDataService.SetNewsApproved(userNews.Id, moderatorId, token);

            if (userNews.GroupId.HasValue)
            {


                await client.SendMessageAsync(chatId, $"Обращение №{userNews.Number} в раздел \"{userNews.Title}\" подтверждено", token);
                await client.SendMessageAsync(userNews.ChatId, $"Обращение №{userNews.Number} в раздел \"{userNews.Title}\" подтверждено", token);

                var messages = await _newsDataService.GetNewsMessages(userNews.Id, token);
                if (messages.Count != 0)
                {
                    await SendForwardMessageTitle(userNews, token);
                    await client.ForwardMessagesAsync(userNews.GroupId.Value, userNews.ChatId, messages.Select(s => (int)s.TGMessageId), (int?)userNews.ThreadId, token);
                }
            }
            else
            {
                await client.SendMessageAsync(chatId, $"Нельзя подтвердить обращение №{userNews.Number} в раздел \"{userNews.Title}\": не задано направление. Требуется пересоздание", token);
                await client.SendMessageAsync(userNews.ChatId, $"Нельзя подтвердить обращение №{userNews.Number} в раздел \"{userNews.Title}\": не задано направление. Требуется пересоздание", token);
            }
        }

        private async Task SendNewsMessageDeclined( Guid moderatorId, long chatId, News userNews, CancellationToken token)
        {
            await _newsDataService.SetNewsDeclined(userNews.Id, moderatorId, token);

            await client.SendMessageAsync(chatId, $"Обращение №{userNews.Number} в раздел \"{userNews.Title}\" отклонено", token);
            await client.SendMessageAsync(userNews.ChatId, $"Обращение №{userNews.Number} в раздел \"{userNews.Title}\" отклонено", token);
        }

        private Task SendUserRemember( long chatId, News? news, CancellationToken token)
        {
            return (news?.Type) switch
            {
                "news" => SendNewsMessageForUserRemember(news, chatId, token),
                "addadmin" => SendAddAdminForAdminRemember(chatId, token),
                "addmoderator" => SendAddModeratorForAdminRememeber(chatId, token),
                "editbutton" => SendEditButtonForAdminRemember(chatId, token),
                "addbutton" => SendAddButtonForAdminRemember(chatId, token),
                "deletebutton" => SendDeleteButtonForAdminRemember(chatId, token),
                _ => Task.CompletedTask,
            };
        }

        private async Task SendNewsMessageForUserRemember(News? news, long chatId, CancellationToken token)
        {
            var button1 = new InlineKeyboardButton("Отправить")
            {
                CallbackData = "SendNews"
            };
            var button2 = new InlineKeyboardButton("Отменить")
            {
                CallbackData = "DeleteNews"
            };
            ReplyMarkup replyMarkup = new InlineKeyboardMarkup(
                new List<List<InlineKeyboardButton>>()
                {
                    new()
                    {
                        button1, button2
                    }
                });
            await client.SendMessageAsync(chatId, $"У вас есть неподтвержденное обращение №{news.Number} в раздел \"{news.Title}\"" +
                " Отправьте одно или несколько сообщений и нажмите кнопку Отправить, либо Отменить для отмены отправки",
                replyMarkup, token);
        }

        private async Task SendAddAdminForAdminRemember(long chatId, CancellationToken token)
        {
            var button1 = new InlineKeyboardButton("Добавить")
            {
                CallbackData = "AddAdmin"
            };
            var button2 = new InlineKeyboardButton("Отменить")
            {
                CallbackData = "AddAdminDecline"
            };
            ReplyMarkup replyMarkup = new InlineKeyboardMarkup(
                new List<List<InlineKeyboardButton>>()
                {
                    new()
                    {
                        button1, button2
                    }
                });
            await client.SendMessageAsync(chatId, "У вас есть неподтвержденные пользователи на добавление в администраторы." +
                " Отправьте один или несколько логинов и нажмите кнопку Добавить, либо Отменить для отмены добавления",
                replyMarkup, token);
        }



        private async Task SendAddModeratorForAdminRememeber(long chatId, CancellationToken token)
        {
            var button1 = new InlineKeyboardButton("Добавить")
            {
                CallbackData = "AddModerator"
            };
            var button2 = new InlineKeyboardButton("Отменить")
            {
                CallbackData = "AddModeratorDecline"
            };
            ReplyMarkup replyMarkup = new InlineKeyboardMarkup(
                new List<List<InlineKeyboardButton>>()
                {
                    new()
                    {
                        button1, button2
                    }
                });
            await client.SendMessageAsync(chatId, "У вас есть неподтвержденные пользователи на добавление в модераторы." +
                " Отправьте один или несколько логинов и нажмите кнопку Добавить, либо Отменить для отмены добавления",
                replyMarkup, token);
        }

        private async Task SendTestConnectionMessage(Message message, string addInfo, CancellationToken token)
        {
            await client.SendMessageAsync(message.Chat.Id, addInfo, token);
        }

        private async Task SendMenuButtons( long chatId, Contract.Model.User user, string type, CancellationToken token)
        {
            if (type == "all")
            {
                if (user.IsModerator || user.IsAdmin)
                {
                    await client.SendMessageAsync(chatId, "Выберите раздел",
                        replyMarkup: new InlineKeyboardMarkup(GetMenuButtons(user)),  token);
                }
                else
                {
                    await client.SendMessageAsync(chatId, "Панель пользователя",
                        replyMarkup: new InlineKeyboardMarkup(await GetUserButtons(token)),  token);
                }
            }

            if (type == "user")
            {
                await client.SendMessageAsync(chatId, "Панель пользователя",
                         replyMarkup: new InlineKeyboardMarkup(await GetUserButtons(token)),  token);
            }

            if (type == "moderator")
            {
                if (user.IsModerator)
                {
                    await client.SendMessageAsync(chatId, "Панель модератора",
                        replyMarkup: new InlineKeyboardMarkup(GetModeratorButtons(user)),  token);
                }
                else
                {
                    await client.SendMessageAsync(chatId, "У вас нет доступа к этому разделу",  token);
                }
            }

            if (type == "admin")
            {
                if (user.IsAdmin)
                {
                    await client.SendMessageAsync(chatId, "Панель администратора",
                        replyMarkup: new InlineKeyboardMarkup(GetAdminButtons()),  token);
                }
                else
                {
                    await client.SendMessageAsync(chatId, "У вас нет доступа к этому разделу",  token);
                }
            }
        }

        private async Task<List<List<InlineKeyboardButton>>> GetUserButtons(CancellationToken token)
        {
            var buttons = (await _buttonsDataService.GetActiveButtons(token)).Where(s => s.ParentId == null);

            var sendButtons = new List<List<InlineKeyboardButton>>();

            foreach (var button in buttons)
            {
                var buttonName = button.ButtonName ?? $"{button.ChatName}:{button.ThreadName}";
                var buttonSend = new InlineKeyboardButton(buttonName)
                {
                    CallbackData = $"SendNewsChoice_{button.ButtonNumber}"
                };
                sendButtons.Add([buttonSend]);
            }

            sendButtons.Add(EmptyButton());

            sendButtons.Add([new InlineKeyboardButton("Отчёт по отправленным обращениям")
                {
                    CallbackData = "UserReport"
                }]);

            sendButtons.Add(EmptyButton());

            sendButtons.Add([new InlineKeyboardButton("Согласие-оферта на обработку персональных данных")
            {
                CallbackData = "GetPDNOferta"
            }]);

            sendButtons.Add(EmptyButton());

            sendButtons.Add([new InlineKeyboardButton("Отправить пожертвование")
            {
                CallbackData = "GetDonateQR"
            }]);

            return sendButtons;
        }

        private static List<InlineKeyboardButton> EmptyButton(string? text = null)
        {
            return [new InlineKeyboardButton(text ?? "* * *")
            {
                CallbackData = "-"
            }];
        }

        private static List<List<InlineKeyboardButton>> GetAdminButtons()
        {
            return
            [
                [
                    new InlineKeyboardButton("Добавить администратора")
                    {
                        CallbackData = "AddAdminChoice"
                    },new InlineKeyboardButton("Добавить модератора")
                    {
                        CallbackData = "AddModeratorChoice"
                    }
                ],
                EmptyButton(),
                [
                    new InlineKeyboardButton("Управление кнопками пользователя (множественное)")
                    {
                        CallbackData = "EditButtonsChoice"
                    }
                ],
                [
                    new InlineKeyboardButton("Просмотр кнопок пользователя")
                    {
                        CallbackData = "GetButtonChoice"
                    }
                ],
                [
                    new InlineKeyboardButton("Добавить кнопку пользователя")
                    {
                        CallbackData = "AddButtonChoice"
                    }
                ],
                [
                    new InlineKeyboardButton("Удалить кнопку пользователя")
                    {
                        CallbackData = "DeleteButtonChoice"
                    }
                ],
                EmptyButton(),                
                [
                    new InlineKeyboardButton("Отчёт по обработанным обращениям пользователей")
                    {
                        CallbackData = "AdminUserReport"
                    }
                ],
                [
                    new InlineKeyboardButton("Отчёт по обработанным обращениям модераторов")
                    {
                        CallbackData = "AdminModeratorReport"
                    }
                ]
            ];
        }

        private static List<List<InlineKeyboardButton>> GetModeratorButtons(Contract.Model.User user)
        {
            var switchNotify = "Включить уведомления";
            if (user.IsNotify)
            {
                switchNotify = "Отключить уведомления";
            }

            return [
                [ new InlineKeyboardButton("Получить обращение для подтверждения")
                {
                    CallbackData = "ApproveNewsChoice_0"
                }],
                [ new InlineKeyboardButton(switchNotify)
                {
                    CallbackData = "SwitchNotify"
                }],
                [new InlineKeyboardButton("Отчёт по обработанным обращениям")
                {
                    CallbackData = "ModeratorReport"
                }]
            ];
        }

        private static List<List<InlineKeyboardButton>> GetMenuButtons(Contract.Model.User user)
        {
            List<List<InlineKeyboardButton>> result = [];
            if (user.IsAdmin)
            {
                result.Add([ new InlineKeyboardButton("Панель администратора")
                {
                    CallbackData = "MenuAdmin"
                }]);
            }
            if (user.IsAdmin)
            {
                result.Add([ new InlineKeyboardButton("Панель модератора")
                {
                    CallbackData = "MenuModerator"
                }]);
            }
            result.Add([ new InlineKeyboardButton("Панель пользователя")
                {
                    CallbackData = "MenuUser"
                }]);

            return result;
        }

        private async Task HandleMyChatMember(ChatMemberUpdated? myChatMember, CancellationToken cancellationToken)
        {
            if (myChatMember == null)
                return;

            var chatId = myChatMember.Chat.Id;
            var description = $"{myChatMember.Chat.Title} : {myChatMember.Chat.FirstName} {myChatMember.Chat.LastName} (@{myChatMember.Chat.Username})";
            var title = myChatMember.Chat.Title;

            await _groupsDataService.AddGroupIfNotExists(chatId, title, description, cancellationToken);
        }

        private async Task StartCommandHandle( long chatId, Contract.Model.User user, News? userNews, string type, CancellationToken cancellationToken)
        {
            if (userNews != null)
            {
                await SendUserRemember(chatId, userNews, cancellationToken);
            }
            else
            {
                await SendMenuButtons(chatId, user, type, cancellationToken);
            }
        }
    }
}
