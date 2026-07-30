namespace ROTGBot.Service
{
    public interface IHandler<T>
    {
        Task<bool> Handle(T? message, CancellationToken cancellationToken);
    }
}
