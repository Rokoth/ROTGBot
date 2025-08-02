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

            var user = await _userDataService.GetOrAddUser(message.From, message.Chat.Id, cancellationToken);
            var userNews = await _newsDataService.GetCurrentNews(user.Id, cancellationToken);

            if (user.IsAdmin && message.IsTopicMessage == true)
            {
                await _buttonsDataService.AddNewButton(message, cancellationToken);
            }

            if (message.Text == "/start")
            {
                await StartCommandHandle( message, user, userNews, cancellationToken);
            }
            else if (userNews != null)
            {
                await _newsDataService.AddNewMessageForNews(message.MessageId, userNews.Id, message.Text ?? "", cancellationToken);
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

            var user = await _userDataService.GetOrAddUser(callbackQuery.From, chatId.Value, token);

            var data = callbackQuery.Data;
            if (data == null) return false;
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
                "SwitchNotify" => await SendWithCheckRights(user, chatId.Value, callbackQuery.Id, RoleEnum.moderator,
                                        (chId, userNews, tk) => SendSwitchNotifyHandle(chId, user.Id, tk), token),
                "SendNewsChoice" => await SendWithCheckRights(user, chatId.Value, callbackQuery.Id, RoleEnum.user,
                                        (chId, userNews, tk) => SendNewsChoiceHandle(chId, user, userNews, buttonNumber.Value, tk), token),
                "SendNews" => await SendWithCheckRights(user, chatId.Value, callbackQuery.Id, RoleEnum.user,
                                        (chId, userNews, tk) => SendNewsHandle(chId, userNews, tk), token),
                "UserReport" => await SendWithCheckRights(user, chatId.Value, callbackQuery.Id, RoleEnum.user,
                                        (chId, userNews, tk) => GetUserReportHandle(chId, user, tk), token),
                "ModeratorReport" => await SendWithCheckRights(user, chatId.Value, callbackQuery.Id, RoleEnum.user,
                                        (chId, userNews, tk) => GetModeratorReportHandle(chId, user, tk), token),
                "DeleteNews" => await SendWithCheckRights(user, chatId.Value, callbackQuery.Id, RoleEnum.user,
                                        (chId, userNews, tk) => DeleteNewsHandle(chId, userNews, tk), token),
                "ApproveNewsChoice" => await SendWithCheckRights(user, chatId.Value, callbackQuery.Id, RoleEnum.moderator,
                                        (chId, userNews, tk) => SendNewsChoiceApproveHandle(chId, offset, tk), token),
                "ApproveNews" => await SendWithCheckRights(user, chatId.Value, callbackQuery.Id, RoleEnum.moderator,
                                        (chId, userNews, tk) => SendNewsApproveHandle(userId, chId, newsId.Value, tk), token),
                "DeclineNews" => await SendWithCheckRights(user, chatId.Value, callbackQuery.Id, RoleEnum.moderator,
                                        (chId, userNews, tk) => SendNewsDeclineHandle(userId, chId, newsId.Value, tk), token),
                "AddAdminChoice" => await SendWithCheckRights(user, chatId.Value, callbackQuery.Id, RoleEnum.administrator,
                                        (chId, userNews, tk) => SendAddAdminChoiceHandle(chId, user, userNews, tk), token),
                "AddModeratorChoice" => await SendWithCheckRights(user, chatId.Value, callbackQuery.Id, RoleEnum.administrator,
                                        (chId, userNews, tk) => SendAddModeratorChoiceHandle(chId, user, userNews, tk), token),
                "EditButtonChoice" => await SendWithCheckRights(user, chatId.Value, callbackQuery.Id, RoleEnum.administrator,
                                        (chId, userNews, tk) => SendEditButtonChoiceHandle(chId, user, userNews, tk), token),
                "AddAdmin" => await SendWithCheckRights(user, chatId.Value, callbackQuery.Id, RoleEnum.administrator,
                                        (chId, userNews, tk) => AddAdminHandle(userId, chId, userNews, tk), token),
                "AddAdminDecline" => await SendWithCheckRights(user, chatId.Value, callbackQuery.Id, RoleEnum.administrator,
                                        (chId, userNews, tk) => AddAdminDeclineHandle(userId, chId, userNews, tk), token),
                "AddModerator" => await SendWithCheckRights(user, chatId.Value, callbackQuery.Id, RoleEnum.administrator,
                                        (chId, userNews, tk) => AddModeratorHandle(userId, chId, userNews, tk), token),
                "AddModeratorDecline" => await SendWithCheckRights(user, chatId.Value, callbackQuery.Id, RoleEnum.administrator,
                                        (chId, userNews, tk) => AddModeratorDeclineHandle(userId, chId, userNews, tk), token),
                "EditButton" => await SendWithCheckRights(user, chatId.Value, callbackQuery.Id, RoleEnum.administrator,
                                        (chId, userNews, tk) => EditButtonHandle(userId, chId, userNews, tk), token),
                "EditButtonDecline" => await SendWithCheckRights(user, chatId.Value, callbackQuery.Id, RoleEnum.administrator,
                                        (chId, userNews, tk) => EditButtonDeclineHandle(userId, chId, userNews, tk), token),
                _ => await SendWithCheckRights(user, chatId.Value, callbackQuery.Id, RoleEnum.user,
                                        (chId, userNews, tk) => SendUserNotImplemented(chId, token), token),
            };
        }

        private async Task<bool> SendWithCheckRights(           
            Contract.Model.User user,
            long chatId,
            string callbackQueryId,
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
            await client.AnswerCallbackQueryAsync(new AnswerCallbackQueryArgs(callbackQueryId), token);

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

        private async Task SendNewsHandle( long chatId, News? userNews, CancellationToken token)
        {
            if (userNews != null)
            {
                await SendNewsMessageAccepted(chatId, userNews, token);
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
            await client.SendMessageAsync(chatId, $"Отчёт по обработанным Вами обращениям\r\n: {report}", token);
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

        private async Task SendEditButtonChoiceHandle( long chatId, Contract.Model.User user, News? userNews, CancellationToken token)
        {
            if (userNews != null)
            {
                await SendUserRemember(chatId, userNews, token);
            }
            else
            {
                await SendEditButtonForUser(chatId, user, token);
            }
        }

        private static ReplyParameters? GetReplyParameters(int? messageId)
        {
            if (messageId == null) return null;
            return new ReplyParameters()
            {
                MessageId = messageId.Value
            };
        }

        private async Task SendNewsMessageAccepted( long chatId, News userNews, CancellationToken token)
        {
            var messages = await _newsDataService.GetNewsMessages(userNews.Id, token);

            if (messages.Count == 0)
            {
                await client.SendMessageAsync(chatId, "Обращение создано некорректно, отправьте не менее одного сообщения", token);
                return;
            }

            await _newsDataService.SetNewsAccepted(userNews.Id, token);
            await client.SendMessageAsync(chatId, "Обращение принято в обработку", token);
            await NotifyModerators(userNews, token);
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
                    await _buttonsDataService.SetButtonSend(button.Id, newItem.Name, token);
                }
                else
                {
                    await _buttonsDataService.RemoveButtonSend(button.Id, token);
                }
            }

            await _newsDataService.SetNewsApproved(userNews.Id, moderatorId, token);
            await client.SendMessageAsync(chatId, "Кнопки сохранены", token);
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
                    if (itemElements.Length > 1)
                    {
                        name = itemElements[1];
                    }
                    numbers.Add(new ButtonSetting()
                    {
                        Number = num,
                        Name = name
                    });
                }
            }

            return numbers;
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
            await client.SendMessageAsync(chatId, "Обращение удалено", token);
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

            await _newsDataService.CreateNews(chatId, user.Id, button.ChatId, button.ThreadId, "news", "Новое обращение", token);

            var sendButtons = new List<List<InlineKeyboardButton>>(){new()
                {
                    new InlineKeyboardButton("Отправить обращение")
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

            await client.SendMessageAsync(chatId, "Отправьте одно или несколько сообщений и нажмите кнопку Отправить", replyMarkup, token);
        }

        private async Task SendAddAdminForUser( long chatId, Contract.Model.User user, CancellationToken token)
        {
            await _newsDataService.CreateNews(chatId, user.Id, null, null, "addadmin", "Добавление администратора", token);

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
            await _newsDataService.CreateNews(chatId, user.Id, null, null, "addmoderator", "Добавление модератора", token);

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

        private async Task SendEditButtonForUser( long chatId, Contract.Model.User user, CancellationToken token)
        {
            var availableButtons = await _buttonsDataService.GetAllButtons(token);
            if (availableButtons.Count != 0)
            {
                await _newsDataService.CreateNews(chatId, user.Id, null, null, "editbutton", "Изменение кнопок", token);

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


                var buttonsView = string.Join("\n", availableButtons.OrderBy(s => s.ButtonNumber).Select(s => $"{s.ButtonNumber}. {s.ChatName}:{s.ThreadName}. Подключена: {(s.ToSend ? "Да" : "Нет")}"));

                await client.SendMessageAsync(chatId, $"Подключенные и доступные кнопки:  \n{buttonsView}. \n\nОтправьте по шаблону ({{номер}} или {{номер:Наименование кнопки}}) одну " +
                    "или несколько настроек (настройки разделяются либо знаком \";\", либо переносом строки, либо отправляются в отдельном сообщении)" +
                    " и нажмите кнопку Сохранить. \nПодключенные кнопки, которые вы не укажете, будут отключены. Если нужных групп или тем нет в списке - " +
                    "добавьте бота в группу и отправьте в чат одно сообщение (для разбивки по темам - отправьте по одному сообщению в каждой из тем). \nПользователь, отправляющий сообщения, должен быть администратором бота.",
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

                var buttonsView = string.Join("\r\n", availableButtons.OrderBy(s => s.ButtonNumber).Select(s => $"{s.ButtonNumber}. {s.ChatName}:{s.ThreadName}. Подключена: {(s.ToSend ? "Да" : "Нет")}"));

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
                     replyMarkup: replyMarkup, token);
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
                await client.SendMessageAsync(chatId, $"Обращение для подтверждения в раздел \"{userButton.ChatName} : {userButton.ThreadName} ({userButton.ButtonName})\"", replyMarkup: replyMarkup, token);
                await client.ForwardMessagesAsync(chatId, userNews.ChatId, messages.Select(s => (int)s.TGMessageId), token);
            }
        }

        private async Task ClearNews( long chatId, News userNews, CancellationToken token)
        {
            await client.SendMessageAsync(chatId, "Обращение для подтверждения создано некорректно, будет удалено", token);
            await client.SendMessageAsync(userNews.ChatId, "Обращение создано некорректно, будет удалено", token);
            await _newsDataService.SetNewsDeleted(userNews.Id, token);
        }

        private async Task SendNewsMessageApproved( Guid moderatorId, long chatId, News userNews, CancellationToken token)
        {
            await _newsDataService.SetNewsApproved(userNews.Id, moderatorId, token);

            if (userNews.GroupId.HasValue)
            {
                await client.SendMessageAsync(chatId, "Обращение подтверждено", token);
                await client.SendMessageAsync(userNews.ChatId, "Обращение подтверждено", token);

                var messages = await _newsDataService.GetNewsMessages(userNews.Id, token);
                if (messages.Count != 0)
                {
                    await client.ForwardMessagesAsync(userNews.GroupId.Value, userNews.ChatId, messages.Select(s => (int)s.TGMessageId), (int?)userNews.ThreadId, token);
                }
            }
            else
            {
                await client.SendMessageAsync(chatId, "Нельзя подтвердить обращение: не задано направление. Требуется пересоздание", token);
                await client.SendMessageAsync(userNews.ChatId, "Нельзя подтвердить обращение: не задано направление. Требуется пересоздание", token);
            }
        }

        private async Task SendNewsMessageDeclined( Guid moderatorId, long chatId, News userNews, CancellationToken token)
        {
            await _newsDataService.SetNewsDeclined(userNews.Id, moderatorId, token);

            await client.SendMessageAsync(chatId, "Обращение отклонено", token);
            await client.SendMessageAsync(userNews.ChatId, "Обращение отклонено", token);
        }

        private Task SendUserRemember( long chatId, News? news, CancellationToken token)
        {
            return (news?.Type) switch
            {
                "news" => SendNewsMessageForUserRemember(chatId, token),
                "addadmin" => SendAddAdminForAdminRemember(chatId, token),
                "addmoderator" => SendAddModeratorForAdminRememeber(chatId, token),
                "editbutton" => SendEditButtonForAdminRemember(chatId, token),
                _ => Task.CompletedTask,
            };
        }

        private async Task SendNewsMessageForUserRemember(long chatId, CancellationToken token)
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
            await client.SendMessageAsync(chatId, "У вас есть неподтвержденное обращение." +
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

        private async Task SendMenuButtons( long chatId, Contract.Model.User user, CancellationToken token)
        {

            var buttons = new List<List<InlineKeyboardButton>>();

            if (user.IsModerator) buttons.AddRange(await GetModeratorButtons(user));

            if (user.IsAdmin) buttons.AddRange(GetAdminButtons());

            buttons.AddRange(await GetUserButtons(token));

            ReplyMarkup replyMarkup = new InlineKeyboardMarkup(buttons);
            await client.SendMessageAsync(chatId, "Выберите, что хотите сделать", replyMarkup: replyMarkup, token);
        }

        private async Task<List<List<InlineKeyboardButton>>> GetUserButtons(CancellationToken token)
        {
            var buttons = await _buttonsDataService.GetActiveButtons(token);

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

            sendButtons.Add([new InlineKeyboardButton("Отчёт по отправленным обращениям")
                {
                    CallbackData = "UserReport"
                }]);

            return sendButtons;
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
                [
                    new InlineKeyboardButton("Управление кнопками пользователя")
                    {
                        CallbackData = "EditButtonChoice"
                    }
                ]
            ];
        }

        private async static Task<List<List<InlineKeyboardButton>>> GetModeratorButtons(Contract.Model.User user)
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



        private async Task HandleMyChatMember(ChatMemberUpdated? myChatMember, CancellationToken cancellationToken)
        {
            if (myChatMember == null)
                return;

            var chatId = myChatMember.Chat.Id;
            var description = $"{myChatMember.Chat.Title} : {myChatMember.Chat.FirstName} {myChatMember.Chat.LastName} (@{myChatMember.Chat.Username})";
            var title = myChatMember.Chat.Title;

            await _groupsDataService.AddGroupIfNotExists(chatId, title, description, cancellationToken);
        }

        private async Task StartCommandHandle(Message message, Contract.Model.User user, News? userNews, CancellationToken cancellationToken)
        {
            if (userNews != null)
            {
                await SendUserRemember(message.Chat.Id, userNews, cancellationToken);
            }
            else
            {
                await SendMenuButtons(message.Chat.Id, user, cancellationToken);
            }
        }
    }
}
