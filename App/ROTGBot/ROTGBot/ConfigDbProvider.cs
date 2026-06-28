using Microsoft.EntityFrameworkCore;
using ROTGBot.Db.Context;
using Context = Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<ROTGBot.Db.Context.DbPgContext>;
using Act = System.Action<Microsoft.EntityFrameworkCore.DbContextOptionsBuilder>;
using Sett = ROTGBot.Db.Model.Settings;

namespace ROTGBot
{
    /// <summary>
    /// Провайдер конфигурации из БД
    /// </summary>
    /// <param name="options"></param>
    public class ConfigDbProvider(Act options) : ConfigurationProvider
    {
        private readonly Act _options = options;

        public override void Load() => 
            ApplySettings(GetSettings(ApplyBuilder(new Context())));

        private void ApplySettings(List<Sett> settings) =>
            GetSettingsKVPs(settings).ForEach(AddConfig);

        private void AddConfig(KeyValuePair<string, string> item) => 
            Data.Add(item.Key, item.Value);

        private static List<KeyValuePair<string, string>> GetSettingsKVPs(List<Sett> settings) =>
            [.. settings.Select(SettingsToKVP)];

        private static KeyValuePair<string, string> SettingsToKVP(Sett item)
            => KeyValuePair.Create(item.ParamName, item.ParamValue);

        private Context ApplyBuilder(Context builder)
        {
            _options(builder);
            return builder;
        }

        private static List<Sett> GetSettings(Context builder)
        {
            using var context = new DbPgContext(builder.Options);
            return [.. context.Settings.AsNoTracking()];
        }
    }
}
