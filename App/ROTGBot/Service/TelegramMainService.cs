using Microsoft.Extensions.Logging;
using ROTGBot.Db.Interface;
using ROTGBot.Db.Model;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq.Dynamic.Core.Tokenizer;
using System.Threading;
using Telegram.BotAPI;
using Telegram.BotAPI.AvailableMethods;
using Telegram.BotAPI.AvailableTypes;
using Telegram.BotAPI.GettingUpdates;
using User = Telegram.BotAPI.AvailableTypes.User;

namespace ROTGBot.Service
{
    public class TelegramMainService(
        ILogger<TelegramMainService> logger,
        IRepository<ROTGBot.Db.Model.User> userRepo,
        IRepository<ROTGBot.Db.Model.Role> roleRepo,
        IRepository<ROTGBot.Db.Model.UserRole> userRoleRepo,
        IRepository<ROTGBot.Db.Model.News> newsRepo,
        IRepository<ROTGBot.Db.Model.NewsMessage> newsMessageRepo,
        IRepository<ROTGBot.Db.Model.Groups> groupsRepo,
        IRepository<ROTGBot.Db.Model.NewsButton> newsButtonRepo) : ITelegramMainService
    {
        private const string botToken = "token";
        private readonly ILogger<TelegramMainService> _logger = logger;
        private readonly IRepository<ROTGBot.Db.Model.User> _userRepo = userRepo;
        private readonly IRepository<ROTGBot.Db.Model.Role> _roleRepo = roleRepo;
        private readonly IRepository<ROTGBot.Db.Model.UserRole> _userRoleRepo = userRoleRepo;
        private readonly IRepository<ROTGBot.Db.Model.News> _newsRepo = newsRepo;
        private readonly IRepository<ROTGBot.Db.Model.NewsMessage> _newsMessageRepo = newsMessageRepo;
        private readonly IRepository<ROTGBot.Db.Model.Groups> _groupsRepo = groupsRepo;
        private readonly IRepository<ROTGBot.Db.Model.NewsButton> _newsButtonRepo = newsButtonRepo;

        public async Task<int> Execute(int offset)
        {
            CancellationToken cancellationToken = new CancellationTokenSource(60000).Token;
            var client = new TelegramBotClient(botToken);                 
            
            var updates = await client.GetUpdatesAsync(offset);
            if (updates?.Any() == true)
            {
                foreach (var update in updates)
                {
                    try
                    {
                        _logger.LogInformation("Update: {name}. {message}", update.Message?.Chat.Username, update.Message?.Text);
                        if (update.Message != null)
                        {
                            await HandleMessage(client, update.Message, cancellationToken);
                        }
                        else if (update.CallbackQuery != null)
                        {
                            await HandleCallback(client, update.CallbackQuery, cancellationToken);
                        }
                        else if (update.MyChatMember != null)
                        {
                            await HandleMyChatMember(client, update.MyChatMember, cancellationToken);
                        }                        
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Ошибка при обработке события");                        
                    }
                }
                return updates.Last().UpdateId + 1;
            }
            return offset;
            //client.SendMessage(new SendMessageArgs());
        }

        private async Task HandleMyChatMember(TelegramBotClient client, ChatMemberUpdated myChatMember, CancellationToken cancellationToken)
        {
            var chatId = myChatMember.Chat.Id;
            var existsGroups = (await _groupsRepo.GetAsync(new Filter<Groups>() {
                Selector = s => s.ChatId == chatId
            }, cancellationToken)).FirstOrDefault();

            if(existsGroups == null)
            {
                await _groupsRepo.AddAsync(new Groups()
                {
                    ChatId = chatId,
                    Description = $"{myChatMember.Chat.Title} : {myChatMember.Chat.FirstName} {myChatMember.Chat.LastName} (@{myChatMember.Chat.Username})",
                    Title = myChatMember.Chat.Title,
                    IsDeleted = false,
                    Id = Guid.NewGuid(),
                    SendNews = false
                }, true, cancellationToken);
            }
        }

        private async Task HandleMessage(TelegramBotClient client, Message message, CancellationToken cancellationToken)
        {
            if(message.From != null)
            {
                string addMessage = "";
                var user = await GetUser(message.From, message.Chat.Id, cancellationToken);
                var roles = await GetUserRoles(user, cancellationToken);

                if(roles.Any(s => s == "administrator"))
                {
                    if(message.IsTopicMessage == true)
                    {
                        var chatId = message.Chat.Id;
                        var threadId = message.MessageThreadId;

                        var exists = await _newsButtonRepo.GetAsync(new Filter<NewsButton>()
                        {
                            Selector = s => s.ChatId == chatId && s.ThreadId == threadId
                        }, cancellationToken);

                        var allButtons = await _newsButtonRepo.GetAsync(new Filter<NewsButton>() { 
                            Selector = s => s.IsDeleted == false
                        }, cancellationToken);


                        if (exists.Count == 0)
                        {
                            var forumTopic = message.ForumTopicCreated ?? message.ReplyToMessage?.ForumTopicCreated;

                            if (forumTopic != null)
                            {
                                await _newsButtonRepo.AddAsync(new NewsButton()
                                {
                                    ChatId = chatId,
                                    ChatName = message.Chat.Title ?? $"{message.Chat.FirstName} {message.Chat.LastName}",
                                    Id = Guid.NewGuid(),
                                    IsDeleted = false,
                                    ThreadId = threadId,
                                    ThreadName = forumTopic.Name,
                                    ToSend = false,
                                    ButtonNumber = allButtons.Count != 0 ? allButtons.Max(s => s.ButtonNumber) + 1 : 1
                                }, true, cancellationToken);
                            }                            
                        }                        
                    }
                }
                                
                var userNews = await GetCurrentNews(user, cancellationToken);

                if (message.Text == "/start")
                {
                    if (userNews != null)
                    {
                        await SendUserRemember(client, message.Chat.Id, userNews, GetReplyParameters(message.MessageId), cancellationToken);
                    }
                    else
                    {
                        await SendMenuButtons(client, message.Chat.Id, user, cancellationToken);
                    }
                }
                else if (userNews != null)
                {
                    await _newsMessageRepo.AddAsync(new Db.Model.NewsMessage()
                    {
                        Id = Guid.NewGuid(),
                        IsDeleted = false,
                        NewsId = userNews.Id,
                        TGMessageId = message.MessageId,
                        TextValue = message.Text
                    }, true, cancellationToken);
                }
                else if(message.IsTopicMessage != true)
                {
                    addMessage += $"Привет, {user.Name}! Для работы нажмите кнопку меню - Старт или введите /start";
                    await SendTestConnectionMessage(client, message, addMessage, GetReplyParameters(message.MessageId), cancellationToken);
                }
            }
            else
            {
                await SendTestConnectionMessage(client, message, "Не удалось получить информацию по отправителю", GetReplyParameters(message.MessageId), cancellationToken);
            }
        }

        private async Task<Db.Model.News?> GetCurrentNews(Db.Model.User user, CancellationToken cancellationToken)
        {            
            return (await _newsRepo.GetAsync(new Db.Model.Filter<Db.Model.News>()
            {
                Selector = s => s.UserId == user.Id
            }, cancellationToken)).FirstOrDefault(s => s.State == "create");
        }

        private async Task<Db.Model.User> GetUser(User tguser, long chatId, CancellationToken cancellationToken)
        {
            var user = (await _userRepo.GetAsync(new Db.Model.Filter<Db.Model.User>()
            {
                Selector = s => s.TGId == tguser.Id
            }, cancellationToken)).FirstOrDefault();
            
            if(user == null)
            {
                user = await _userRepo.AddAsync(new Db.Model.User()
                {
                    Id = Guid.NewGuid(),
                    Description = $"{tguser.FirstName} {tguser.LastName} (@{tguser.Username})",
                    IsDeleted = false,
                    Name = $"{tguser.FirstName} {tguser.LastName} (@{tguser.Username})",
                    TGLogin = tguser.Username,
                    TGId = tguser.Id,
                    ChatId = chatId
                }, true, cancellationToken);

                var userRole = (await _roleRepo.GetAsync(new Filter<Role>() { Selector = s => s.Name == "user" }, cancellationToken)).First();

                await _userRoleRepo.AddAsync(new UserRole()
                {
                    Id = Guid.NewGuid(),
                    IsDeleted = false,
                    RoleId = userRole.Id,
                    UserId = user.Id
                }, true, cancellationToken);
            }
            else
            {
                user.ChatId = chatId;
                await _userRepo.UpdateAsync(user, true, cancellationToken);
            }
            return user;
        }

        private async Task<bool> HandleCallback(TelegramBotClient client, CallbackQuery callbackQuery, CancellationToken token)
        {

            var chatId = callbackQuery.Message?.Chat.Id;
            if (chatId == null)
            {
                return false;
            }

            var user = await GetUser(callbackQuery.From, chatId.Value, token);
            var roles = await GetUserRoles(user, token);
            var replyPrams = GetReplyParameters(callbackQuery.Message?.MessageId);
            if (roles.Length == 0)
            {
                await SendUserHasNoRights(client, chatId.Value, replyPrams);
                return false;
            }
            
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

            return data switch
            {
                "SwitchNotify" => await SendWithCheckRights(client, user, chatId.Value, roles, replyPrams, callbackQuery.Id, "moderator",
                                        (cl, chId, rpl, userNews, tk) => SendSwitchNotifyHandle(cl, chId, user, rpl, tk), token),
                "SendNewsChoice" => await SendWithCheckRights(client, user, chatId.Value, roles, replyPrams, callbackQuery.Id, "user",
                                        (cl, chId, rpl, userNews, tk) => SendNewsChoiceHandle(cl, chId, user, userNews, buttonNumber.Value, rpl, tk), token),
                "SendNews" => await SendWithCheckRights(client, user, chatId.Value, roles, replyPrams, callbackQuery.Id, "user",
                                        (cl, chId, rpl, userNews, tk) => SendNewsHandle(cl, chId, userNews, rpl, tk), token),
                "DeleteNews" => await SendWithCheckRights(client, user, chatId.Value, roles, replyPrams, callbackQuery.Id, "user",
                                        (cl, chId, rpl, userNews, tk) => DeleteNewsHandle(cl, chId, userNews, rpl, tk), token),
                "ApproveNewsChoice" => await SendWithCheckRights(client, user, chatId.Value, roles, replyPrams, callbackQuery.Id, "moderator",
                                        (cl, chId, rpl, userNews, tk) => SendNewsChoiceApproveHandle(cl, chId, offset, rpl, tk), token),
                "ApproveNews" => await SendWithCheckRights(client, user, chatId.Value, roles, replyPrams, callbackQuery.Id, "moderator",
                                        (cl, chId, rpl, userNews, tk) => SendNewsApproveHandle(cl, chId, newsId.Value, rpl, tk), token),
                "DeclineNews" => await SendWithCheckRights(client, user, chatId.Value, roles, replyPrams, callbackQuery.Id, "moderator",
                                        (cl, chId, rpl, userNews, tk) => SendNewsDeclineHandle(cl, chId, newsId.Value, rpl, tk), token),
                "AddAdminChoice" => await SendWithCheckRights(client, user, chatId.Value, roles, replyPrams, callbackQuery.Id, "administrator",
                                        (cl, chId, rpl, userNews, tk) => SendAddAdminChoiceHandle(cl, chId, user, userNews, rpl, tk), token),
                "AddModeratorChoice" => await SendWithCheckRights(client, user, chatId.Value, roles, replyPrams, callbackQuery.Id, "administrator",
                                        (cl, chId, rpl, userNews, tk) => SendAddModeratorChoiceHandle(cl, chId, user, userNews, rpl, tk), token),
                "EditButtonChoice" => await SendWithCheckRights(client, user, chatId.Value, roles, replyPrams, callbackQuery.Id, "administrator",
                                        (cl, chId, rpl, userNews, tk) => SendEditButtonChoiceHandle(cl, chId, user, userNews, rpl, tk), token),
                "AddAdmin" => await SendWithCheckRights(client, user, chatId.Value, roles, replyPrams, callbackQuery.Id, "administrator",
                                        (cl, chId, rpl, userNews, tk) => AddAdminHandle(cl, chId, userNews, rpl, tk), token),
                "AddAdminDecline" => await SendWithCheckRights(client, user, chatId.Value, roles, replyPrams, callbackQuery.Id, "administrator",
                                        (cl, chId, rpl, userNews, tk) => AddAdminDeclineHandle(cl, chId, userNews, rpl, tk), token),
                "AddModerator" => await SendWithCheckRights(client, user, chatId.Value, roles, replyPrams, callbackQuery.Id, "administrator",
                                        (cl, chId, rpl, userNews, tk) => AddModeratorHandle(cl, chId, userNews, rpl, tk), token),
                "AddModeratorDecline" => await SendWithCheckRights(client, user, chatId.Value, roles, replyPrams, callbackQuery.Id, "administrator",
                                        (cl, chId, rpl, userNews, tk) => AddModeratorDeclineHandle(cl, chId, userNews, rpl, tk), token),
                "EditButton" => await SendWithCheckRights(client, user, chatId.Value, roles, replyPrams, callbackQuery.Id, "administrator",
                                        (cl, chId, rpl, userNews, tk) => EditButtonHandle(cl, chId, userNews, rpl, tk), token),
                "EditButtonDecline" => await SendWithCheckRights(client, user, chatId.Value, roles, replyPrams, callbackQuery.Id, "administrator",
                                        (cl, chId, rpl, userNews, tk) => EditButtonDeclineHandle(cl, chId, userNews, rpl, tk), token),
                _ => await SendWithCheckRights(client, user, chatId.Value, roles, replyPrams, callbackQuery.Id, "user",
                                        (cl, chId, rpl, userNews, tk) => SendUserNotImplemented(cl, chId, rpl), token),
            };
        }

        private async Task<bool> SendWithCheckRights(
            TelegramBotClient client,
            Db.Model.User user,            
            long chatId,             
            string[] roles, 
            ReplyParameters? replyPrams,             
            string callbackQueryId, 
            string role,
            Func<TelegramBotClient, long, ReplyParameters?, Db.Model.News?, CancellationToken, Task> succesAction,
            CancellationToken token)
        {
            var result = false;
            var userNews = await GetCurrentNews(user, token);
            if (!roles.Contains(role))
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
            var userNews = await _newsRepo.GetAsync(newsId, token);
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
            var userNews = await _newsRepo.GetAsync(newsId, token);
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
            var existsPrev = false;
            var existsNext = false;
            var userNewses = (await _newsRepo.GetAsync(new Filter<News>()
            {
                Selector = s=> s.State == "accepted" && s.Type == "news"
            }, token)).OrderBy(s => s.CreatedDate);

            var allCount = userNewses.Count();

            if (offset > 0 && allCount > 1)
            {
                existsPrev = true;
            }

            if(offset + 1 > allCount)
            {
                offset = allCount - 1;
            }

            if(offset < allCount - 1)
            {
                existsNext = true;
            }

            var userNews = userNewses.Skip(offset).FirstOrDefault();

            if (userNews != null)
            {
                await SendNewsMessageForApprove(client, chatId, userNews, existsPrev, existsNext, offset, replyPrams, token);
            }
            else
            {
                await ApproveNewsMessageNotFound(client, chatId, replyPrams);
            }
        }

        private async Task SendNewsChoiceHandle(TelegramBotClient client, long chatId, Db.Model.User user, News? userNews, int buttonNumber, ReplyParameters? replyPrams, CancellationToken token)
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

        private async Task SendSwitchNotifyHandle(TelegramBotClient client, long chatId, Db.Model.User user, ReplyParameters? replyPrams, CancellationToken token)
        {
            user.IsNotify = !user.IsNotify;
            await _userRepo.UpdateAsync(user, true, token);


            await client.SendMessageAsync(chatId, $"Уведомления {(user.IsNotify? "включены":"выключены")}",
                replyParameters: replyPrams, cancellationToken: token);
        }

        private async Task SendAddAdminChoiceHandle(TelegramBotClient client, long chatId, Db.Model.User user, News? userNews, ReplyParameters? replyPrams, CancellationToken token)
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

        private async Task SendAddModeratorChoiceHandle(TelegramBotClient client, long chatId, Db.Model.User user, News? userNews, ReplyParameters? replyPrams, CancellationToken token)
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

        private async Task SendEditButtonChoiceHandle(TelegramBotClient client, long chatId, Db.Model.User user, News? userNews, ReplyParameters? replyPrams, CancellationToken token)
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
            var messages = (await _newsMessageRepo.GetAsync(new Filter<NewsMessage>()
            {
                Selector = s => s.NewsId == userNews.Id
            }, token)).Select(s => (int)s.TGMessageId);

            if (!messages.Any())
            {                
                await client.SendMessageAsync(chatId, "Обращение создано некорректно, отправьте не менее одного сообщения", cancellationToken: token);
                return;
            }
            userNews.State = "accepted";
            await _newsRepo.UpdateAsync(userNews, true, token);
            await client.SendMessageAsync(chatId, "Обращение принято в обработку", cancellationToken: token, replyParameters: replyParameters);

            var notifyModerators = await _userRepo.GetAsync(new Filter<Db.Model.User>()
            {
                Selector = s => !s.IsDeleted && s.IsNotify
            }, token);

            foreach(var moder in notifyModerators)
            {
                var roles = await GetUserRoles(moder, token);
                if(roles?.Any(s => s == "moderator") == true)
                {
                    await SendNewsMessageForApprove(client, moder.ChatId, userNews, false, false, 0, replyParameters, token);
                }
            }
        }

