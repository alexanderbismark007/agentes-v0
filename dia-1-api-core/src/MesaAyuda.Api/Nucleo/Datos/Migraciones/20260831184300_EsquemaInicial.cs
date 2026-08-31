using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MesaAyuda.Api.Nucleo.Datos.Migraciones
{
    /// <inheritdoc />
    public partial class EsquemaInicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "mesa");

            migrationBuilder.CreateTable(
                name: "solicitudes",
                schema: "mesa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Codigo = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    Titulo = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    Descripcion = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    SolicitanteNombre = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    SolicitanteCorreo = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    UnidadDestino = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Categoria = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Prioridad = table.Column<int>(type: "integer", nullable: false),
                    Estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CategoriaSugerida = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    ConfianzaSugerencia = table.Column<double>(type: "double precision", nullable: true),
                    OrigenSugerencia = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    FechaCierre = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FechaCreacion = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FechaActualizacion = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_solicitudes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "comentarios",
                schema: "mesa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SolicitudId = table.Column<Guid>(type: "uuid", nullable: false),
                    Autor = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Contenido = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    EsInterno = table.Column<bool>(type: "boolean", nullable: false),
                    FechaCreacion = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FechaActualizacion = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_comentarios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_comentarios_solicitudes_SolicitudId",
                        column: x => x.SolicitudId,
                        principalSchema: "mesa",
                        principalTable: "solicitudes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_comentarios_SolicitudId",
                schema: "mesa",
                table: "comentarios",
                column: "SolicitudId");

            migrationBuilder.CreateIndex(
                name: "IX_solicitudes_Categoria_Prioridad",
                schema: "mesa",
                table: "solicitudes",
                columns: new[] { "Categoria", "Prioridad" });

            migrationBuilder.CreateIndex(
                name: "IX_solicitudes_Codigo",
                schema: "mesa",
                table: "solicitudes",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_solicitudes_Estado",
                schema: "mesa",
                table: "solicitudes",
                column: "Estado");

            migrationBuilder.CreateIndex(
                name: "IX_solicitudes_FechaCreacion",
                schema: "mesa",
                table: "solicitudes",
                column: "FechaCreacion");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "comentarios",
                schema: "mesa");

            migrationBuilder.DropTable(
                name: "solicitudes",
                schema: "mesa");
        }
    }
}
