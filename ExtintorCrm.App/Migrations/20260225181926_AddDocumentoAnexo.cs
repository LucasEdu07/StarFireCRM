using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExtintorCrm.App.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentoAnexo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DocumentosAnexo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ClienteId = table.Column<Guid>(type: "TEXT", nullable: true),
                    PagamentoId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Contexto = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    TipoDocumento = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    NomeOriginal = table.Column<string>(type: "TEXT", maxLength: 260, nullable: false),
                    CaminhoRelativo = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                    TamanhoBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentosAnexo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentosAnexo_Clientes_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "Clientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DocumentosAnexo_Pagamentos_PagamentoId",
                        column: x => x.PagamentoId,
                        principalTable: "Pagamentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentosAnexo_ClienteId",
                table: "DocumentosAnexo",
                column: "ClienteId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentosAnexo_PagamentoId",
                table: "DocumentosAnexo",
                column: "PagamentoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentosAnexo");
        }
    }
}
