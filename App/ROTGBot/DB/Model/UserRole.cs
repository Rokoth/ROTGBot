using ROTGBot.DB.Attributes;

namespace ROTGBot.DB.Model
{
    [TableName("userrole")]
    public class UserRole : Entity
    {
        [ColumnName("userid")]
        public Guid UserId { get; set; }
        [ColumnName("roleid")]
        public Guid RoleId { get; set; }        
    }
}