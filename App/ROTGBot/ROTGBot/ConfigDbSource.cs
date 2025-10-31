using Microsoft.EntityFrameworkCore;

namespace ROTGBot
{
    /// <summary>
    /// Добавление источника конфигурации приложения - База Данных
    /// </summary>
    /// <param name="optionsAction"></param>
    public class ConfigDbSource(Action<DbContextOptionsBuilder> optionsAction) : IConfigurationSource
    {
        /// <summary>
        /// Настройки источника конфигурации приложения
        /// </summary>
        private readonly Action<DbContextOptionsBuilder> _optionsAction = optionsAction;

        /// <summary>
        /// Создание провайдера конфигурации
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public IConfigurationProvider Build(IConfigurationBuilder builder) => new ConfigDbProvider(_optionsAction);
    }
}