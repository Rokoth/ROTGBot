namespace ROTGBot.Db.Attributes
{
    public abstract class NamedAttribute(string name) : Attribute
    {
        /// <summary>
        /// Имя
        /// </summary>
        public string Name { get; } = name;
    }
}