        private async Task AddAdminAccepted(TelegramBotClient client, long chatId, News userNews, ReplyParameters? replyParameters, CancellationToken token)
        {
            var messages = (await _newsMessageRepo.GetAsync(new Filter<NewsMessage>()
            {
                Selector = s => s.NewsId == userNews.Id
            }, token));

            if (!messages.Any())
            {
                await client.SendMessageAsync(chatId, "Не отправлено ни одного логина", cancellationToken: token);
                return;
            }

            foreach(var message in messages)
            {
                var logins = message.TextValue?.Split(",").Select(s => s.Trim()).Select(s => s.TrimStart('@')).Where(s => s != string.Empty);
                if(logins != null && logins.Any())
                {
                    foreach(var login in logins)
                    {
                        var user = (await _userRepo.GetAsync(new Filter<Db.Model.User>() { 
                            Selector = s => s.TGLogin != null && s.TGLogin == login
                        }, token)).FirstOrDefault();

                        if(user != null)
                        {
                            var adminRole = (await _roleRepo.GetAsync(new Filter<Role>() { Selector = s => s.Name == "administrator" }, token)).First();

                            await _userRoleRepo.AddAsync(new UserRole()
                            {
                                Id = Guid.NewGuid(),
                                IsDeleted = false,
                                RoleId = adminRole.Id,
                                UserId = user.Id
                            }, true, token);
                        }
                    }
                }
            }

            userNews.State = "approved";
            await _newsRepo.UpdateAsync(userNews, true, token);
            await client.SendMessageAsync(chatId, "Администраторы добавлены", cancellationToken: token, replyParameters: replyParameters);
        }

