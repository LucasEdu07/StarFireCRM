using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExtintorCrm.App.Migrations
{
    /// <inheritdoc />
    public partial class AddConfiguracaoAlerta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConfiguracoesAlerta",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    Alerta7Dias = table.Column<bool>(type: "INTEGER", nullable: false),
                    Alerta15Dias = table.Column<bool>(type: "INTEGER", nullable: false),
                    Alerta30Dias = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfiguracoesAlerta", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConfiguracoesAlerta");
        }
    }
}
