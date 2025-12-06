using Microsoft.EntityFrameworkCore;
using ROTGBot.Db.Context;

namespace ROTGBot
{
    public class ConfigDbProvider(Action<DbContextOptionsBuilder> options) : ConfigurationProvider
    {
        private readonly Action<DbContextOptionsBuilder> _options = options;

        public override void Load() => GetItems().ForEach(item => Data.Add(item.ParamName, item.ParamValue));

        private List<Db.Model.Settings> GetItems() => [.. new DbPgContext(GetBuilder().Options).Settings.AsNoTracking()];

        private DbContextOptionsBuilder<DbPgContext> GetBuilder()
        {
            var builder = new DbContextOptionsBuilder<DbPgContext>();
            _options(builder);
            return builder;
        }
    }
}
