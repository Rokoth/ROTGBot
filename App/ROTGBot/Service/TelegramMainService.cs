using Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ROTGBot.Contract.Model;
using Telegram.BotAPI;
using Telegram.BotAPI.AvailableMethods;
using Telegram.BotAPI.AvailableTypes;
using Telegram.BotAPI.GettingUpdates;

namespace ROTGBot.Service
{
    public class TelegramMainService : ITelegramMainService
    {
        private readonly string botToken = "token";

        private const string HelloMessage = "Привет, {0}! Для работы нажмите кнопку меню - Старт или введите /start";

        private readonly ILogger<TelegramMainService> _logger;
                               
        private readonly IGroupsDataService _groupsDataService;
        private readonly IUserDataService _userDataService;
        private readonly INewsDataService _newsDataService;
        private readonly IButtonsDataService _buttonsDataService;
        

        public TelegramMainService(
            ILogger<TelegramMainService> logger,           
            IGroupsDataService groupsDataService,
            IUserDataService userDataService,
            INewsDataService newsDataService,
            IButtonsDataService buttonsDataService,
            IConfiguration configuration)
        {
            _logger = logger;                       
            _groupsDataService = groupsDataService;
            _userDataService = userDataService;
            _newsDataService = newsDataService;
            _buttonsDataService = buttonsDataService;
            var botSettings = configuration.GetSection("BotSettings").Get<BotSettings>();
            botToken = botSettings?.Token ?? botToken;
        }

        public async Task<int> Execute(int offset)
        {
            var cancellationToken = new CancellationTokenSource(60000).Token;
            var client = new TelegramBotClient(botToken);

            var updates = await client.GetUpdatesAsync(offset);
            if ((updates?.Any()) != true)
            {
                return offset;
            }

            await HandleUpdates(client, updates, cancellationToken);

            return updates.Last().UpdateId + 1;
        }

