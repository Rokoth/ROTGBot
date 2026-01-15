using ROTGBot.DB.Attributes;
using System;
using System.Collections.Generic;

namespace ROTGBot.DB.Model
{
    public abstract class Entity: IEntity
    {
        [PrimaryKey]
        [ColumnName("id")]
        public Guid Id { get; set; }
      
        [ColumnName("is_deleted")]
        public bool IsDeleted { get; set; }
       
    }
}