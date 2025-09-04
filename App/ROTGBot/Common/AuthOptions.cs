﻿using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace Common
{
    /// <summary>
    /// Настройки авторизации API
    /// </summary>
    public class AuthOptions
    {        
        /// <summary>
        /// Издатель токена
        /// </summary>
        public string Issuer { get; set; } = string.Empty;

        /// <summary>
        /// Потребитель токена
        /// </summary>
        public string Audience { get; set; } = string.Empty;

        /// <summary>
        /// Ключ для шифрации
        /// </summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>
        /// Время жизни токена - 1 минута
        /// </summary>
        public int LifeTime { get; set; } = 60;

        /// <summary>
        /// получить ключ
        /// </summary>
        /// <returns></returns>
        public SymmetricSecurityKey GetSymmetricSecurityKey() => new(Encoding.ASCII.GetBytes(Key));        
    }
}
