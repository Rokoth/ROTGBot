using ROTGBot.DB.Model;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace ROTGBot.DB.Context
{
    /// <summary>
    /// Postgresql context
    /// </summary>
    /// <remarks>
    /// ctor
    /// </remarks>
    /// <param name="options"></param>
    public class DbPgContext(DbContextOptions<DbPgContext> options) : DbContext(options)
    {        
        /// <summary>
        /// settings set
        /// </summary>
        public DbSet<Settings> Settings { get; set; }

        /// <summary>
        /// create models
        /// </summary>
        /// <param name="modelBuilder"></param>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.HasPostgresExtension("uuid-ossp");

            modelBuilder.ApplyConfiguration(new EntityConfiguration<Settings>());

            var types = Assembly.GetAssembly(typeof(Entity))?.GetTypes();

            foreach (var type in types ?? [])
            {
                if (typeof(IEntity).IsAssignableFrom(type) && !type.IsAbstract)
                {
                    BuildAndInvoke(modelBuilder, type);
                }
            }
        }

        private void BuildAndInvoke(ModelBuilder modelBuilder, Type type) 
            => GetType().GetMethod(nameof(ApplyConf), BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)?
                .MakeGenericMethod(type).Invoke(this, [modelBuilder, GetConfig(type)]);

        private static object? GetConfig(Type type)
        {
            var configType = typeof(EntityConfiguration<>).MakeGenericType(type);
            var config = Activator.CreateInstance(configType);
            return config;
        }

        /// <summary>
        /// ApplyConfiguration generic wrapper
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="modelBuilder"></param>
        /// <param name="config"></param>
        private static void ApplyConf<T>(ModelBuilder modelBuilder, EntityConfiguration<T> config) where T : class, IEntity 
            => modelBuilder.ApplyConfiguration(config);

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) { }
    }
}
