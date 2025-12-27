namespace ROTGBot.Contract.Model
{
    public class Report
    {
        public string Type { get; set; }

        public List<ReportItem> Items { get; set; } = new List<ReportItem>();
    }

    public class AdminUserReport
    {        

        public List<ReportItem> Items { get; set; } = new List<ReportItem>();
    }

}
