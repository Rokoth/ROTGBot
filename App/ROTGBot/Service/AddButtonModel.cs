namespace ROTGBot.Service
{
    public class AddButtonModel
    {
        public long ChatId { get; set; }
        public int? ThreadId { get; set; }
        public string ChatName { get; set; }
        public string? ThreadName { get; set; }
    }
}
