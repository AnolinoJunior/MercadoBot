using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MercadoBot.Api.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarTarefasCasa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TarefasCasa",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ChatId = table.Column<long>(type: "bigint", nullable: false),
                    Descricao = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    DiaSemana = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TarefasCasa", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TarefasCasa_ChatId_DiaSemana",
                table: "TarefasCasa",
                columns: new[] { "ChatId", "DiaSemana" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TarefasCasa");
        }
    }
}
