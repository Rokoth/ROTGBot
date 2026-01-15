using System;

namespace ROTGBot.DB.Model
{
    public interface IEntity
    {
        Guid Id { get; set; }
        bool IsDeleted { get; set; }        
    }
}