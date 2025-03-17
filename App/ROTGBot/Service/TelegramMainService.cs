using Microsoft.Extensions.Logging;
using ROTGBot.Db.Interface;
using ROTGBot.Db.Model;
using System.Collections.Generic;
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
        IRepository<ROTGBot.Db.Model.Groups> groupsRepo) : ITelegramMainService
    {
        private const string botToken = "7802404850:AAFNQst8GQjwPQRTjtzpUwK147fdMSV6FCc";
        private ILogger<TelegramMainService> _logger = logger;
        private IRepository<ROTGBot.Db.Model.User> _userRepo = userRepo;
        private IRepository<ROTGBot.Db.Model.Role> _roleRepo = roleRepo;
        private IRepository<ROTGBot.Db.Model.UserRole> _userRoleRepo = userRoleRepo;
        private IRepository<ROTGBot.Db.Model.News> _newsRepo = newsRepo;
        private IRepository<ROTGBot.Db.Model.NewsMessage> _newsMessageRepo = newsMessageRepo;
        private IRepository<ROTGBot.Db.Model.Groups> _groupsRepo = groupsRepo;

        public async Task<int> Execute(int offset)
        {
            CancellationToken cancellationToken = new CancellationTokenSource(60000).Token;
            var client = new TelegramBotClient(botToken);                 
            
            var updates = await client.GetUpdatesAsync(offset);
            if (updates?.Any() == true)
            {
                foreach (var update in updates)
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
                var user = await GetUser(message.From, cancellationToken);
                addMessage += $"Привет, {user.Name}!";
                var userNews = await GetCurrentNews(user, cancellationToken);

                if (message.Text == "/start")
                {
                    if (userNews != null)
                    {
                        await SendUserRemember(client, message.Chat.Id, userNews, GetReplyParameters(message.MessageId));
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
                //else
                //{
                //    await SendTestConnectionMessage(client, message, addMessage, GetReplyParameters(message.MessageId), cancellationToken);
                //}
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

        private async Task<Db.Model.User> GetUser(User tguser, CancellationToken cancellationToken)
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
                    TGId = tguser.Id
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

            return user;
        }

        private async Task<bool> HandleCallback(TelegramBotClient client, CallbackQuery callbackQuery, CancellationToken token)
        {

            var chatId = callbackQuery.Message?.Chat.Id;
            if (chatId == null)
            {
                return false;
            }

            var user = await GetUser(callbackQuery.From, token);
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

            return data switch
            {
                "SendNewsChoice" => await SendWithCheckRights(client, user, chatId.Value, roles, replyPrams, callbackQuery.Id, "user",
                                        (cl, chId, rpl, userNews, tk) => SendNewsChoiceHandle(cl, chId, user, userNews, rpl, tk), token),
                "SendNews" => await SendWithCheckRights(client, user, chatId.Value, roles, replyPrams, callbackQuery.Id, "user",
                                        (cl, chId, rpl, userNews, tk) => SendNewsHandle(cl, chId, userNews, rpl, tk), token),
                "DeleteNews" => await SendWithCheckRights(client, user, chatId.Value, roles, replyPrams, callbackQuery.Id, "user",
                                        (cl, chId, rpl, userNews, tk) => DeleteNewsHandle(cl, chId, userNews, rpl, tk), token),
                "ApproveNewsChoice" => await SendWithCheckRights(client, user, chatId.Value, roles, replyPrams, callbackQuery.Id, "moderator",
                                        (cl, chId, rpl, userNews, tk) => SendNewsChoiceApproveHandle(cl, chId, rpl, tk), token),
                "ApproveNews" => await SendWithCheckRights(client, user, chatId.Value, roles, replyPrams, callbackQuery.Id, "moderator",
                                        (cl, chId, rpl, userNews, tk) => SendNewsApproveHandle(cl, chId, newsId.Value, rpl, tk), token),
                "DeclineNews" => await SendWithCheckRights(client, user, chatId.Value, roles, replyPrams, callbackQuery.Id, "moderator",
                                        (cl, chId, rpl, userNews, tk) => SendNewsDeclineHandle(cl, chId, newsId.Value, rpl, tk), token),
                "AddAdminChoice" => await SendWithCheckRights(client, user, chatId.Value, roles, replyPrams, callbackQuery.Id, "administrator",
                                        (cl, chId, rpl, userNews, tk) => SendAddAdminChoiceHandle(cl, chId, user, userNews, rpl, tk), token),
                "AddModeratorChoice" => await SendWithCheckRights(client, user, chatId.Value, roles, replyPrams, callbackQuery.Id, "administrator",
                                        (cl, chId, rpl, userNews, tk) => SendAddModeratorChoiceHandle(cl, chId, user, userNews, rpl, tk), token),
                "AddAdmin" => await SendWithCheckRights(client, user, chatId.Value, roles, replyPrams, callbackQuery.Id, "administrator",
                                        (cl, chId, rpl, userNews, tk) => AddAdminHandle(cl, chId, userNews, rpl, tk), token),
                "AddAdminDecline" => await SendWithCheckRights(client, user, chatId.Value, roles, replyPrams, callbackQuery.Id, "administrator",
                                        (cl, chId, rpl, userNews, tk) => AddAdminDeclineHandle(cl, chId, userNews, rpl, tk), token),
                "AddModerator" => await SendWithCheckRights(client, user, chatId.Value, roles, replyPrams, callbackQuery.Id, "administrator",
                                        (cl, chId, rpl, userNews, tk) => AddModeratorHandle(cl, chId, userNews, rpl, tk), token),
                "AddModeratorDecline" => await SendWithCheckRights(client, user, chatId.Value, roles, replyPrams, callbackQuery.Id, "administrator",
                                        (cl, chId, rpl, userNews, tk) => AddModeratorDeclineHandle(cl, chId, userNews, rpl, tk), token),

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

        private async Task SendNewsChoiceApproveHandle(TelegramBotClient client, long chatId, ReplyParameters? replyPrams, CancellationToken token)
        {
            var userNews = (await _newsRepo.GetAsync(new Filter<News>()
            {
                Selector = s=> s.State == "accepted" && s.Type == "news"
            }, token)).OrderBy(s => s.Id).FirstOrDefault();
            if (userNews != null)
            {
                await SendNewsMessageForApprove(client, chatId, userNews, replyPrams, token);
            }
            else
            {
                await ApproveNewsMessageNotFound(client, chatId, replyPrams);
            }
        }

        private async Task SendNewsChoiceHandle(TelegramBotClient client, long chatId, Db.Model.User user, News? userNews, ReplyParameters? replyPrams, CancellationToken token)
        {
            if (userNews != null)
            {
                await SendNewsMessageForUserRemember(client, chatId, replyPrams);
            }
            else
            {
                await SendNewsMessageForUser(client, chatId, user, replyPrams, token);
            }
        }

        private async Task SendAddAdminChoiceHandle(TelegramBotClient client, long chatId, Db.Model.User user, News? userNews, ReplyParameters? replyPrams, CancellationToken token)
        {
            if (userNews != null)
            {
                await SendAddAdminForUserRemember(client, chatId, replyPrams);
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
                await SendAddModeratorForUserRemember(client, chatId, replyPrams);
            }
            else
            {
                await SendAddModeratorForUser(client, chatId, user, replyPrams, token);
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
                await client.SendMessageAsync(chatId, "Новость создана некорректно, отправьте не менее одного сообщения", cancellationToken: token);
                return;
            }
            userNews.State = "accepted";
            await _newsRepo.UpdateAsync(userNews, true, token);
            await client.SendMessageAsync(chatId, "Новость принята в обработку", cancellationToken: token, replyParameters: replyParameters);
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

        private async Task AddAdminModeratorDeclined(TelegramBotClient client, long chatId, News userNews, ReplyParameters? replyParameters, CancellationToken token)
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
            await client.SendMessageAsync(chatId, "Нет неподтвержденных новостей", replyParameters: replyParameters);
        }

        private static async Task AddAdminMessageNotFound(TelegramBotClient client, long chatId, ReplyParameters? replyParameters)
        {
            await client.SendMessageAsync(chatId, "Нет задач на добавление администратора", replyParameters: replyParameters);
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
            await client.SendMessageAsync(chatId, "Новость удалена", cancellationToken: token, replyParameters: replyParameters);
        }

        private static async Task DeleteNewsMessageNotFound(TelegramBotClient client, long chatId, ReplyParameters? replyParameters)
        {
            await client.SendMessageAsync(chatId, "Нет неподтвержденных новостей", replyParameters: replyParameters);
        }

        private static async Task ApproveNewsMessageNotFound(TelegramBotClient client, long chatId, ReplyParameters? replyParameters)
        {
            await client.SendMessageAsync(chatId, "Нет неотправленных новостей", replyParameters: replyParameters);
        }

        private async Task SendNewsMessageForUser(TelegramBotClient client, long chatId, Db.Model.User user, ReplyParameters? replyParameters, CancellationToken token)
        {
            await _newsRepo.AddAsync(new News()
            {
                IsDeleted = false,
                Id = Guid.NewGuid(),
                UserId = user.Id,
                State = "create",
                Title = "Новая новость",
                ChatId = chatId,
                Description = "Новая новость",
                Type = "news"
            }, true, token);

            var button1 = new InlineKeyboardButton("Отправить")
            {
                CallbackData = "SendNews"
            };
            ReplyMarkup replyMarkup = new InlineKeyboardMarkup(
                new List<List<InlineKeyboardButton>>() 
                {
                    new()
                    {
                        button1
                    }
                });

            await client.SendMessageAsync(chatId, "Отправьте одно или несколько сообщений и нажмите кнопку Отправить", replyMarkup: replyMarkup, replyParameters: replyParameters, cancellationToken: token);
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
                Type = "addadmin"
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
                Type = "addmoderator"
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

        private async Task SendNewsMessageForApprove(TelegramBotClient client, long chatId, News userNews, ReplyParameters? replyPrams, CancellationToken token)
        {
            var messages = (await _newsMessageRepo.GetAsync(new Filter<NewsMessage>()
            {
                Selector = s => s.NewsId == userNews.Id
            }, token)).Select(s => (int)s.TGMessageId);

            if(!messages.Any())
            {
                await client.SendMessageAsync(chatId, "Новость для подтверждения создана некорректно, будет удалена", replyParameters: replyPrams, cancellationToken: token);
                await client.SendMessageAsync(userNews.ChatId, "Новость создана некорректно, будет удалена", cancellationToken: token);
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
            ReplyMarkup replyMarkup = new InlineKeyboardMarkup(
                new List<List<InlineKeyboardButton>>()
                {
                            new()
                            {
                                button1, button2
                            }
                });

            await client.SendMessageAsync(chatId, "Новость для подтверждения:", replyMarkup: replyMarkup, replyParameters: replyPrams, cancellationToken: token);
            await client.ForwardMessagesAsync(chatId, userNews.ChatId, messages, cancellationToken: token);
        }

        private async Task SendNewsMessageApproved(TelegramBotClient client, long chatId, News userNews, ReplyParameters? replyPrams, CancellationToken token)
        {
            userNews.State = "approved";           
            await _newsRepo.UpdateAsync(userNews, true, token);

            await client.SendMessageAsync(chatId, "Новость подтверждена", replyParameters: replyPrams, cancellationToken: token);
            await client.SendMessageAsync(userNews.ChatId, "Новость подтверждена", replyParameters: replyPrams, cancellationToken: token);

            var toSendGroups = await _groupsRepo.GetAsync(new Filter<Groups>() { 
                Selector = s=> s.SendNews
            }, token);

            var messages = (await _newsMessageRepo.GetAsync(new Filter<NewsMessage>()
            {
                Selector = s => s.NewsId == userNews.Id
            }, token)).Select(s => (int)s.TGMessageId);

            if(messages.Any())
            {
                foreach (var group in toSendGroups)
                {
                    await client.ForwardMessagesAsync(group.ChatId, userNews.ChatId, messages, messageThreadId: (int?)group.ThreadId,  cancellationToken: token);                    
                }
            }
        }

        private async Task SendNewsMessageDeclined(TelegramBotClient client, long chatId, News userNews, ReplyParameters? replyPrams, CancellationToken token)
        {
            userNews.State = "declined";           
            await _newsRepo.UpdateAsync(userNews, true, token);

            await client.SendMessageAsync(chatId, "Новость отклонена", replyParameters: replyPrams, cancellationToken: token);
            await client.SendMessageAsync(userNews.ChatId, "Новость отклонена", replyParameters: replyPrams, cancellationToken: token);
        }

        private static Task SendUserRemember(TelegramBotClient client, long chatId, Db.Model.News? news, ReplyParameters? replyParameters)
        {
            return (news?.Type) switch
            {
                "news" => SendNewsMessageForUserRemember(client, chatId, replyParameters),
                "addadmin" => SendNewsMessageForUserRemember(client, chatId, replyParameters),
                "addmoderator" => SendNewsMessageForUserRemember(client, chatId, replyParameters),
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
            await client.SendMessageAsync(chatId, "У вас есть неподтвержденная новость." +
                " Отправьте одно или несколько сообщений и нажмите кнопку Отправить, либо Отменить для отмены отправки", 
                replyMarkup: replyMarkup, replyParameters: replyParameters);
        }

        private static async Task SendAddAdminForUserRemember(TelegramBotClient client, long chatId, ReplyParameters? replyParameters)
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

        private static async Task SendAddModeratorForUserRemember(TelegramBotClient client, long chatId, ReplyParameters? replyParameters)
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

            foreach (var role in roles)
            {
                switch (role)
                {
                    case "user":
                        buttons.Add(GetUserButtons());
                        break;
                    case "administrator":
                        buttons.Add(GetAdminButtons());
                        break;
                    case "moderator":
                        buttons.Add(GetModeratorButtons());
                        break;
                }
            }

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

        private static List<InlineKeyboardButton> GetUserButtons()
        {
            return [ new InlineKeyboardButton("Отправить новость")
            {
                CallbackData = "SendNewsChoice"
            }];
        }

        private static List<InlineKeyboardButton> GetAdminButtons()
        {
            return [ new InlineKeyboardButton("Добавить администратора")
            {
                CallbackData = "AddAdminChoice"
            },new InlineKeyboardButton("Добавить модератора")
            {
                CallbackData = "AddModeratorChoice"
            }];
        }

        private static List<InlineKeyboardButton> GetModeratorButtons()
        {
            return [ new InlineKeyboardButton("Подтвердить новость")
            {
                CallbackData = "ApproveNewsChoice"
            }];
        }

        public async Task SetCommands()
        {
            var client = new TelegramBotClient(botToken);
            _ = await client.SetMyCommandsAsync(new SetMyCommandsArgs([new("start", "Начать работу")]));
        }
    }
}
