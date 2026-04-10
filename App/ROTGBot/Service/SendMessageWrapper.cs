using Microsoft.Extensions.Logging;
using ROTGBot.Contract.Model;
using Telegram.BotAPI.AvailableTypes;

namespace ROTGBot.Service
{
    public class SendMessageWrapper : ISendMessageWrapper
    {
        private readonly ILogger<SendMessageWrapper> _logger;                
        private readonly IButtonsDataService _buttonsDataService;
        private readonly ITelegramBotWrapper client;

        public SendMessageWrapper(
            ILogger<SendMessageWrapper> logger,            
            IButtonsDataService buttonsDataService,
            ITelegramBotWrapper wrapper)
        {
            _logger = logger;            
            _buttonsDataService = buttonsDataService;           
            client = wrapper;
        }

        public Task SendUserRemember(long chatId, News? news, CancellationToken token)
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

        public async Task SendMenuButtons(long chatId, Contract.Model.User user, string type, CancellationToken token)
        {
            if (type == "all")
            {
                if (user.IsModerator || user.IsAdmin)
                {
                    await client.SendMessageAsync(chatId, "Выберите раздел",
                        replyMarkup: new InlineKeyboardMarkup(GetMenuButtons(user)), token);
                }
                else
                {
                    await client.SendMessageAsync(chatId, "Панель пользователя",
                        replyMarkup: new InlineKeyboardMarkup(await GetUserButtons(token)), token);
                }
            }

            if (type == "user")
            {
                await client.SendMessageAsync(chatId, "Панель пользователя",
                         replyMarkup: new InlineKeyboardMarkup(await GetUserButtons(token)), token);
            }

            if (type == "moderator")
            {
                if (user.IsModerator)
                {
                    await client.SendMessageAsync(chatId, "Панель модератора",
                        replyMarkup: new InlineKeyboardMarkup(GetModeratorButtons(user)), token);
                }
                else
                {
                    await client.SendMessageAsync(chatId, "У вас нет доступа к этому разделу", token);
                }
            }

            if (type == "admin")
            {
                if (user.IsAdmin)
                {
                    await client.SendMessageAsync(chatId, "Панель администратора",
                        replyMarkup: new InlineKeyboardMarkup(GetAdminButtons()), token);
                }
                else
                {
                    await client.SendMessageAsync(chatId, "У вас нет доступа к этому разделу", token);
                }
            }
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

        public static string GetTabs(int count)
        {
            var result = "";
            for (int i = 0; i < count; i++)
            {
                result += "\t\t\t\t";
            }
            return result;
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

        private async Task SendEditButtonForAdminRemember(long chatId, CancellationToken token)
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
                     replyMarkup: replyMarkup, token);
            }
        }

        private async Task SendAddButtonForAdminRemember(long chatId, CancellationToken token)
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
                    " либо нажмите Отменить для отмены изменения кнопок", replyMarkup: replyMarkup, token);
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
                     replyMarkup: replyMarkup, token);
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

        private async Task SendDeleteButtonForAdminRemember(long chatId, CancellationToken token)
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
                    "Отправьте номер кнопки, которую хотите удалить, либо Отменить для отмены изменения кнопок", replyMarkup: replyMarkup, token);
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
                     replyMarkup: replyMarkup, token);
            }
        }
    }
}
