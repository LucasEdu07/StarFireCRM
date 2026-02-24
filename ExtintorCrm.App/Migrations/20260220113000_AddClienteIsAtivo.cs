using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExtintorCrm.App.Migrations
{
    public partial class AddClienteIsAtivo : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsAtivo",
                table: "Clientes",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.Sql(
                "UPDATE Clientes SET IsAtivo = CASE " +
                "WHEN lower(ifnull(Status, '')) = 'inativo' THEN 0 ELSE 1 END;");

            migrationBuilder.Sql(
                "UPDATE Clientes SET Status = NULL " +
                "WHERE lower(ifnull(Status, '')) IN ('ativo', 'inativo');");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsAtivo",
                table: "Clientes");
        }
    }
}
