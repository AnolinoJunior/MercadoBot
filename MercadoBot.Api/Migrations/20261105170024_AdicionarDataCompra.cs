using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MercadoBot.Api.Migrations
{
    public partial class AdicionarDataCompra : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CompradoEm",
                table: "ItensCompra",
                type: "datetime2",
                nullable: true);

            // Data de referência para os registros antigos já comprados.
            migrationBuilder.Sql("""
                UPDATE [ItensCompra]
                SET [CompradoEm] = SYSUTCDATETIME()
                WHERE [Comprado] = 1
                  AND [CompradoEm] IS NULL;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CompradoEm",
                table: "ItensCompra");
        }
    }
}