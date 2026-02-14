namespace ROTGBot.Contract.Model
{    
    public class AdminUserReport
    {
        public List<ByUserReportItem> Items { get; set; } = new List<ByUserReportItem>();
        public List<ByTypeReportItem> Total { get; set; }
    }

    public class UserReport
    {
        public List<ByYearReportItem> Items { get; set; } = new List<ByYearReportItem>();
        public List<ByTypeReportItem> Total { get; set; }
    }

    public class AdminModeratorReport
    {
        public List<ByUserReportItem> Items { get; set; } = new List<ByUserReportItem>();
        public List<ByTypeReportItem> Total { get; set; }
    }

    public class ModeratorReport
    {
        public List<ByYearReportItem> Items { get; set; } = new List<ByYearReportItem>();
        public List<ByTypeReportItem> Total { get; set; }
    }

    public class ByUserReportItem 
    {
        public string User { get; set; } = default!;

        public List<ByYearReportItem> ChildItems { get; set; }
        public List<ByTypeReportItem> Total { get; set; }
    }

    public class ByYearReportItem
    {
        public string Year { get; set; } = default!;

        public List<ByMonthReportItem> ChildItems { get; set; }
        public List<ByTypeReportItem> Total { get; set; }
    }

    public class ByMonthReportItem
    {
        public string Year { get; set; } = default!;

        public List<ByTypeReportItem> ChildItems { get; set; }
    }

    public class ByTypeReportItem
    {
        public ReportType Type { get; set; }
        public int Count { get; set; }
    }

    public enum ReportType
    {
        Sended,
        Accepted,
        Approved,
        Declined
    }
}
