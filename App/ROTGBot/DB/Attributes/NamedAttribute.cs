namespace ROTGBot.Db.Attributes
{
    [AttributeUsage(AttributeTargets.Property)]
    public class NamedAttribute(string name) : Attribute
    {
        /// <summary>
        /// Имя колоник БД
        /// </summary>
        public string Name { get; } = name;
    }
}
