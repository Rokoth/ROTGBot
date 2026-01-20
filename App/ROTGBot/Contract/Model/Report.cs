namespace ROTGBot.Contract.Model
{    
    public class AdminUserReport
    {
        public List<ByUserReportItem> Items { get; set; } = new List<ByUserReportItem>();
    }

    public class AdminModeratorReport
    {
        public List<ByUserReportItem> Items { get; set; } = new List<ByUserReportItem>();
    }

    public class ByUserReportItem 
    {
        public string User { get; set; } = default!;

        public List<ByYearReportItem> ChildItems { get; set; }
    }

    public class ByYearReportItem
    {
        public string Year { get; set; } = default!;

        public List<IReportItem> ChildItems { get; set; }
    }

    public class ByMonthReportItem
    {
        public string Year { get; set; } = default!;

        public List<IReportItem> ChildItems { get; set; }
    }

    public interface IReportItem
    {
        List<IReportItem> ChildItems { get; set; }

        //public int Year { get; set; }

        //public string Month { get; set; } = "-";

        //public Dictionary<string, int> Count { get; set; } = [];
    }

}
