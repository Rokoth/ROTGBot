﻿using ROTGBot.Db.Attributes;
using System;

namespace ROTGBot.Db.Model
{
    [TableName("user")]
    public class User : Entity
    {
        [ColumnName("tgid")]
        public long TGId { get; set; }
        [ColumnName("name")]
        public string? Name { get; set; }
        [ColumnName("description")]
        public string? Description { get; set; }
        [ColumnName("tglogin")]
        public string? TGLogin { get; set; }               
        
    }

    [TableName("role")]
    public class Role : Entity
    {
        [ColumnName("name")]
        public string Name { get; set; }
        [ColumnName("description")]
        public string? Description { get; set; }       
    }

    [TableName("userrole")]
    public class UserRole : Entity
    {
        [ColumnName("userid")]
        public Guid UserId { get; set; }
        [ColumnName("roleid")]
        public Guid RoleId { get; set; }        
    }

    [TableName("news")]
    public class News : Entity
    {
        [ColumnName("title")]
        public string? Title { get; set; }
        [ColumnName("description")]
        public string? Description { get; set; }
        [ColumnName("state")]
        public string? State { get; set; }
        [ColumnName("chatid")]
        public long ChatId { get; set; }
        [ColumnName("userid")]
        public Guid UserId { get; set; }
        [ColumnName("type")]
        public string Type { get; set; } = "news";
    }

    [TableName("groups")]
    public class Groups : Entity
    {
        [ColumnName("title")]
        public string? Title { get; set; }
        [ColumnName("description")]
        public string? Description { get; set; }
        [ColumnName("sendnews")]
        public bool SendNews { get; set; }
        [ColumnName("chatid")]
        public long ChatId { get; set; }
        [ColumnName("threadid")]
        public long? ThreadId { get; set; }        
    }

    [TableName("newsmessage")]
    public class NewsMessage : Entity
    {
        [ColumnName("newsid")]
        public Guid NewsId { get; set; }
        [ColumnName("tgmessageid")]
        public long TGMessageId { get; set; }
        
        [ColumnName("valuetext")]
        public string? TextValue { get; set; }
    }
}