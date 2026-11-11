using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaekwondoAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddGraduacionExamen : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Graduacion",
                table: "Examenes",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Graduacion",
                table: "Examenes");
        }
    }
}
