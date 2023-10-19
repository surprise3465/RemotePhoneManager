using Microsoft.EntityFrameworkCore;
using RemotePhone.Models;

namespace RemotePhone.Database
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {

        }
        public DbSet<VirtualPhone> VirtualPhones { get; set; }
        public DbSet<RealPhone> RealPhones { get; set; }
        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
        }
    }
}
