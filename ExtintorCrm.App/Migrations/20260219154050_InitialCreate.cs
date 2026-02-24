using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExtintorCrm.App.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Clientes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    NomeFantasia = table.Column<string>(type: "TEXT", nullable: false),
                    RazaoSocial = table.Column<string>(type: "TEXT", nullable: true),
                    Documento = table.Column<string>(type: "TEXT", nullable: true),
                    Contato = table.Column<string>(type: "TEXT", nullable: true),
                    Telefone = table.Column<string>(type: "TEXT", nullable: true),
                    Endereco = table.Column<string>(type: "TEXT", nullable: true),
                    TipoServico = table.Column<string>(type: "TEXT", nullable: true),
                    StatusRecarga = table.Column<string>(type: "TEXT", nullable: true),
                    VencimentoServico = table.Column<DateTime>(type: "TEXT", nullable: true),
                    NumeroAlvara = table.Column<string>(type: "TEXT", nullable: true),
                    VencimentoAlvara = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", nullable: true),
                    AvisoAtivo = table.Column<bool>(type: "INTEGER", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clientes", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Clientes");
        }
    }
}
