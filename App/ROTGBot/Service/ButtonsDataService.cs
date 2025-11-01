using ROTGBot.Db.Interface;
using ROTGBot.Db.Model;
using System.Data;

namespace ROTGBot.Service
{
    public class AddButtonModel
    {
        public long ChatId { get; set; }
        public int? ThreadId { get; set; }
        public string ChatName { get; set; }
        public string? ThreadName { get; set; }
    }

    public class ButtonsDataService(IRepository<NewsButton> newsButtonRepo) : IButtonsDataService
    {
        private const string NAME_REQUIRED_MESSAGE = "Наименование группы обязательно";
        private readonly IRepository<NewsButton> _newsButtonRepo = newsButtonRepo;

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

        public async Task<List<Contract.Model.NewsButton>> GetActiveButtons(CancellationToken token)
        {
            return [.. (await _newsButtonRepo.GetAsync(new Filter<NewsButton>()
            {
                Selector = s => !s.IsDeleted && s.ToSend
            }, token)).Select(Map).Where(s => s != null)];
        }

        public async Task<List<Contract.Model.NewsButton>> GetAllButtons(CancellationToken token)
        {
            return [.. (await _newsButtonRepo.GetAsync(new Filter<NewsButton>()
            {
                Selector = s => !s.IsDeleted
            }, token)).Select(Map).Where(s => s != null)];
        }

        public async Task<Contract.Model.NewsButton?> GetButtonByNumber(int buttonNumber, CancellationToken token)
        {
            return Map((await _newsButtonRepo.GetAsync(new Filter<NewsButton>()
            {
                Selector = s => !s.IsDeleted && s.ButtonNumber == buttonNumber
            }, token)).FirstOrDefault());
        }

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

        public async Task<Contract.Model.NewsButton?> GetButtonByThreadId(long? groupId, long? threadId, CancellationToken token)
        {
            if (groupId == null) return null;
            
            return Map((await _newsButtonRepo.GetAsync(new Filter<NewsButton>()
            {
                Selector = s => !s.IsDeleted && s.ThreadId == threadId && s.ChatId == groupId
            }, token)).FirstOrDefault());
        }

        public async Task RemoveButtonSend(Guid id, CancellationToken token)
        {
            var button = await _newsButtonRepo.GetAsync(id, token);
            if(button.ToSend)
            {
                button.ToSend = false;
                await _newsButtonRepo.UpdateAsync(button, true, token);
            }
        }

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
