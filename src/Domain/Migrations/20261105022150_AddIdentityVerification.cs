using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustSign.Domain.Migrations
{
    /// <inheritdoc />
    public partial class AddIdentityVerification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "identity_verifications",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    citizen_id = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    full_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    date_of_birth = table.Column<DateOnly>(type: "date", nullable: false),
                    gender = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    address = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    issued_on = table.Column<DateOnly>(type: "date", nullable: true),
                    face_distance = table.Column<double>(type: "double precision", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    selfie_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    front_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    back_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revoked_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    revoke_reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_identity_verifications", x => x.id);
                    table.ForeignKey(
                        name: "fk_identity_verifications_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_identity_verifications_citizen_id",
                schema: "identity",
                table: "identity_verifications",
                column: "citizen_id",
                unique: true,
                filter: "status = 'Verified'");

            migrationBuilder.CreateIndex(
                name: "ix_identity_verifications_user_id",
                schema: "identity",
                table: "identity_verifications",
                column: "user_id",
                unique: true,
                filter: "status = 'Verified'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "identity_verifications",
                schema: "identity");
        }
    }
}
