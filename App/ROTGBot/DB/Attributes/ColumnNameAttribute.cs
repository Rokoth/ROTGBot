namespace ROTGBot.Db.Attributes
{
    /// <summary>
    /// Атрибут Имя колонки БД
    /// </summary>   
    /// <param name="name"></param>
    [AttributeUsage(AttributeTargets.Property)]
    public class ColumnNameAttribute(string name) : Attribute
    {
        /// <summary>
        /// Имя колоник БД
        /// </summary>
        public string Name { get; } = name;
    }
}
