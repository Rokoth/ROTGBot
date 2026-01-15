using ROTGBot.DB.Attributes;

namespace ROTGBot.DB.Model
{
    [TableName("role")]
    public class Role : Entity
    {
        [ColumnName("name")]
        public string Name { get; set; } = "";
        [ColumnName("description")]
        public string? Description { get; set; }       
    }
}