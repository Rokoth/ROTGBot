namespace ROTGBot.Db.Attributes
{
    /// <summary>
    /// Атрибут - тип колонки
    /// </summary>
    /// <remarks>
    /// ctor
    /// </remarks>
    /// <param name="name"></param>
    [AttributeUsage(AttributeTargets.Property)]
    public class ColumnTypeAttribute(string name) : Attribute
    {
        /// <summary>
        /// Наименование типа колонки
        /// </summary>
        public string Name { get; } = name;
    }
}
