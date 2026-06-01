using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AdminHiitop.Api.Migrations;

public partial class AddShopifyLocationsTable : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
            CREATE TABLE IF NOT EXISTS `shopify_locations` (
                `id`                   INT NOT NULL AUTO_INCREMENT,
                `shopify_location_id`  BIGINT NOT NULL,
                `name`                 VARCHAR(120) NOT NULL DEFAULT '',
                `is_active`            TINYINT(1) NOT NULL DEFAULT 1,
                `is_pos`               TINYINT(1) NOT NULL DEFAULT 0,
                `address`              VARCHAR(255) NULL,
                `city`                 VARCHAR(120) NULL,
                `synced_at`            DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                PRIMARY KEY (`id`),
                UNIQUE KEY `uq_shopify_locations_shopify_location_id` (`shopify_location_id`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        ");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TABLE IF EXISTS `shopify_locations`;");
    }
}
