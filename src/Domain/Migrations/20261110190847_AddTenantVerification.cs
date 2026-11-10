using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustSign.Domain.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantVerification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "business_license_key",
                schema: "identity",
                table: "tenants",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "verification_reject_reason",
                schema: "identity",
                table: "tenants",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "verification_status",
                schema: "identity",
                table: "tenants",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Verified");

            // Doanh nghiệp đã đăng ký trước khi có tính năng này được coi là đã xác thực; doanh nghiệp mới do ứng dụng đặt trạng thái,
            // nên bỏ giá trị mặc định đi để cơ sở dữ liệu khớp với mô hình EF.
            migrationBuilder.Sql("ALTER TABLE identity.tenants ALTER COLUMN verification_status DROP DEFAULT;");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "verification_submitted_at",
                schema: "identity",
                table: "tenants",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "verified_at",
                schema: "identity",
                table: "tenants",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_tenants_tax_code",
                schema: "identity",
                table: "tenants",
                column: "tax_code",
                unique: true,
                filter: "tax_code IS NOT NULL AND verification_status IN ('Pending', 'Verified')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_tenants_tax_code",
                schema: "identity",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "business_license_key",
                schema: "identity",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "verification_reject_reason",
                schema: "identity",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "verification_status",
                schema: "identity",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "verification_submitted_at",
                schema: "identity",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "verified_at",
                schema: "identity",
                table: "tenants");
        }
    }
}
