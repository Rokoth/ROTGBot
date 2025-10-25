namespace Common
{
    /// <summary>
    /// Общие настройки сервиосв
    /// </summary>
    public class CommonOptions
    {
        /// <summary>
        /// Строка подключения к базе данных
        /// </summary>
        public Dictionary<string, string> ConnectionStrings { get; set; } = [];        
    }
}
