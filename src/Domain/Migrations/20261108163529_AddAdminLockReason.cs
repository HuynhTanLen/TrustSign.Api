using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustSign.Domain.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminLockReason : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "lock_reason",
                schema: "identity",
                table: "users",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "locked_at",
                schema: "identity",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "lock_reason",
                schema: "identity",
                table: "tenants",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "locked_at",
                schema: "identity",
                table: "tenants",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "lock_reason",
                schema: "identity",
                table: "users");

            migrationBuilder.DropColumn(
                name: "locked_at",
                schema: "identity",
                table: "users");

            migrationBuilder.DropColumn(
                name: "lock_reason",
                schema: "identity",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "locked_at",
                schema: "identity",
                table: "tenants");
        }
    }
}
