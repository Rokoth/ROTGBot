using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ROTGBot.Db.Context;

namespace ROTGBot
{
    public class ConfigDbProvider : ConfigurationProvider
    {
        private readonly Action<DbContextOptionsBuilder> _options;       

        public ConfigDbProvider(Action<DbContextOptionsBuilder> options)
        {
            _options = options;           
        }

        public override void Load()
        {
            var builder = new DbContextOptionsBuilder<DbPgContext>();
            _options(builder);

            using var context = new DbPgContext(builder.Options);
            var items = context.Settings
                .AsNoTracking()
                .ToList();

            foreach (var item in items)
            {
                Data.Add(item.ParamName, item.ParamValue);
            }
        }                
    }
}
