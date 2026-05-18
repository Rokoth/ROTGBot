using ROTGBot.Contract.Model;

namespace ROTGBot.Contract.Filters
{
    public class NewsFilter : Filter<News>
    {
        public NewsFilter(string name, int? userNumber,  int? size, int? page, string sort) : base(size, page, sort)
        {
        }

        
    }
}
