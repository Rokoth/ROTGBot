using ROTGBot.DB.Attributes;

namespace ROTGBot.DB.Model
{
    [TableName("newsmessage")]
    public class NewsMessage : Entity
    {
        [ColumnName("newsid")]
        public Guid NewsId { get; set; }
        [ColumnName("tgmessageid")]
        public long TGMessageId { get; set; }
        
        [ColumnName("valuetext")]
        public string? TextValue { get; set; }
    }
}