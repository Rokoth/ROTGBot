namespace ROTGBot.Contract.Model
{    
    public class AdminUserReport
    {
        public List<ReportItem> Items { get; set; } = new List<ReportItem>();
    }

    public class AdminModeratorReport
    {
        public List<ReportItem> Items { get; set; } = new List<ReportItem>();
    }

    public class ReportItem
    {
        public int Year { get; set; }

        public string Month { get; set; } = "-";

        public Dictionary<string, int> Count { get; set; } = [];
    }

}
