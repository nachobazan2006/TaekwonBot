using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TaekwondoAPI.Data;

namespace TaekwondoAPI.Migrations;

[DbContext(typeof(TaekwondoDbContext))]
[Migration("20261110010000_AddTaekwonBotLocal")]
public sealed partial class AddTaekwonBotLocal : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>("AlumnoClubId", "Cuotas", nullable: true);
        migrationBuilder.AddColumn<int>("PeriodoAnio", "Cuotas", nullable: false, defaultValue: 0);
        migrationBuilder.AddColumn<int>("PeriodoMes", "Cuotas", nullable: false, defaultValue: 0);
        migrationBuilder.AddColumn<string>("Estado", "Cuotas", nullable: false, defaultValue: "active");
        migrationBuilder.AddColumn<int>("Version", "Cuotas", nullable: false, defaultValue: 1);
        migrationBuilder.Sql("CREATE TABLE Cuotas_Bot (Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, NombreApellido TEXT NOT NULL, Mes TEXT NOT NULL, Monto INTEGER NOT NULL, Fecha TEXT NOT NULL, Clase TEXT NOT NULL, MedioPago TEXT NOT NULL, EmailRecibo TEXT NOT NULL, ReciboEnviado INTEGER NOT NULL, FechaEnvioRecibo TEXT NULL, ErrorEnvioRecibo TEXT NOT NULL, AlumnoClubId INTEGER NULL, PeriodoAnio INTEGER NOT NULL, PeriodoMes INTEGER NOT NULL, Estado TEXT NOT NULL, Version INTEGER NOT NULL, FOREIGN KEY (AlumnoClubId) REFERENCES AlumnosClub(Id) ON DELETE RESTRICT);");
        migrationBuilder.Sql("INSERT INTO Cuotas_Bot (Id,NombreApellido,Mes,Monto,Fecha,Clase,MedioPago,EmailRecibo,ReciboEnviado,FechaEnvioRecibo,ErrorEnvioRecibo,AlumnoClubId,PeriodoAnio,PeriodoMes,Estado,Version) SELECT Id,NombreApellido,Mes,Monto,Fecha,Clase,MedioPago,EmailRecibo,ReciboEnviado,FechaEnvioRecibo,ErrorEnvioRecibo,AlumnoClubId,PeriodoAnio,PeriodoMes,Estado,Version FROM Cuotas;");
        migrationBuilder.Sql("DROP TABLE Cuotas;");
        migrationBuilder.Sql("ALTER TABLE Cuotas_Bot RENAME TO Cuotas;");
        migrationBuilder.CreateIndex("IX_Cuotas_AlumnoClubId_PeriodoAnio_PeriodoMes", "Cuotas", new[] { "AlumnoClubId", "PeriodoAnio", "PeriodoMes" }, unique: true, filter: "\"Estado\" = 'active' AND \"AlumnoClubId\" IS NOT NULL");
        migrationBuilder.Sql("CREATE TABLE BotHistoryMessages (Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, Owner TEXT NOT NULL, Role TEXT NOT NULL, Text TEXT NOT NULL, CreatedAt TEXT NOT NULL);");
        migrationBuilder.Sql("CREATE INDEX IX_BotHistoryMessages_Owner_CreatedAt ON BotHistoryMessages (Owner, CreatedAt);");
        migrationBuilder.Sql("CREATE TABLE BotOperations (Id TEXT NOT NULL PRIMARY KEY, Owner TEXT NOT NULL, Session TEXT NOT NULL, Kind TEXT NOT NULL, Status TEXT NOT NULL, PaymentId INTEGER NULL, ExpectedVersion INTEGER NOT NULL, BeforeJson TEXT NOT NULL, AfterJson TEXT NOT NULL, Reason TEXT NOT NULL, CreatedAt TEXT NOT NULL, ExpiresAt TEXT NOT NULL);");
        migrationBuilder.Sql("CREATE INDEX IX_BotOperations_Owner ON BotOperations (Owner);");
        migrationBuilder.Sql("CREATE TABLE PaymentAuditEvents (Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, OperationId TEXT NOT NULL, PaymentId INTEGER NOT NULL, Owner TEXT NOT NULL, Kind TEXT NOT NULL, Reason TEXT NOT NULL, BeforeJson TEXT NOT NULL, AfterJson TEXT NOT NULL, CreatedAt TEXT NOT NULL);");
        migrationBuilder.Sql("CREATE UNIQUE INDEX IX_PaymentAuditEvents_OperationId ON PaymentAuditEvents (OperationId);");
    }
    protected override void Down(MigrationBuilder migrationBuilder) => throw new NotSupportedException("No se permite eliminar historial contable mediante rollback automático.");
}
