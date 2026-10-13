using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IoTMonitor.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDeviceProtocolArchitecture : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "protocol_key",
                table: "devices",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "mqtt");

            migrationBuilder.AddColumn<string>(
                name: "protocol_settings",
                table: "devices",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'{}'::jsonb");

            migrationBuilder.CreateIndex(
                name: "ix_devices_protocol_key_is_active",
                table: "devices",
                columns: new[] { "protocol_key", "is_active" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_devices_protocol_key_is_active",
                table: "devices");

            migrationBuilder.DropColumn(
                name: "protocol_key",
                table: "devices");

            migrationBuilder.DropColumn(
                name: "protocol_settings",
                table: "devices");
        }
    }
}
