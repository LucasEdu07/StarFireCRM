using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExtintorCrm.App.Migrations
{
    /// <inheritdoc />
    public partial class AddClienteCamposExcel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Bairro",
                table: "Clientes",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CEP",
                table: "Clientes",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CPF",
                table: "Clientes",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Categoria",
                table: "Clientes",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Cidade",
                table: "Clientes",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Complemento",
                table: "Clientes",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "Clientes",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "Nascimento",
                table: "Clientes",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Numero",
                table: "Clientes",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Observacoes",
                table: "Clientes",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RG",
                table: "Clientes",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Representante",
                table: "Clientes",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Sexo",
                table: "Clientes",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Telefone1",
                table: "Clientes",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Telefone2",
                table: "Clientes",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Telefone3",
                table: "Clientes",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TipoContato",
                table: "Clientes",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UF",
                table: "Clientes",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "VencimentoExtintores",
                table: "Clientes",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Bairro",
                table: "Clientes");

            migrationBuilder.DropColumn(
                name: "CEP",
                table: "Clientes");

            migrationBuilder.DropColumn(
                name: "CPF",
                table: "Clientes");

            migrationBuilder.DropColumn(
                name: "Categoria",
                table: "Clientes");

            migrationBuilder.DropColumn(
                name: "Cidade",
                table: "Clientes");

            migrationBuilder.DropColumn(
                name: "Complemento",
                table: "Clientes");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "Clientes");

            migrationBuilder.DropColumn(
                name: "Nascimento",
                table: "Clientes");

            migrationBuilder.DropColumn(
                name: "Numero",
                table: "Clientes");

            migrationBuilder.DropColumn(
                name: "Observacoes",
                table: "Clientes");

            migrationBuilder.DropColumn(
                name: "RG",
                table: "Clientes");

            migrationBuilder.DropColumn(
                name: "Representante",
                table: "Clientes");

            migrationBuilder.DropColumn(
                name: "Sexo",
                table: "Clientes");

            migrationBuilder.DropColumn(
                name: "Telefone1",
                table: "Clientes");

            migrationBuilder.DropColumn(
                name: "Telefone2",
                table: "Clientes");

            migrationBuilder.DropColumn(
                name: "Telefone3",
                table: "Clientes");

            migrationBuilder.DropColumn(
                name: "TipoContato",
                table: "Clientes");

            migrationBuilder.DropColumn(
                name: "UF",
                table: "Clientes");

            migrationBuilder.DropColumn(
                name: "VencimentoExtintores",
                table: "Clientes");
        }
    }
}
