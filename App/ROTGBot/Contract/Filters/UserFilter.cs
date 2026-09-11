using ROTGBot.Contract.Model;

namespace ROTGBot.Contract.Filters
{
    public class UserFilter(int? size, int? page, string sort) : Filter<User>(size, page, sort)
    {
    }
}
