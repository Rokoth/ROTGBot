
namespace ROTGBot.Contract.Model
{
    public class User : Entity
    {        
        public long TGId { get; set; }        
        public string? Name { get; set; }        
        public string? Description { get; set; }       
        public string? TGLogin { get; set; }        
        public bool IsNotify { get; set; }       
        public long ChatId { get; set; }

        public bool IsBlocked { get; set; }

        public List<RoleEnum> Roles { get; set; } = [];

        public string RolesString => string.Join(", ", Roles.Select(s => s switch
        {
            RoleEnum.administrator => "Администратор",
            RoleEnum.moderator => "Модератор",
            RoleEnum.user => "Пользователь",
            _ => "Неизвестно",
        }));

        public bool IsAdmin => Roles.Contains(RoleEnum.administrator);
        public bool IsModerator => Roles.Contains(RoleEnum.moderator);

        public DateTime LastSendDate { get; set; }
    }

    public class Role : Entity
    {        
        public string Name { get; set; } = "";       
        public string? Description { get; set; }
    }

}
