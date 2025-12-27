namespace ROTGBot.Contract.Model
{
    public class ReportItem
    {
        public string User { get; set; } = "-";
        public int Year { get; set; }

        public string Month { get; set; } = "-";

        public Dictionary<string, int> Count { get; set; } = [];
    }     

}
