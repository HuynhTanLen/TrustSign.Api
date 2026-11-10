using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustSign.Domain.Migrations
{
    /// <inheritdoc />
    public partial class AddSignatureImageAndAuditChainIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "signature_storage_key",
                schema: "contract",
                table: "contract_signers",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_audit_events_resource_type_resource_id_prev_hash",
                schema: "audit",
                table: "audit_events",
                columns: new[] { "resource_type", "resource_id", "prev_hash" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_audit_events_resource_type_resource_id_prev_hash",
                schema: "audit",
                table: "audit_events");

            migrationBuilder.DropColumn(
                name: "signature_storage_key",
                schema: "contract",
                table: "contract_signers");
        }
    }
}
