using ROTGBot.Db.Interface;
using ROTGBot.Db.Model;
using System.Data;

namespace ROTGBot.Service
{
    /// <summary>
    /// Сервис работы с пользовательскими кнопками
    /// </summary>
    /// <param name="newsButtonRepo"></param>
    public class ButtonsDataService(IRepository<NewsButton> newsButtonRepo) : IButtonsDataService
    {
        private const string NAME_REQUIRED_MESSAGE = "Наименование группы обязательно";
        private readonly IRepository<NewsButton> _newsButtonRepo = newsButtonRepo;

        /// <summary>
        /// Добавить новую кнопку
        /// </summary>
        /// <param name="chatId">ИД чата (группы)</param>
        /// <param name="threadId">ИД темы в группе</param>
        /// <param name="chatName">Имя чата (группы)</param>
        /// <param name="threadName">Имя темы в группе</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public async Task<bool> AddNewButton(long chatId, int? threadId, string chatName, string? threadName, CancellationToken cancellationToken)
        {
            if(string.IsNullOrEmpty(chatName))
            {
                throw new ArgumentException(NAME_REQUIRED_MESSAGE);
            }

            var allButtons = await _newsButtonRepo.GetAsync(new Filter<NewsButton>()
            {
                Selector = s => !s.IsDeleted
            }, cancellationToken);

            var exists = allButtons.FirstOrDefault(s => s.ChatId == chatId && s.ThreadId == threadId);

            if (exists != null || threadName == null)
            {
                return false;
            }

            await _newsButtonRepo.AddAsync(new NewsButton()
            {
                ChatId = chatId,
                ChatName = chatName,
                Id = Guid.NewGuid(),
                IsDeleted = false,
                ThreadId = threadId,
                ThreadName = threadName,
                ToSend = false,
                ButtonNumber = allButtons.Count != 0 ? allButtons.Max(s => s.ButtonNumber) + 1 : 1,
                IsModerate = false
            }, true, cancellationToken);

            return true;
        }

        /// <summary>
        /// Добавить родительскую кнопку (категорий)
        /// </summary>
        /// <param name="name">Наименование</param>
        /// <param name="parent">ИД родительской кнопки</param>
        /// <param name="cancellationToken">токен</param>
        /// <returns></returns>
        public async Task<bool> AddParentButton(string name, int? parent, CancellationToken cancellationToken)
        {
            var exists = await _newsButtonRepo.GetAsync(new Filter<NewsButton>()
            {
                Selector = s => !s.IsDeleted && s.ParentId == parent && s.ButtonName == name
            }, cancellationToken);

            var allButtons = await _newsButtonRepo.GetAsync(new Filter<NewsButton>()
            {
                Selector = s => !s.IsDeleted
            }, cancellationToken);


            if (exists.Count != 0)
            {
                return false;
            }

            await _newsButtonRepo.AddAsync(new NewsButton()
            {
                ChatName = name,
                Id = Guid.NewGuid(),
                IsDeleted = false,
                ToSend = true,
                ButtonNumber = allButtons.Count != 0 ? allButtons.Max(s => s.ButtonNumber) + 1 : 1,
                IsParent = true,
                ParentId = parent,
                ButtonName = name

            }, true, cancellationToken);

            return true;
        }

        /// <summary>
        /// Активные (включенные) кнопки
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        public async Task<List<Contract.Model.NewsButton>> GetActiveButtons(CancellationToken token)
        {
            return [.. (await _newsButtonRepo.GetAsync(new Filter<NewsButton>()
            {
                Selector = s => !s.IsDeleted && s.ToSend
            }, token)).Select(Map).Where(s => s != null)];
        }

        /// <summary>
        /// Получить все кнопки
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        public async Task<List<Contract.Model.NewsButton>> GetAllButtons(CancellationToken token)
        {
            return [.. (await _newsButtonRepo.GetAsync(new Filter<NewsButton>()
            {
                Selector = s => !s.IsDeleted
            }, token)).Select(Map).Where(s => s != null)];
        }

        /// <summary>
        /// Кнопка по номеру
        /// </summary>
        /// <param name="buttonNumber">Номер кнопки</param>
        /// <param name="token"></param>
        /// <returns></returns>
        public async Task<Contract.Model.NewsButton?> GetButtonByNumber(int buttonNumber, CancellationToken token)
        {
            return Map((await _newsButtonRepo.GetAsync(new Filter<NewsButton>()
            {
                Selector = s => !s.IsDeleted && s.ButtonNumber == buttonNumber
            }, token)).FirstOrDefault());
        }

        /// <summary>
        /// Маппинг моедли БД в контракт
        /// </summary>
        /// <param name="newsButton">Модель БД</param>
        /// <returns></returns>
        private Contract.Model.NewsButton? Map(NewsButton? newsButton)
        {
            if(newsButton == null) return null;

            return new Contract.Model.NewsButton()
            {
                Id = newsButton.Id,
                ButtonName = newsButton.ButtonName,
                ButtonNumber = newsButton.ButtonNumber,
                ChatId = newsButton.ChatId,
                ChatName = newsButton.ChatName,
                ThreadId = newsButton.ThreadId,
                ThreadName = newsButton.ThreadName,
                ToSend = newsButton.ToSend,
                ParentId = newsButton.ParentId,
                IsParent = newsButton.IsParent,
                IsModerate = newsButton.IsModerate
            };
        }

        /// <summary>
        /// Получить кнопку по ИД темы группы
        /// </summary>
        /// <param name="groupId">ИД темы</param>
        /// <param name="threadId">ИД группы</param>
        /// <param name="token"></param>
        /// <returns></returns>
        public async Task<Contract.Model.NewsButton?> GetButtonByThreadId(long? groupId, long? threadId, CancellationToken token)
        {
            if (groupId == null) return null;
            
            return Map((await _newsButtonRepo.GetAsync(new Filter<NewsButton>()
            {
                Selector = s => !s.IsDeleted && s.ThreadId == threadId && s.ChatId == groupId
            }, token)).FirstOrDefault());
        }

        /// <summary>
        /// Деактивация кнопки
        /// </summary>
        /// <param name="id">ИД кнопки</param>
        /// <param name="token"></param>
        /// <returns></returns>
        public async Task RemoveButtonSend(Guid id, CancellationToken token)
        {
            var button = await _newsButtonRepo.GetAsync(id, token);
            if(button.ToSend)
            {
                button.ToSend = false;
                await _newsButtonRepo.UpdateAsync(button, true, token);
            }
        }

        /// <summary>
        /// Активация кнопки
        /// </summary>
        /// <param name="id">ИД кнопки</param>
        /// <param name="name">Наименование</param>
        /// <param name="parentId">Родитель</param>
        /// <param name="isModerate">Модерируемая</param>
        /// <param name="token"></param>
        /// <returns></returns>
        public async Task SetButtonSend(Guid id, string? name, int? parentId, bool isModerate, CancellationToken token)
        {
            var button = await _newsButtonRepo.GetAsync(id, token);
            button.ToSend = true;
            button.ButtonName = name;
            button.ParentId = parentId;
            button.IsModerate = isModerate;
            await _newsButtonRepo.UpdateAsync(button, true, token);
        }
    }
}
