using ROTGBot.DB.Attributes;

namespace ROTGBot.DB.Model
{

    [TableName("settings")]
    public class Settings
    {
        [ColumnName("id")]
        public int Id { get; set; }

        [ColumnName("param_name")]
        public string ParamName { get; set; } = "";

        [ColumnName("param_value")]
        public string ParamValue { get; set; } = "";
    }
}