using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExtintorCrm.App.Migrations
{
    /// <inheritdoc />
    public partial class AddPagamentoCpfFkAndClienteCpfUnique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Pagamentos_Clientes_ClienteId",
                table: "Pagamentos");

            migrationBuilder.DropIndex(
                name: "IX_Pagamentos_ClienteId",
                table: "Pagamentos");

            migrationBuilder.AddColumn<string>(
                name: "Categoria",
                table: "Pagamentos",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Centro",
                table: "Pagamentos",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Conta",
                table: "Pagamentos",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContaTransferencia",
                table: "Pagamentos",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Contato",
                table: "Pagamentos",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CpfCnpjCliente",
                table: "Pagamentos",
                type: "TEXT",
                maxLength: 18,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DataEfetiva",
                table: "Pagamentos",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DataPrevista",
                table: "Pagamentos",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Forma",
                table: "Pagamentos",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NumeroDocumento",
                table: "Pagamentos",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Observacoes",
                table: "Pagamentos",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Projeto",
                table: "Pagamentos",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RazaoSocial",
                table: "Pagamentos",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Pagamentos",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Subcategoria",
                table: "Pagamentos",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Tipo",
                table: "Pagamentos",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ValorEfetivo",
                table: "Pagamentos",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ValorPrevisto",
                table: "Pagamentos",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "VencimentoFatura",
                table: "Pagamentos",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CPF",
                table: "Clientes",
                type: "TEXT",
                maxLength: 18,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.Sql(
                """
                UPDATE Clientes
                SET CPF = REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(IFNULL(CPF, ''), '.', ''), '-', ''), '/', ''), ' ', ''), '\t', '');
                """);

            migrationBuilder.Sql(
                """
                UPDATE Clientes
                SET Documento = REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(IFNULL(Documento, ''), '.', ''), '-', ''), '/', ''), ' ', ''), '\t', '');
                """);

            migrationBuilder.Sql(
                """
                UPDATE Clientes
                SET CPF = Documento
                WHERE IFNULL(CPF, '') = '' AND IFNULL(Documento, '') <> '';
                """);

            migrationBuilder.Sql(
                """
                UPDATE Clientes
                SET CPF = '9' || printf('%017d', rowid)
                WHERE IFNULL(CPF, '') = '';
                """);

            migrationBuilder.Sql(
                """
                WITH duplicados AS (
                    SELECT rowid,
                           CPF,
                           ROW_NUMBER() OVER(PARTITION BY CPF ORDER BY rowid) AS rn
                    FROM Clientes
                )
                UPDATE Clientes
                SET CPF = '9' || printf('%017d', rowid)
                WHERE rowid IN (SELECT rowid FROM duplicados WHERE rn > 1);
                """);

            migrationBuilder.Sql(
                """
                UPDATE Pagamentos
                SET CpfCnpjCliente = (
                    SELECT c.CPF
                    FROM Clientes c
                    WHERE c.Id = Pagamentos.ClienteId
                )
                WHERE IFNULL(CpfCnpjCliente, '') = '';
                """);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Clientes_CPF",
                table: "Clientes",
                column: "CPF");

            migrationBuilder.CreateIndex(
                name: "IX_Pagamentos_CpfCnpjCliente",
                table: "Pagamentos",
                column: "CpfCnpjCliente");

            migrationBuilder.CreateIndex(
                name: "IX_Clientes_CPF",
                table: "Clientes",
                column: "CPF",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Pagamentos_Clientes_CpfCnpjCliente",
                table: "Pagamentos",
                column: "CpfCnpjCliente",
                principalTable: "Clientes",
                principalColumn: "CPF",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Pagamentos_Clientes_CpfCnpjCliente",
                table: "Pagamentos");

            migrationBuilder.DropIndex(
                name: "IX_Pagamentos_CpfCnpjCliente",
                table: "Pagamentos");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Clientes_CPF",
                table: "Clientes");

            migrationBuilder.DropIndex(
                name: "IX_Clientes_CPF",
                table: "Clientes");

            migrationBuilder.DropColumn(
                name: "Categoria",
                table: "Pagamentos");

            migrationBuilder.DropColumn(
                name: "Centro",
                table: "Pagamentos");

            migrationBuilder.DropColumn(
                name: "Conta",
                table: "Pagamentos");

            migrationBuilder.DropColumn(
                name: "ContaTransferencia",
                table: "Pagamentos");

            migrationBuilder.DropColumn(
                name: "Contato",
                table: "Pagamentos");

            migrationBuilder.DropColumn(
                name: "CpfCnpjCliente",
                table: "Pagamentos");

            migrationBuilder.DropColumn(
                name: "DataEfetiva",
                table: "Pagamentos");

            migrationBuilder.DropColumn(
                name: "DataPrevista",
                table: "Pagamentos");

            migrationBuilder.DropColumn(
                name: "Forma",
                table: "Pagamentos");

            migrationBuilder.DropColumn(
                name: "NumeroDocumento",
                table: "Pagamentos");

            migrationBuilder.DropColumn(
                name: "Observacoes",
                table: "Pagamentos");

            migrationBuilder.DropColumn(
                name: "Projeto",
                table: "Pagamentos");

            migrationBuilder.DropColumn(
                name: "RazaoSocial",
                table: "Pagamentos");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Pagamentos");

            migrationBuilder.DropColumn(
                name: "Subcategoria",
                table: "Pagamentos");

            migrationBuilder.DropColumn(
                name: "Tipo",
                table: "Pagamentos");

            migrationBuilder.DropColumn(
                name: "ValorEfetivo",
                table: "Pagamentos");

            migrationBuilder.DropColumn(
                name: "ValorPrevisto",
                table: "Pagamentos");

            migrationBuilder.DropColumn(
                name: "VencimentoFatura",
                table: "Pagamentos");

            migrationBuilder.AlterColumn<string>(
                name: "CPF",
                table: "Clientes",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 18);

            migrationBuilder.CreateIndex(
                name: "IX_Pagamentos_ClienteId",
                table: "Pagamentos",
                column: "ClienteId");

            migrationBuilder.AddForeignKey(
                name: "FK_Pagamentos_Clientes_ClienteId",
                table: "Pagamentos",
                column: "ClienteId",
                principalTable: "Clientes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
