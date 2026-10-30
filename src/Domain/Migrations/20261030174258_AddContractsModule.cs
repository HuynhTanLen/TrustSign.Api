using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustSign.Domain.Migrations
{
    /// <inheritdoc />
    public partial class AddContractsModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "contract");

            migrationBuilder.CreateTable(
                name: "contracts",
                schema: "contract",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    template_id = table.Column<Guid>(type: "uuid", nullable: true),
                    storage_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    original_file_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    current_file_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    file_size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    creator_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    due_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cancelled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_contracts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "templates",
                schema: "contract",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    variables_json = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    author_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_templates", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "contract_signers",
                schema: "contract",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    contract_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sign_order = table.Column<int>(type: "integer", nullable: false),
                    full_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    page_number = table.Column<int>(type: "integer", nullable: false),
                    box_x = table.Column<float>(type: "real", nullable: false),
                    box_y = table.Column<float>(type: "real", nullable: false),
                    box_width = table.Column<float>(type: "real", nullable: false),
                    box_height = table.Column<float>(type: "real", nullable: false),
                    signed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    decline_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_contract_signers", x => x.id);
                    table.ForeignKey(
                        name: "fk_contract_signers_contracts_contract_id",
                        column: x => x.contract_id,
                        principalSchema: "contract",
                        principalTable: "contracts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_contract_signers_contract_id_sign_order",
                schema: "contract",
                table: "contract_signers",
                columns: new[] { "contract_id", "sign_order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_contracts_code",
                schema: "contract",
                table: "contracts",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_contracts_tenant_id_created_at",
                schema: "contract",
                table: "contracts",
                columns: new[] { "tenant_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_contracts_tenant_id_status",
                schema: "contract",
                table: "contracts",
                columns: new[] { "tenant_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_templates_tenant_id_updated_at",
                schema: "contract",
                table: "templates",
                columns: new[] { "tenant_id", "updated_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "contract_signers",
                schema: "contract");

            migrationBuilder.DropTable(
                name: "templates",
                schema: "contract");

            migrationBuilder.DropTable(
                name: "contracts",
                schema: "contract");
        }
    }
}
