using Microsoft.EntityFrameworkCore;
using PruebaCVisual.Models;

namespace PruebaCVisual.Data
{
    public class DatabaseContext : DbContext
    {
        public DatabaseContext(DbContextOptions<DatabaseContext> options) : base(options) { }

        public DbSet<PaymentNotification> PaymentNotifications { get; set; }
    }
}
