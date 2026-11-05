using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustSign.Domain.Migrations
{
    /// <inheritdoc />
    public partial class AddIdentityEnhancementsAndSignerRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "audit");

            migrationBuilder.AddColumn<string>(
                name: "job_title",
                schema: "identity",
                table: "users",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "phone_number",
                schema: "identity",
                table: "users",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "terms_accepted_at",
                schema: "identity",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "terms_version",
                schema: "identity",
                table: "users",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "phone",
                schema: "identity",
                table: "tenants",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "representative_name",
                schema: "identity",
                table: "tenants",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "representative_title",
                schema: "identity",
                table: "tenants",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "organization",
                schema: "contract",
                table: "contract_signers",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "phone",
                schema: "contract",
                table: "contract_signers",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "role",
                schema: "contract",
                table: "contract_signers",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Signer");

            migrationBuilder.AddColumn<string>(
                name: "title",
                schema: "contract",
                table: "contract_signers",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "audit_events",
                schema: "audit",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    actor_email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    resource_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    resource_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    payload_json = table.Column<string>(type: "text", nullable: true),
                    prev_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_events", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "password_reset_tokens",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    used_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_password_reset_tokens", x => x.id);
                    table.ForeignKey(
                        name: "fk_password_reset_tokens_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_audit_events_created_at",
                schema: "audit",
                table: "audit_events",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_audit_events_hash",
                schema: "audit",
                table: "audit_events",
                column: "hash");

            migrationBuilder.CreateIndex(
                name: "ix_audit_events_tenant_id",
                schema: "audit",
                table: "audit_events",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_password_reset_tokens_token_hash",
                schema: "identity",
                table: "password_reset_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_password_reset_tokens_user_id",
                schema: "identity",
                table: "password_reset_tokens",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_events",
                schema: "audit");

            migrationBuilder.DropTable(
                name: "password_reset_tokens",
                schema: "identity");

            migrationBuilder.DropColumn(
                name: "job_title",
                schema: "identity",
                table: "users");

            migrationBuilder.DropColumn(
                name: "phone_number",
                schema: "identity",
                table: "users");

            migrationBuilder.DropColumn(
                name: "terms_accepted_at",
                schema: "identity",
                table: "users");

            migrationBuilder.DropColumn(
                name: "terms_version",
                schema: "identity",
                table: "users");

            migrationBuilder.DropColumn(
                name: "phone",
                schema: "identity",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "representative_name",
                schema: "identity",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "representative_title",
                schema: "identity",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "organization",
                schema: "contract",
                table: "contract_signers");

            migrationBuilder.DropColumn(
                name: "phone",
                schema: "contract",
                table: "contract_signers");

            migrationBuilder.DropColumn(
                name: "role",
                schema: "contract",
                table: "contract_signers");

            migrationBuilder.DropColumn(
                name: "title",
                schema: "contract",
                table: "contract_signers");
        }
    }
}