        private async Task HandleUpdates(TelegramBotClient client, IEnumerable<Update> updates, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(client);
            ArgumentNullException.ThrowIfNull(updates);

            foreach (var update in updates)
            {
                try
                {
                    await HandleMessage(client, update.Message, cancellationToken);
                    await HandleCallback(client, update.CallbackQuery, cancellationToken);
                    await HandleMyChatMember(update.MyChatMember, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка при обработке события");
                }
            }
        }
                
        private async Task HandleMessage(TelegramBotClient client, Message? message, CancellationToken cancellationToken)
        {
            if (message == null)
                return;

            _logger.LogInformation("Message update: {name}. {message}", message.Chat.Username, message.Text);

            var reply = GetReplyParameters(message.MessageId);

            if (message.From == null)
            {
                await SendTestConnectionMessage(client, message, "Не удалось получить информацию по отправителю", reply, cancellationToken);
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
                await StartCommandHandle(client, message, reply, user, userNews, cancellationToken);
            }
            else if (userNews != null)
            {
                await _newsDataService.AddNewMessageForNews(message.MessageId, userNews.Id, message.Text ?? "", cancellationToken);
            }
            else if (message.IsTopicMessage != true)
            {               
                await SendTestConnectionMessage(client, message, string.Format(HelloMessage, user.Name), reply, cancellationToken);
            }
        }

        private async Task<bool> HandleCallback(TelegramBotClient client, CallbackQuery? callbackQuery, CancellationToken token)
        {
            if (callbackQuery == null)
                return false;

            var chatId = callbackQuery.Message?.Chat.Id;
            if (chatId == null)
            {
                return false;
            }
           
            var user = await _userDataService.GetOrAddUser(callbackQuery.From, chatId.Value, token);
           
            var replyPrams = GetReplyParameters(callbackQuery.Message?.MessageId);
                       
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

            return data switch
            {
                "SwitchNotify" => await SendWithCheckRights(client, user, chatId.Value, replyPrams, callbackQuery.Id, RoleEnum.moderator,
                                        (cl, chId, rpl, userNews, tk) => SendSwitchNotifyHandle(cl, chId, user.Id, rpl, tk), token),
                "SendNewsChoice" => await SendWithCheckRights(client, user, chatId.Value, replyPrams, callbackQuery.Id, RoleEnum.user,
                                        (cl, chId, rpl, userNews, tk) => SendNewsChoiceHandle(cl, chId, user, userNews, buttonNumber.Value, rpl, tk), token),
                "SendNews" => await SendWithCheckRights(client, user, chatId.Value, replyPrams, callbackQuery.Id, RoleEnum.user,
                                        (cl, chId, rpl, userNews, tk) => SendNewsHandle(cl, chId, userNews, rpl, tk), token),
                "DeleteNews" => await SendWithCheckRights(client, user, chatId.Value, replyPrams, callbackQuery.Id, RoleEnum.user,
                                        (cl, chId, rpl, userNews, tk) => DeleteNewsHandle(cl, chId, userNews, rpl, tk), token),
                "ApproveNewsChoice" => await SendWithCheckRights(client, user, chatId.Value, replyPrams, callbackQuery.Id, RoleEnum.moderator,
                                        (cl, chId, rpl, userNews, tk) => SendNewsChoiceApproveHandle(cl, chId, offset, rpl, tk), token),
                "ApproveNews" => await SendWithCheckRights(client, user, chatId.Value, replyPrams, callbackQuery.Id, RoleEnum.moderator,
                                        (cl, chId, rpl, userNews, tk) => SendNewsApproveHandle(cl, chId, newsId.Value, rpl, tk), token),
                "DeclineNews" => await SendWithCheckRights(client, user, chatId.Value, replyPrams, callbackQuery.Id, RoleEnum.moderator,
                                        (cl, chId, rpl, userNews, tk) => SendNewsDeclineHandle(cl, chId, newsId.Value, rpl, tk), token),
                "AddAdminChoice" => await SendWithCheckRights(client, user, chatId.Value, replyPrams, callbackQuery.Id, RoleEnum.administrator,
                                        (cl, chId, rpl, userNews, tk) => SendAddAdminChoiceHandle(cl, chId, user, userNews, rpl, tk), token),
                "AddModeratorChoice" => await SendWithCheckRights(client, user, chatId.Value, replyPrams, callbackQuery.Id, RoleEnum.administrator,
                                        (cl, chId, rpl, userNews, tk) => SendAddModeratorChoiceHandle(cl, chId, user, userNews, rpl, tk), token),
                "EditButtonChoice" => await SendWithCheckRights(client, user, chatId.Value, replyPrams, callbackQuery.Id, RoleEnum.administrator,
                                        (cl, chId, rpl, userNews, tk) => SendEditButtonChoiceHandle(cl, chId, user, userNews, rpl, tk), token),
                "AddAdmin" => await SendWithCheckRights(client, user, chatId.Value, replyPrams, callbackQuery.Id, RoleEnum.administrator,
                                        (cl, chId, rpl, userNews, tk) => AddAdminHandle(cl, chId, userNews, rpl, tk), token),
                "AddAdminDecline" => await SendWithCheckRights(client, user, chatId.Value, replyPrams, callbackQuery.Id, RoleEnum.administrator,
                                        (cl, chId, rpl, userNews, tk) => AddAdminDeclineHandle(cl, chId, userNews, rpl, tk), token),
                "AddModerator" => await SendWithCheckRights(client, user, chatId.Value, replyPrams, callbackQuery.Id, RoleEnum.administrator,
                                        (cl, chId, rpl, userNews, tk) => AddModeratorHandle(cl, chId, userNews, rpl, tk), token),
                "AddModeratorDecline" => await SendWithCheckRights(client, user, chatId.Value, replyPrams, callbackQuery.Id, RoleEnum.administrator,
                                        (cl, chId, rpl, userNews, tk) => AddModeratorDeclineHandle(cl, chId, userNews, rpl, tk), token),
                "EditButton" => await SendWithCheckRights(client, user, chatId.Value, replyPrams, callbackQuery.Id, RoleEnum.administrator,
                                        (cl, chId, rpl, userNews, tk) => EditButtonHandle(cl, chId, userNews, rpl, tk), token),
                "EditButtonDecline" => await SendWithCheckRights(client, user, chatId.Value, replyPrams, callbackQuery.Id, RoleEnum.administrator,
                                        (cl, chId, rpl, userNews, tk) => EditButtonDeclineHandle(cl, chId, userNews, rpl, tk), token),
                _ => await SendWithCheckRights(client, user, chatId.Value, replyPrams, callbackQuery.Id, RoleEnum.user,
                                        (cl, chId, rpl, userNews, tk) => SendUserNotImplemented(cl, chId, rpl), token),
            };
        }

        private async Task<bool> SendWithCheckRights(
            TelegramBotClient client,
            Contract.Model.User user,            
            long chatId,  
            ReplyParameters? replyPrams,             
            string callbackQueryId, 
            RoleEnum role,
            Func<TelegramBotClient, long, ReplyParameters?, News?, CancellationToken, Task> succesAction,
            CancellationToken token)
        {
            var result = false;
            var userNews = await _newsDataService.GetCurrentNews(user.Id, token);
            if (!user.Roles.Contains(role))
            {
                await SendUserHasNoRights(client, chatId, replyPrams);
            }
            else
            {
                await succesAction(client, chatId, replyPrams, userNews, token);               
                result = true;
            }
            await client.AnswerCallbackQueryAsync(new AnswerCallbackQueryArgs(callbackQueryId), cancellationToken: token);

            return result;
        }

        private async Task DeleteNewsHandle(TelegramBotClient client, long chatId, News? userNews, ReplyParameters? replyPrams, CancellationToken token)
        {
            if (userNews != null)
            {
                await DeleteNewsMessageAccepted(client, chatId, userNews, replyPrams, token);
            }
            else
            {
                await DeleteNewsMessageNotFound(client, chatId, replyPrams);
            }
        }

        private async Task SendNewsHandle(TelegramBotClient client, long chatId, News? userNews, ReplyParameters? replyPrams, CancellationToken token)
        {
            if (userNews != null)
            {
                await SendNewsMessageAccepted(client, chatId, userNews, replyPrams, token);
            }
            else
            {
                await SendNewsMessageNotFound(client, chatId, replyPrams);
            }
        }

        private async Task AddAdminHandle(TelegramBotClient client, long chatId, News? userNews, ReplyParameters? replyPrams, CancellationToken token)
        {
            if (userNews != null)
            {
                await AddAdminAccepted(client, chatId, userNews, replyPrams, token);
            }
            else
            {
                await AddAdminMessageNotFound(client, chatId, replyPrams);
            }
        }

        private async Task EditButtonHandle(TelegramBotClient client, long chatId, News? userNews, ReplyParameters? replyPrams, CancellationToken token)
        {
            if (userNews != null)
            {
                await EditButtonAccepted(client, chatId, userNews, replyPrams, token);
            }
            else
            {
                await EditButtonMessageNotFound(client, chatId, replyPrams);
            }
        }

        private async Task AddModeratorHandle(TelegramBotClient client, long chatId, News? userNews, ReplyParameters? replyPrams, CancellationToken token)
        {
            if (userNews != null)
            {
                await AddModeratorAccepted(client, chatId, userNews, replyPrams, token);
            }
            else
            {
                await AddModeratorMessageNotFound(client, chatId, replyPrams);
            }
        }

        private async Task AddAdminDeclineHandle(TelegramBotClient client, long chatId, News? userNews, ReplyParameters? replyPrams, CancellationToken token)
        {
            if (userNews != null)
            {
                await AddAdminModeratorDeclined(client, chatId, userNews, replyPrams, token);
            }
            else
            {
                await AddAdminMessageNotFound(client, chatId, replyPrams);
            }
        }

        private async Task EditButtonDeclineHandle(TelegramBotClient client, long chatId, News? userNews, ReplyParameters? replyPrams, CancellationToken token)
        {
            if (userNews != null)
            {
                await AddAdminModeratorDeclined(client, chatId, userNews, replyPrams, token);
            }
            else
            {
                await EditButtonMessageNotFound(client, chatId, replyPrams);
            }
        }

        private async Task AddModeratorDeclineHandle(TelegramBotClient client, long chatId, News? userNews, ReplyParameters? replyPrams, CancellationToken token)
        {
            if (userNews != null)
            {
                await AddAdminModeratorDeclined(client, chatId, userNews, replyPrams, token);
            }
            else
            {
                await AddModeratorMessageNotFound(client, chatId, replyPrams);
            }
        }

        private async Task SendNewsApproveHandle(TelegramBotClient client, long chatId, Guid newsId, ReplyParameters? replyPrams, CancellationToken token)
        {
            var userNews = await _newsDataService.GetNewsById(newsId, token);
            if (userNews != null)
            {
                await SendNewsMessageApproved(client, chatId, userNews, replyPrams, token);
            }
            else
            {
                await SendNewsMessageNotFound(client, chatId, replyPrams);
            }
        }

        private async Task SendNewsDeclineHandle(TelegramBotClient client, long chatId, Guid newsId, ReplyParameters? replyPrams, CancellationToken token)
        {
            var userNews = await _newsDataService.GetNewsById(newsId, token);
            if (userNews != null)
            {
                await SendNewsMessageDeclined(client, chatId, userNews, replyPrams, token);
            }
            else
            {
                await SendNewsMessageNotFound(client, chatId, replyPrams);
            }
        }

        private async Task SendNewsChoiceApproveHandle(TelegramBotClient client, long chatId, int offset, ReplyParameters? replyPrams, CancellationToken token)
        {            
            var userNewses = await _newsDataService.GetNewsForApprove(token);

            var allCount = userNewses.Count;

            offset = Math.Min(offset, allCount - 1);

            var userNews = userNewses.Skip(offset).FirstOrDefault();

            if (userNews != null)
            {
                await SendNewsMessageForApprove(client, chatId, userNews, GetExistsPrev(offset), GetExistsNext(offset, allCount), offset, replyPrams, token);
            }
            else
            {
                await ApproveNewsMessageNotFound(client, chatId, replyPrams);
            }
        }

        private static bool GetExistsNext(int offset, int allCount) => offset < allCount - 1;

        private static bool GetExistsPrev(int offset) => offset > 0;

        private async Task SendNewsChoiceHandle(TelegramBotClient client, long chatId, Contract.Model.User user, News? userNews, int buttonNumber, ReplyParameters? replyPrams, CancellationToken token)
        {
            if (userNews != null)
            {
                await SendUserRemember(client, chatId, userNews, replyPrams, token);
            }
            else
            {
                await SendNewsMessageForUser(client, chatId, buttonNumber, user, replyPrams, token);
            }
        }

        private async Task SendSwitchNotifyHandle(TelegramBotClient client, long chatId, Guid userId, ReplyParameters? replyPrams, CancellationToken token)
        {            
            var isNotify = await _userDataService.SwitchUserNotify(userId, token);

            await client.SendMessageAsync(chatId, $"Уведомления {(isNotify ? "включены":"выключены")}",
                replyParameters: replyPrams, cancellationToken: token);
        }

        private async Task SendAddAdminChoiceHandle(TelegramBotClient client, long chatId, Contract.Model.User user, News? userNews, ReplyParameters? replyPrams, CancellationToken token)
        {
            if (userNews != null)
            {
                await SendUserRemember(client, chatId, userNews, replyPrams, token);
            }
            else
            {
                await SendAddAdminForUser(client, chatId, user, replyPrams, token);
            }
        }

        private async Task SendAddModeratorChoiceHandle(TelegramBotClient client, long chatId, Contract.Model.User user, News? userNews, ReplyParameters? replyPrams, CancellationToken token)
        {
            if (userNews != null)
            {
                await SendUserRemember(client, chatId, userNews, replyPrams, token);
            }
            else
            {
                await SendAddModeratorForUser(client, chatId, user, replyPrams, token);
            }
        }

        private async Task SendEditButtonChoiceHandle(TelegramBotClient client, long chatId, Contract.Model.User user, News? userNews, ReplyParameters? replyPrams, CancellationToken token)
        {
            if (userNews != null)
            {
                await SendUserRemember(client, chatId, userNews, replyPrams, token);
            }
            else
            {
                await SendEditButtonForUser(client, chatId, user, replyPrams, token);
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

        private async Task SendNewsMessageAccepted(TelegramBotClient client, long chatId, News userNews, ReplyParameters? replyParameters, CancellationToken token)
        {
            var messages = await _newsDataService.GetNewsMessages(userNews.Id, token);

            if (messages.Count == 0)
            {
                await client.SendMessageAsync(chatId, "Обращение создано некорректно, отправьте не менее одного сообщения", cancellationToken: token);
                return;
            }

            await _newsDataService.SetNewsAccepted(userNews.Id, token);
            await client.SendMessageAsync(chatId, "Обращение принято в обработку", cancellationToken: token, replyParameters: replyParameters);
            await NotifyModerators(client, userNews, replyParameters, token);
        }

        private async Task NotifyModerators(TelegramBotClient client, News userNews, ReplyParameters? replyParameters, CancellationToken token)
        {
            var notifyModerators = await _userDataService.GetNotifyModerators(token);
            foreach (var moder in notifyModerators.Where(s => s.IsModerator))
            {
                await SendNewsMessageForApprove(client, moder.ChatId, userNews, false, false, 0, replyParameters, token);
            }
        }

        private async Task AddAdminAccepted(TelegramBotClient client, long chatId, News userNews, ReplyParameters? replyParameters, CancellationToken token)
        {
            var messages = await _newsDataService.GetNewsMessages(userNews.Id, token);

            if (messages.Count == 0)
            {
                await client.SendMessageAsync(chatId, "Не отправлено ни одного логина", cancellationToken: token);
                return;
            }

            await ParseAndSetRole(messages, RoleEnum.administrator, token);

            await _newsDataService.SetNewsApproved(userNews.Id, token);
            await client.SendMessageAsync(chatId, "Администраторы добавлены", cancellationToken: token, replyParameters: replyParameters);
        }

        private async Task ParseAndSetRole(IEnumerable<NewsMessage> messages, RoleEnum role, CancellationToken token)
        {
            foreach (var message in messages)
            {
                var logins = message.TextValue?.Split(",").Select(s => s.Trim()).Select(s => s.TrimStart('@')).Where(s => s != string.Empty);
                if (logins == null || !logins.Any()) continue;

                foreach (var login in logins)
                {
                    await _userDataService.SetRole(login, role , token);
                }
            }
        }

        private async Task EditButtonAccepted(TelegramBotClient client, long chatId, News userNews, ReplyParameters? replyParameters, CancellationToken token)
        {
            var messages = await _newsDataService.GetNewsMessages(userNews.Id, token);

            if (messages.Count == 0)
            {
                await client.SendMessageAsync(chatId, "Не отправлено ни одной кнопки", cancellationToken: token);
                return;
            }

            var settings = ParseButtonsSettings(messages);

            if (settings.Count == 0)
            {
                await client.SendMessageAsync(chatId, "Не отправлено ни одной кнопки", cancellationToken: token);
                return;
            }

            var groupped = settings.GroupBy(s => s.Number);
            if (groupped.Any(s => s.Count() > 1))
            {
                await client.SendMessageAsync(chatId, "Для некоторых кнопок отправлено больше одной настройки, перезапустите настройку", cancellationToken: token);
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

            await _newsDataService.SetNewsApproved(userNews.Id, token);            
            await client.SendMessageAsync(chatId, "Кнопки сохранены", cancellationToken: token, replyParameters: replyParameters);
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

        private async Task AddAdminModeratorDeclined(TelegramBotClient client, long chatId, News userNews, ReplyParameters? replyParameters, CancellationToken token)
        {
            await _newsDataService.SetNewsDeclined(userNews.Id, token);           
            await client.SendMessageAsync(chatId, "Задание отменено", cancellationToken: token, replyParameters: replyParameters);
        }

        private async Task AddModeratorAccepted(TelegramBotClient client, long chatId, News userNews, ReplyParameters? replyParameters, CancellationToken token)
        {
            var messages = await _newsDataService.GetNewsMessages(userNews.Id, token);

            if (messages.Count == 0)
            {
                await client.SendMessageAsync(chatId, "Не отправлено ни одного логина", cancellationToken: token);
                return;
            }

            await ParseAndSetRole(messages, RoleEnum.moderator, token);

            await _newsDataService.SetNewsApproved(userNews.Id, token);           
            await client.SendMessageAsync(chatId, "Модераторы добавлены", cancellationToken: token, replyParameters: replyParameters);
        }

        private static async Task SendNewsMessageNotFound(TelegramBotClient client, long chatId, ReplyParameters? replyParameters)
        {
            await client.SendMessageAsync(chatId, "Нет неподтвержденных обращений", replyParameters: replyParameters);
        }

        private static async Task AddAdminMessageNotFound(TelegramBotClient client, long chatId, ReplyParameters? replyParameters)
        {
            await client.SendMessageAsync(chatId, "Нет задач на добавление администратора", replyParameters: replyParameters);
        }

        private static async Task EditButtonMessageNotFound(TelegramBotClient client, long chatId, ReplyParameters? replyParameters)
        {
            await client.SendMessageAsync(chatId, "Нет задач на добавление кнопок", replyParameters: replyParameters);
        }

        private static async Task AddModeratorMessageNotFound(TelegramBotClient client, long chatId, ReplyParameters? replyParameters)
        {
            await client.SendMessageAsync(chatId, "Нет задач на добавление модератора", replyParameters: replyParameters);
        }

        private static async Task SendUserHasNoRights(TelegramBotClient client, long chatId, ReplyParameters? replyParameters)
        {
            await client.SendMessageAsync(chatId, "У вас нет прав на это действие", replyParameters: replyParameters);
        }

        private static async Task SendUserNotImplemented(TelegramBotClient client, long chatId, ReplyParameters? replyParameters)
        {
            await client.SendMessageAsync(chatId, "Действие не реализовано", replyParameters: replyParameters);
        }

        private async Task DeleteNewsMessageAccepted(TelegramBotClient client, long chatId, News userNews, ReplyParameters? replyParameters, CancellationToken token)
        {
            await _newsDataService.SetNewsDeleted(userNews.Id, token);            
            await client.SendMessageAsync(chatId, "Обращение удалено", cancellationToken: token, replyParameters: replyParameters);
        }

        private static async Task DeleteNewsMessageNotFound(TelegramBotClient client, long chatId, ReplyParameters? replyParameters)
        {
            await client.SendMessageAsync(chatId, "Нет неподтвержденных обращений", replyParameters: replyParameters);
        }

        private static async Task ApproveNewsMessageNotFound(TelegramBotClient client, long chatId, ReplyParameters? replyParameters)
        {
            await client.SendMessageAsync(chatId, "Нет неотправленных обращений", replyParameters: replyParameters);
        }

        private async Task SendNewsMessageForUser(TelegramBotClient client, long chatId, int buttonNumber, Contract.Model.User user, ReplyParameters? replyParameters, CancellationToken token)
        {
            var button = await _buttonsDataService.GetButtonByNumber(buttonNumber, token);

            if (button == null || !button.ToSend)
            {
                await client.SendMessageAsync(chatId, "Недействительное направление обращения, выберите другое",
                    replyParameters: replyParameters, cancellationToken: token);
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

            await client.SendMessageAsync(chatId, "Отправьте одно или несколько сообщений и нажмите кнопку Отправить", replyMarkup: replyMarkup,
                replyParameters: replyParameters, cancellationToken: token);
        }

        private async Task SendAddAdminForUser(TelegramBotClient client, long chatId, Contract.Model.User user, ReplyParameters? replyPrams, CancellationToken token)
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

            await client.SendMessageAsync(chatId, "Отправьте по одному логины пользователей, которых надо добавить в администраторы и нажмите кнопку Добавить", 
                replyMarkup: replyMarkup, 
                replyParameters: replyPrams, cancellationToken: token);
        }

        private async Task SendAddModeratorForUser(TelegramBotClient client, long chatId, Contract.Model.User user, ReplyParameters? replyPrams, CancellationToken token)
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

            await client.SendMessageAsync(chatId, "Отправьте по одному логины пользователей, которых надо добавить в модераторы и нажмите кнопку Добавить",
                replyMarkup: replyMarkup,
                replyParameters: replyPrams, cancellationToken: token);
        }

        private async Task SendEditButtonForUser(TelegramBotClient client, long chatId, Contract.Model.User user, ReplyParameters? replyPrams, CancellationToken token)
        {
            var availableButtons = await _buttonsDataService.GetAllButtons(token);            
            if(availableButtons.Count != 0)
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
                    replyMarkup: replyMarkup,
                    replyParameters: replyPrams, cancellationToken: token);
            }
            else
            {                
                await client.SendMessageAsync(chatId, "Нет доступных кнопок для добавления пользователю. " +
                    "Для добавления доступных кнопок добавьте бота в группу и отправьте в чат одно сообщение (для разбивки по темам - отправьте по одному сообщению в каждой из тем)." +
                    "Пользователь, отправляющий сообщения, должен быть администратором бота.",                   
                    replyParameters: replyPrams, cancellationToken: token);
            }
               
        }

        private async Task SendEditButtonForAdminRemember(TelegramBotClient client, long chatId, ReplyParameters? replyParameters, CancellationToken token)
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
                    " и нажмите кнопку Сохранить, либо Отменить для отмены изменения кнопок", replyMarkup: replyMarkup, replyParameters: replyParameters, cancellationToken: token);
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
                     replyMarkup: replyMarkup, replyParameters: replyParameters, cancellationToken: token);
            }
        }
               

        private async Task SendNewsMessageForApprove(TelegramBotClient client, long chatId, News userNews, 
            bool existsPrev, bool existsNext, int currentOffset, ReplyParameters? replyPrams, CancellationToken token)
        {
            var messages = await _newsDataService.GetNewsMessages(userNews.Id, token);

            if(messages.Count == 0)
            {
                await ClearNews(client, chatId, userNews, replyPrams, token);
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
           
            if(userButton == null)
            {
                await ClearNews(client, chatId, userNews, replyPrams, token);
                return;
            }
            else
            {
                await client.SendMessageAsync(chatId, $"Обращение для подтверждения в раздел \"{userButton.ChatName} : {userButton.ThreadName} ({userButton.ButtonName})\"", replyMarkup: replyMarkup, replyParameters: replyPrams, cancellationToken: token);
                await client.ForwardMessagesAsync(chatId, userNews.ChatId, messages.Select(s => (int)s.TGMessageId), cancellationToken: token);
            }                
        }

        private async Task ClearNews(TelegramBotClient client, long chatId, News userNews, ReplyParameters? replyPrams, CancellationToken token)
        {
            await client.SendMessageAsync(chatId, "Обращение для подтверждения создано некорректно, будет удалено", replyParameters: replyPrams, cancellationToken: token);
            await client.SendMessageAsync(userNews.ChatId, "Обращение создано некорректно, будет удалено", cancellationToken: token);
            await _newsDataService.SetNewsDeleted(userNews.Id, token);
        }

        private async Task SendNewsMessageApproved(TelegramBotClient client, long chatId, News userNews, ReplyParameters? replyPrams, CancellationToken token)
        {
            await _newsDataService.SetNewsApproved(userNews.Id, token);                       

            if(userNews.GroupId.HasValue)
            {
                await client.SendMessageAsync(chatId, "Обращение подтверждена", replyParameters: replyPrams, cancellationToken: token);
                await client.SendMessageAsync(userNews.ChatId, "Обращение подтверждена", replyParameters: replyPrams, cancellationToken: token);

                var messages = await _newsDataService.GetNewsMessages(userNews.Id, token);
                if (messages.Count != 0)
                {
                    await client.ForwardMessagesAsync(userNews.GroupId.Value, userNews.ChatId, messages.Select(s => (int)s.TGMessageId), messageThreadId: (int?)userNews.ThreadId, cancellationToken: token);
                }
            }
            else
            {
                await client.SendMessageAsync(chatId, "Нельзя подтвердить обращение: не задано направление. Требуется пересоздание", replyParameters: replyPrams, cancellationToken: token);
                await client.SendMessageAsync(userNews.ChatId, "Нельзя подтвердить обращение: не задано направление. Требуется пересоздание", replyParameters: replyPrams, cancellationToken: token);
            }            
        }

        private async Task SendNewsMessageDeclined(TelegramBotClient client, long chatId, News userNews, ReplyParameters? replyPrams, CancellationToken token)
        {
            await _newsDataService.SetNewsDeclined(userNews.Id, token);

            await client.SendMessageAsync(chatId, "Обращение отклонено", replyParameters: replyPrams, cancellationToken: token);
            await client.SendMessageAsync(userNews.ChatId, "Обращение отклонено", replyParameters: replyPrams, cancellationToken: token);
        }

        private Task SendUserRemember(TelegramBotClient client, long chatId, News? news, ReplyParameters? replyParameters, CancellationToken token)
        {
            return (news?.Type) switch
            {
                "news" => SendNewsMessageForUserRemember(client, chatId, replyParameters),
                "addadmin" => SendAddAdminForAdminRemember(client, chatId, replyParameters),
                "addmoderator" => SendAddModeratorForAdminRememeber(client, chatId, replyParameters),
                "editbutton" => SendEditButtonForAdminRemember(client, chatId, replyParameters, token),
                _ => Task.CompletedTask,
            };
        }

        private static async Task SendNewsMessageForUserRemember(TelegramBotClient client, long chatId, ReplyParameters? replyParameters)
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
                replyMarkup: replyMarkup, replyParameters: replyParameters);
        }

        private static async Task SendAddAdminForAdminRemember(TelegramBotClient client, long chatId, ReplyParameters? replyParameters)
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
                replyMarkup: replyMarkup, replyParameters: replyParameters);
        }

        

        private static async Task SendAddModeratorForAdminRememeber(TelegramBotClient client, long chatId, ReplyParameters? replyParameters)
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
                replyMarkup: replyMarkup, replyParameters: replyParameters);
        }

        private static async Task SendTestConnectionMessage(TelegramBotClient client, Message message, string addInfo, ReplyParameters? replyParameters, CancellationToken token)
        {
            await client.SendMessageAsync(message.Chat.Id, addInfo, cancellationToken: token, replyParameters: replyParameters);
        }

        private async Task SendMenuButtons(TelegramBotClient client, long chatId, Contract.Model.User user, CancellationToken token)
        {            

            var buttons = new List<List<InlineKeyboardButton>>();
                       
            if (user.IsModerator) buttons.AddRange(GetModeratorButtons(user));
            if (user.IsAdmin) buttons.AddRange(GetAdminButtons());
            buttons.AddRange(await GetUserButtons(token));

            ReplyMarkup replyMarkup = new InlineKeyboardMarkup(buttons);
            await client.SendMessageAsync(chatId, "Выберите, что хотите сделать", replyMarkup: replyMarkup, cancellationToken: token);
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

        private static List<List<InlineKeyboardButton>> GetModeratorButtons(Contract.Model.User user)
        {
            var switchNotify = "Включить уведомления";
            if(user.IsNotify)
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
                }]
            ];
        }

        public async Task SetCommands()
        {
            var client = new TelegramBotClient(botToken);
            _ = await client.SetMyCommandsAsync(new SetMyCommandsArgs([new("start", "Начать работу")]));
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

        private async Task StartCommandHandle(TelegramBotClient client, Message message, ReplyParameters? reply, Contract.Model.User user, News? userNews, CancellationToken cancellationToken)
        {
            if (userNews != null)
            {
                await SendUserRemember(client, message.Chat.Id, userNews, reply, cancellationToken);
            }
            else
            {
                await SendMenuButtons(client, message.Chat.Id, user, cancellationToken);
            }
        }
    }   
}
