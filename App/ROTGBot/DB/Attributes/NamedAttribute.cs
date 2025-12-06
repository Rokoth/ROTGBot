namespace ROTGBot.Db.Attributes
{
    public abstract class NamedAttribute(string name) : Attribute
    {       
        public string Name { get; } = name;
    }
}
