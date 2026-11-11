using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaekwondoAPI.Migrations
{
    /// <inheritdoc />
    public partial class InitialSqlite : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Alumnos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Numero = table.Column<string>(type: "TEXT", nullable: false),
                    FechaContacto = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Estado = table.Column<string>(type: "TEXT", nullable: false),
                    MensajeEnviado = table.Column<bool>(type: "INTEGER", nullable: false),
                    FechaUltimoMensaje = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Alumnos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AlumnosClub",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    NombreApellido = table.Column<string>(type: "TEXT", nullable: false),
                    Graduacion = table.Column<string>(type: "TEXT", nullable: false),
                    Horario = table.Column<string>(type: "TEXT", nullable: false),
                    FichaMedica = table.Column<string>(type: "TEXT", nullable: false),
                    Socio = table.Column<string>(type: "TEXT", nullable: false),
                    Gmail = table.Column<string>(type: "TEXT", nullable: false),
                    FechaRegistro = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlumnosClub", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ConfiguracionMensajes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DiasRecordatorio = table.Column<int>(type: "INTEGER", nullable: false),
                    ReenviarAutomaticamente = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfiguracionMensajes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Cuotas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    NombreApellido = table.Column<string>(type: "TEXT", nullable: false),
                    Mes = table.Column<string>(type: "TEXT", nullable: false),
                    Monto = table.Column<int>(type: "INTEGER", nullable: false),
                    Fecha = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Clase = table.Column<string>(type: "TEXT", nullable: false),
                    MedioPago = table.Column<string>(type: "TEXT", nullable: false),
                    EmailRecibo = table.Column<string>(type: "TEXT", nullable: false),
                    ReciboEnviado = table.Column<bool>(type: "INTEGER", nullable: false),
                    FechaEnvioRecibo = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ErrorEnvioRecibo = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cuotas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Examenes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    NombreApellido = table.Column<string>(type: "TEXT", nullable: false),
                    ExamenRendir = table.Column<string>(type: "TEXT", nullable: false),
                    Madera = table.Column<string>(type: "TEXT", nullable: false),
                    Cinturon = table.Column<string>(type: "TEXT", nullable: false),
                    Monto = table.Column<int>(type: "INTEGER", nullable: false),
                    Clase = table.Column<string>(type: "TEXT", nullable: false),
                    FechaExamen = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Examenes", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Alumnos");

            migrationBuilder.DropTable(
                name: "AlumnosClub");

            migrationBuilder.DropTable(
                name: "ConfiguracionMensajes");

            migrationBuilder.DropTable(
                name: "Cuotas");

            migrationBuilder.DropTable(
                name: "Examenes");
        }
    }
}
