using ROTGBot.Contract.Model;

namespace ROTGBot.Service
{
    public interface INewsDataService
    {
        Task AddNewMessageForNews(long messageId, Guid userNewsId, string text, CancellationToken cancellationToken);
        Task CreateNews(long chatId, Guid id, long? groupId, long? threadId, string type, string title, CancellationToken token);
        Task<News?> GetCurrentNews(Guid userId, CancellationToken cancellationToken);
        Task<News?> GetNewsById(Guid id, CancellationToken token);
        Task<List<News>> GetNewsForApprove(CancellationToken token);
        Task<List<NewsMessage>> GetNewsMessages(Guid newsId, CancellationToken token);
        Task SetNewsAccepted(Guid id, CancellationToken token);
        Task SetNewsApproved(Guid id, CancellationToken token);
        Task SetNewsDeclined(Guid id, CancellationToken token);
        Task SetNewsDeleted(Guid id, CancellationToken token);
    }
}