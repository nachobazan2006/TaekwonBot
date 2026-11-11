using Microsoft.EntityFrameworkCore;
using TaekwondoAPI.Models;

namespace TaekwondoAPI.Data
{
    public class TaekwondoDbContext : DbContext
    {
        public TaekwondoDbContext(DbContextOptions<TaekwondoDbContext> options)
            : base(options)
        {
        }

        public DbSet<Alumno> Alumnos { get; set; }
        public DbSet<AlumnoClub> AlumnosClub { get; set; }
        public DbSet<Cuota> Cuotas { get; set; }
        public DbSet<Examen> Examenes { get; set; }
        public DbSet<ConfiguracionMensajes> ConfiguracionMensajes { get; set; }
        public DbSet<BotHistoryMessage> BotHistoryMessages => Set<BotHistoryMessage>();
        public DbSet<BotOperation> BotOperations => Set<BotOperation>();
        public DbSet<PaymentAuditEvent> PaymentAuditEvents => Set<PaymentAuditEvent>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            BotModel.Configure(modelBuilder);
        }


    }
}


