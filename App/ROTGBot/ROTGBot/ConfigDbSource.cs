using Microsoft.EntityFrameworkCore;

namespace ROTGBot
{
    /// <summary>
    /// Добавление источника конфигурации приложения - База Данных
    /// </summary>
    /// <param name="optionsAction"></param>
    public class ConfigDbSource(Action<DbContextOptionsBuilder> optionsAction) : 
        IConfigurationSource
    {
        private readonly Action<DbContextOptionsBuilder> _optionsAction = optionsAction;

        public IConfigurationProvider Build(IConfigurationBuilder builder) 
            => new ConfigDbProvider(_optionsAction);
    }
}
