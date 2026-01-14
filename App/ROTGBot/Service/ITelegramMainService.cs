namespace ROTGBot.Service
{
    public interface ITelegramMainService
    {
        Task<int> Execute(int offset);

        Task<bool> SetCommands(CancellationToken token);
    }
}