namespace ROTGBot.Contract.Interfaces
{
    /// <summary>
    /// Интерфейс базовой модели
    /// </summary>
    public interface IEntity
    {
        /// <summary>
        /// Идентификатор
        /// </summary>
        Guid Id { get; set; }
    }
}
