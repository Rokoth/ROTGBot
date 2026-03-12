namespace ROTGBot.Service
{
    public interface IHandler<T>
    {
        Task Handle(T? message, CancellationToken cancellationToken);
    }
}
