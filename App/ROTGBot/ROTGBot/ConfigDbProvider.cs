using Microsoft.EntityFrameworkCore;
using ROTGBot.Db.Context;

namespace ROTGBot
{
    public class ConfigDbProvider(Action<DbContextOptionsBuilder> options) : ConfigurationProvider
    {
        private readonly Action<DbContextOptionsBuilder> _options = options;

        public override void Load()
        {
            GetSettings(GetBuilder())
                .Select(item => KeyValuePair.Create(item.ParamName, item.ParamValue))
                .ToList()
                .ForEach(item => Data.Add(item.Key, item.Value));
        }

        private DbContextOptionsBuilder<DbPgContext> GetBuilder()
        {
            var builder = new DbContextOptionsBuilder<DbPgContext>();
            _options(builder);
            return builder;
        }

        private static List<Db.Model.Settings> GetSettings(DbContextOptionsBuilder<DbPgContext> builder)
        {
            using var context = new DbPgContext(builder.Options);
            return [.. context.Settings.AsNoTracking()];
        }
    }
}
