using ROTGBot.Db.Interface;
using ROTGBot.Db.Model;
using System.Data;
using System.Linq;

namespace ROTGBot.Service
{
    public class NewsDataService(
        IRepository<News> newsRepo,
        IRepository<NewsMessage> newsMessageRepo) : INewsDataService
    {
        private readonly IRepository<News> _newsRepo = newsRepo;
        private readonly IRepository<NewsMessage> _newsMessageRepo = newsMessageRepo;

        public async Task AddNewMessageForNews(long messageId, Guid userNewsId, string text, CancellationToken cancellationToken)
        {
            await _newsMessageRepo.AddAsync(new NewsMessage()
            {
                Id = Guid.NewGuid(),
                IsDeleted = false,
                NewsId = userNewsId,
                TGMessageId = messageId,
                TextValue = text
            }, true, cancellationToken);
        }

        public async Task<Contract.Model.News?> GetCurrentNews(Guid userId, CancellationToken cancellationToken)
        {
            var result =  (await _newsRepo.GetAsync(new Filter<News>()
            {
                Selector = s => s.UserId == userId
            }, cancellationToken)).FirstOrDefault(s => s.State == "create");
            return Map(result);
        }

        private static Contract.Model.News? Map(News? result)
        {
            if (result == null) return null;

            return new Contract.Model.News()
            {
                ChatId = result.ChatId,
                CreatedDate = result.CreatedDate,
                Description = result.Description,
                GroupId = result.GroupId,
                Id = result.Id,
                State = result.State,
                ThreadId = result.ThreadId,
                Title = result.Title,
                Type = result.Type,
                UserId = result.UserId,
                IsMulti = result.IsMulti
            };
        }

        private static Contract.Model.NewsMessage? Map(NewsMessage? result)
        {
            if (result == null) return null;

            return new Contract.Model.NewsMessage()
            {
                Id = result.Id,
                NewsId = result.NewsId,
                TextValue = result.TextValue,
                TGMessageId = result.TGMessageId
            };
        }

        public async Task<Contract.Model.News?> GetNewsById(Guid id, CancellationToken token)
        {
            return Map(await _newsRepo.GetAsync(id, token));
        }

        public async Task<List<Contract.Model.News>> GetNewsForApprove(CancellationToken token)
        {
            return Map((await _newsRepo.GetAsync(new Filter<News>()
            {
                Selector = s => s.State == "accepted" && s.Type == "news"
            }, token)).OrderBy(s => s.CreatedDate));
        }

        private static List<Contract.Model.News> Map(IEnumerable<Db.Model.News> news)
        {
            List<Contract.Model.News> result = [];
            foreach(var item in news)
            {
                var map = Map(item);
                if (map != null)
                    result.Add(map);
            }
            return result;
        }

        private static List<Contract.Model.NewsMessage> Map(IEnumerable<Db.Model.NewsMessage> news)
        {
            List<Contract.Model.NewsMessage> result = [];
            foreach (var item in news)
            {
                var map = Map(item);
                if (map != null)
                    result.Add(map);
            }
            return result;
        }

        public async Task<List<Contract.Model.NewsMessage>> GetNewsMessages(Guid newsId, CancellationToken token)
        {
            return Map((await _newsMessageRepo.GetAsync(new Filter<NewsMessage>()
            {
                Selector = s => s.NewsId == newsId
            }, token)));
        }

        public async Task SetNewsAccepted(Guid id, CancellationToken token)
        {
            await SetNewsStatus(id, "accepted", false, token);
        }

        private async Task SetNewsStatus(Guid id, string state, bool toDelete, CancellationToken token)
        {
            var userNews = await _newsRepo.GetAsync(id, token);
            userNews.State = state;
            if (toDelete) userNews.IsDeleted = true;
            await _newsRepo.UpdateAsync(userNews, true, token);
        }

        public async Task SetNewsApproved(Guid id, CancellationToken token)
        {
            await SetNewsStatus(id, "approved", false, token);            
        }

        public async Task SetNewsDeclined(Guid id, CancellationToken token)
        {
            await SetNewsStatus(id, "declined", false, token);            
        }

        public async Task SetNewsDeleted(Guid id, CancellationToken token)
        {
            await SetNewsStatus(id, "deleted", true, token);           
        }

        public async Task CreateNews(long chatId, Guid userId, long? groupId, long? threadId, string type, string title, CancellationToken token)
        {
            await _newsRepo.AddAsync(new News()
            {
                IsDeleted = false,
                Id = Guid.NewGuid(),
                UserId = userId,
                State = "create",
                Title = title,
                ChatId = chatId,
                Description = title,
                Type = type,
                GroupId = groupId,
                ThreadId = threadId,
                CreatedDate = DateTime.Now,
                IsMulti = false
            }, true, token);
        }

        public async Task SetNewsMulti(Guid id, CancellationToken token)
        {
            var userNews = await _newsRepo.GetAsync(id, token);
            userNews.IsMulti = true;           
            await _newsRepo.UpdateAsync(userNews, true, token);
        }
    }
}