        private async Task EditButtonAccepted(TelegramBotClient client, long chatId, News userNews, ReplyParameters? replyParameters, CancellationToken token)
        {
            var messages = (await _newsMessageRepo.GetAsync(new Filter<NewsMessage>()
            {
                Selector = s => s.NewsId == userNews.Id
            }, token));

            if (messages.Count == 0)
            {
                await client.SendMessageAsync(chatId, "Не отправлено ни одной кнопки", cancellationToken: token);
                return;
            }

            var buttons = new List<string>();

            foreach (var message in messages.Where(s => s.TextValue != null))
            {
                var values = message.TextValue.Split(["\r\n", ";"],
                    StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Where(s => s != null && s != string.Empty);
                buttons.AddRange(values);
            }

            var numbers = new List<ButtonSetting>();
            foreach (var item in buttons)
            {
                var itemElements = item.Split(":").Select(s => s.Trim()).ToArray();
                if (int.TryParse(itemElements[0], out int num))
                {
                    string? name = null;
                    if(itemElements.Length > 1)
                    {
                        name = itemElements[1];
                    }
                    numbers.Add(new ButtonSetting()
                    {
                        Number = num, Name = name
                    });
                }
            }

            if (numbers.Count == 0)
            {
                await client.SendMessageAsync(chatId, "Не отправлено ни одной кнопки", cancellationToken: token);
                return;
            }

            var groupped = numbers.GroupBy(s => s.Number);
            if(groupped.Any(s => s.Count() > 1))
            {
                await client.SendMessageAsync(chatId, "Для некоторых кнопок отправлено больше одной настройки, перезапустите настройку", cancellationToken: token);
                return;
            }

            var allButtons = await _newsButtonRepo.GetAsync(new Filter<NewsButton>()
            {
                Selector = s => !s.IsDeleted
            }, token);
            
            foreach(var button in allButtons)
            {
                var newItem = numbers.FirstOrDefault(s => s.Number == button.ButtonNumber);
                if(newItem != null)
                {
                    button.ToSend = true;
                    button.ButtonName = newItem.Name;
                    await _newsButtonRepo.UpdateAsync(button, true, token);
                }
                else if(button.ToSend)
                {
                    button.ToSend = false;
                    await _newsButtonRepo.UpdateAsync(button, true, token);
                }
            }

            userNews.State = "approved";
            await _newsRepo.UpdateAsync(userNews, true, token);
            await client.SendMessageAsync(chatId, "Кнопки сохранены", cancellationToken: token, replyParameters: replyParameters);
        }

        private async Task AddAdminModeratorDeclined(TelegramBotClient client, long chatId, News userNews, ReplyParameters? replyParameters, CancellationToken token)
        {
            var messages = (await _newsMessageRepo.GetAsync(new Filter<NewsMessage>()
            {
                Selector = s => s.NewsId == userNews.Id
            }, token));
                        
            userNews.State = "declined";
            await _newsRepo.UpdateAsync(userNews, true, token);
            await client.SendMessageAsync(chatId, "Задание отменено", cancellationToken: token, replyParameters: replyParameters);
        }

        private async Task AddModeratorAccepted(TelegramBotClient client, long chatId, News userNews, ReplyParameters? replyParameters, CancellationToken token)
        {
            var messages = (await _newsMessageRepo.GetAsync(new Filter<NewsMessage>()
            {
                Selector = s => s.NewsId == userNews.Id
            }, token));

            if (!messages.Any())
            {
                await client.SendMessageAsync(chatId, "Не отправлено ни одного логина", cancellationToken: token);
                return;
            }

            foreach (var message in messages)
            {
                var logins = message.TextValue?.Split(",").Select(s => s.Trim()).Select(s => s.TrimStart('@')).Where(s => s != string.Empty);
                if (logins != null && logins.Any())
                {
                    foreach (var login in logins)
                    {
                        var user = (await _userRepo.GetAsync(new Filter<Db.Model.User>()
                        {
                            Selector = s => s.TGLogin != null && s.TGLogin == login
                        }, token)).FirstOrDefault();

                        if (user != null)
                        {
                            var adminRole = (await _roleRepo.GetAsync(new Filter<Role>() { Selector = s => s.Name == "moderator" }, token)).First();

                            await _userRoleRepo.AddAsync(new UserRole()
                            {
                                Id = Guid.NewGuid(),
                                IsDeleted = false,
                                RoleId = adminRole.Id,
                                UserId = user.Id
                            }, true, token);
                        }
                    }
                }
            }

            userNews.State = "approved";
            await _newsRepo.UpdateAsync(userNews, true, token);
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
            userNews.State = "deleted";
            userNews.IsDeleted = true;
            await _newsRepo.UpdateAsync(userNews, true, token);
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

        private async Task SendNewsMessageForUser(TelegramBotClient client, long chatId, int buttonNumber, Db.Model.User user, ReplyParameters? replyParameters, CancellationToken token)
        {
            var button = (await _newsButtonRepo.GetAsync(new Filter<NewsButton>()
            {
                Selector = s => s.ButtonNumber == buttonNumber
            }, token)).FirstOrDefault();

            if(button == null || button.IsDeleted || !button.ToSend)
            {
                await client.SendMessageAsync(chatId, "Недействительное направление обращения, выберите другое", 
                    replyParameters: replyParameters, cancellationToken: token);
                return;
            }

            await _newsRepo.AddAsync(new News()
            {
                IsDeleted = false,
                Id = Guid.NewGuid(),
                UserId = user.Id,
                State = "create",
                Title = "Новое обращение",
                ChatId = chatId,
                Description = "Новое обращение",
                Type = "news",
                GroupId = button.ChatId,
                ThreadId = button.ThreadId,
                CreatedDate = DateTime.Now
            }, true, token);
                       
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

        private async Task SendAddAdminForUser(TelegramBotClient client, long chatId, Db.Model.User user, ReplyParameters? replyPrams, CancellationToken token)
        {
            await _newsRepo.AddAsync(new News()
            {
                IsDeleted = false,
                Id = Guid.NewGuid(),
                UserId = user.Id,
                State = "create",
                Title = "Добавление администратора",
                ChatId = chatId,
                Description = "Добавление администратора",
                Type = "addadmin",
                CreatedDate = DateTime.Now
            }, true, token);

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

        private async Task SendAddModeratorForUser(TelegramBotClient client, long chatId, Db.Model.User user, ReplyParameters? replyPrams, CancellationToken token)
        {
            await _newsRepo.AddAsync(new News()
            {
                IsDeleted = false,
                Id = Guid.NewGuid(),
                UserId = user.Id,
                State = "create",
                Title = "Добавление модератора",
                ChatId = chatId,
                Description = "Добавление модератора",
                Type = "addmoderator",
                CreatedDate = DateTime.Now
            }, true, token);

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

        private async Task SendEditButtonForUser(TelegramBotClient client, long chatId, Db.Model.User user, ReplyParameters? replyPrams, CancellationToken token)
        {
            var availableButtons = await _newsButtonRepo.GetAsync(new Filter<NewsButton>() { Selector = s => s.IsDeleted == false }, token);
            if(availableButtons.Count != 0)
            {
                await _newsRepo.AddAsync(new News()
                {
                    IsDeleted = false,
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    State = "create",
                    Title = "Изменение кнопок",
                    ChatId = chatId,
                    Description = "Изменение кнопок",
                    Type = "editbutton",
                    CreatedDate = DateTime.Now
                }, true, token);

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
            var availableButtons = await _newsButtonRepo.GetAsync(new Filter<NewsButton>() { Selector = s => s.IsDeleted == false }, token);
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
                    " и нажмите кнопку Сохранить, либо Отменить для отмены изменения кнопок",
                    replyMarkup: replyMarkup, replyParameters: replyParameters);
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
            var messages = (await _newsMessageRepo.GetAsync(new Filter<NewsMessage>()
            {
                Selector = s => s.NewsId == userNews.Id
            }, token)).Select(s => (int)s.TGMessageId);

            if(!messages.Any())
            {
                await client.SendMessageAsync(chatId, "Обращение для подтверждения создана некорректно, будет удалена", replyParameters: replyPrams, cancellationToken: token);
                await client.SendMessageAsync(userNews.ChatId, "Обращение создана некорректно, будет удалена", cancellationToken: token);
                userNews.State = "deleted";
                userNews.IsDeleted = true;
                await _newsRepo.UpdateAsync(userNews, true, token);
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

            var userButton = (await _newsButtonRepo.GetAsync(new Filter<NewsButton>() {
                Selector = s => s.ThreadId == userNews.ThreadId && s.ChatId == userNews.GroupId
            }, token)).FirstOrDefault();

            if(userButton == null)
            {
                await client.SendMessageAsync(chatId, "Обращение для подтверждения создано некорректно, будет удалена", replyParameters: replyPrams, cancellationToken: token);
                await client.SendMessageAsync(userNews.ChatId, "Обращение создано некорректно, будет удалена", cancellationToken: token);
                userNews.State = "deleted";
                userNews.IsDeleted = true;
                await _newsRepo.UpdateAsync(userNews, true, token);
            }
            else
            {
                await client.SendMessageAsync(chatId, $"Обращение для подтверждения в раздел \"{userButton.ChatName} : {userButton.ThreadName} ({userButton.ButtonName})\"", replyMarkup: replyMarkup, replyParameters: replyPrams, cancellationToken: token);
                await client.ForwardMessagesAsync(chatId, userNews.ChatId, messages, cancellationToken: token);
            }                
        }

        private async Task SendNewsMessageApproved(TelegramBotClient client, long chatId, News userNews, ReplyParameters? replyPrams, CancellationToken token)
        {
            userNews.State = "approved";           
            await _newsRepo.UpdateAsync(userNews, true, token);                       

            if(userNews.GroupId.HasValue)
            {
                await client.SendMessageAsync(chatId, "Обращение подтверждена", replyParameters: replyPrams, cancellationToken: token);
                await client.SendMessageAsync(userNews.ChatId, "Обращение подтверждена", replyParameters: replyPrams, cancellationToken: token);

                var messages = (await _newsMessageRepo.GetAsync(new Filter<NewsMessage>()
                {
                    Selector = s => s.NewsId == userNews.Id
                }, token)).Select(s => (int)s.TGMessageId);

                if (messages.Any())
                {
                    await client.ForwardMessagesAsync(userNews.GroupId.Value, userNews.ChatId, messages, messageThreadId: (int?)userNews.ThreadId, cancellationToken: token);
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
            userNews.State = "declined";           
            await _newsRepo.UpdateAsync(userNews, true, token);

            await client.SendMessageAsync(chatId, "Обращение отклонено", replyParameters: replyPrams, cancellationToken: token);
            await client.SendMessageAsync(userNews.ChatId, "Обращение отклонено", replyParameters: replyPrams, cancellationToken: token);
        }

        private Task SendUserRemember(TelegramBotClient client, long chatId, Db.Model.News? news, ReplyParameters? replyParameters, CancellationToken token)
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
            await client.SendMessageAsync(message.Chat.Id, $"Вы отправили: {message.Text}. Дополнительно: {addInfo}", cancellationToken: token, replyParameters: replyParameters);
        }

        private async Task SendMenuButtons(TelegramBotClient client, long chatId, Db.Model.User user, CancellationToken token)
        {
            var roles = await GetUserRoles(user, token);

            if (roles.Length == 0)
            {
                await client.SendMessageAsync(chatId, $"Для вас нет доступных действий", cancellationToken: token);
                return;
            }

            var buttons = new List<List<InlineKeyboardButton>>();

            if (roles.Any(s => s == "user")) buttons.AddRange(await GetUserButtons(token));
            if (roles.Any(s => s == "moderator")) buttons.AddRange(GetModeratorButtons(user));
            if (roles.Any(s => s == "administrator")) buttons.AddRange(GetAdminButtons());

            
            ReplyMarkup replyMarkup = new InlineKeyboardMarkup(buttons);
            await client.SendMessageAsync(chatId, "Выберите, что хотите сделать", replyMarkup: replyMarkup, cancellationToken: token);
        }

        private async Task<string[]> GetUserRoles(Db.Model.User user, CancellationToken token)
        {
            string[] roles = [];
            var userRoles = (await _userRoleRepo.GetAsync(new Filter<UserRole>() { Selector = s => s.UserId == user.Id }, token)).Select(s => s.RoleId).Distinct().ToArray();
            if (userRoles.Length != 0)
            {
                roles = [.. (await _roleRepo.GetAsync(new Filter<Role>() { Selector = s => userRoles.Contains(s.Id) }, token)).Select(s => s.Name)];
            }

            return roles;
        }

        private async Task<List<List<InlineKeyboardButton>>> GetUserButtons(CancellationToken token)
        {
            var buttons = await _newsButtonRepo.GetAsync(new Filter<NewsButton>()
            {
                Selector = s => !s.IsDeleted && s.ToSend
            }, token);

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

        private static List<List<InlineKeyboardButton>> GetModeratorButtons(Db.Model.User user)
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
    }

    public class ButtonSetting
    {
        public int Number { get; set; }
        public string? Name { get; set; }
    }
}
