using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MercadoBot.Api.Migrations
{
    public partial class AdicionarQuantidade : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Quantidade",
                table: "ItensCompra",
                type: "int",
                nullable: false,
                defaultValue: 1);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Quantidade",
                table: "ItensCompra");
        }
    }
}