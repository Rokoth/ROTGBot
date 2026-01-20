using ROTGBot.Contract.Model;

namespace ROTGBot.Service
{
    public interface INewsDataService
    {
        Task<bool> AddNewMessageForNews(long messageId, Guid userNewsId, string text, CancellationToken cancellationToken);
        Task CreateNews(long chatId, Guid id, long? groupId, long? threadId, string type, string title, bool isModerate, CancellationToken token);
        Task<News?> GetCurrentNews(Guid userId, CancellationToken cancellationToken);
        Task<News?> GetNewsById(Guid id, CancellationToken token);
        Task<List<News>> GetNewsForApprove(CancellationToken token);
        Task<List<NewsMessage>> GetNewsMessages(Guid newsId, CancellationToken token);
        Task SetNewsAccepted(Guid id, CancellationToken token);
        Task SetNewsApproved(Guid id, Guid moderatorId, CancellationToken token);
        Task SetNewsDeclined(Guid id, Guid moderatorId, CancellationToken token);
        Task SetNewsDeleted(Guid id, CancellationToken token);
        Task SetNewsMulti(Guid id, CancellationToken token);

        Task<string> GetAdminUserReportString(CancellationToken token);
        Task<string> GetAdminModeratorReportString(CancellationToken token);
        Task<string> GetModeratorReportString(Guid id, CancellationToken token);
        Task<string> GetUserReportString(Guid id, CancellationToken token);

        Task<AdminUserReport> GetAdminUserReport(CancellationToken token);
        Task<AdminModeratorReport> GetAdminModeratorReport(CancellationToken token);
        Task<Report> GetModeratorReport(Guid id, CancellationToken token);
        Task<Report> GetUserReport(Guid id, CancellationToken token);
    }
}