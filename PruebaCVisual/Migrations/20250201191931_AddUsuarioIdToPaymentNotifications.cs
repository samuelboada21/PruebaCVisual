using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PruebaCVisual.Migrations
{
    /// <inheritdoc />
    public partial class AddUsuarioIdToPaymentNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "UsuarioId",
                table: "PaymentNotifications",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentNotifications_UsuarioId",
                table: "PaymentNotifications",
                column: "UsuarioId");

            migrationBuilder.AddForeignKey(
                name: "FK_PaymentNotifications_Usuarios_UsuarioId",
                table: "PaymentNotifications",
                column: "UsuarioId",
                principalTable: "Usuarios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PaymentNotifications_Usuarios_UsuarioId",
                table: "PaymentNotifications");

            migrationBuilder.DropIndex(
                name: "IX_PaymentNotifications_UsuarioId",
                table: "PaymentNotifications");

            migrationBuilder.DropColumn(
                name: "UsuarioId",
                table: "PaymentNotifications");
        }
    }
}
