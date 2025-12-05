namespace ROTGBot.Contract.Model
{
    public class ReportItem
    {
        public int Year { get; set; }

        public int Month { get; set; }

        public Dictionary<string, int> Count { get; set; } = [];
    }

     

}
