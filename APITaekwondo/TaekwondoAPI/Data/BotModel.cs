using Microsoft.EntityFrameworkCore;
using TaekwondoAPI.Models;

namespace TaekwondoAPI.Data;

public static class BotModel
{
    public static void Configure(ModelBuilder model)
    {
        var payment = model.Entity<Cuota>();
        payment.Property(p => p.AlumnoClubId);
        payment.Property(p => p.PeriodoAnio);
        payment.Property(p => p.PeriodoMes);
        payment.Property(p => p.Estado).IsRequired();
        payment.Property(p => p.Version).IsConcurrencyToken();
        payment.HasIndex(p => new { p.AlumnoClubId, p.PeriodoAnio, p.PeriodoMes })
            .IsUnique().HasFilter("\"Estado\" = 'active' AND \"AlumnoClubId\" IS NOT NULL");
        payment.HasOne<AlumnoClub>().WithMany().HasForeignKey(p => p.AlumnoClubId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<BotHistoryMessage>().ToTable("BotHistoryMessages").HasIndex(p => new { p.Owner, p.CreatedAt });
        model.Entity<BotOperation>().ToTable("BotOperations");
        model.Entity<BotOperation>().HasKey(p => p.Id);
        model.Entity<BotOperation>().HasIndex(p => p.Owner);
        model.Entity<BotOperation>().Property(p => p.Status).IsConcurrencyToken();
        model.Entity<PaymentAuditEvent>().ToTable("PaymentAuditEvents").HasIndex(p => p.OperationId).IsUnique();
    }
}
