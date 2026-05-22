using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AdminHiitop.Api.Migrations;

public partial class AddShopifyTransfersTable : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
            CREATE TABLE IF NOT EXISTS `shopify_transfers` (
                `id`                INT NOT NULL AUTO_INCREMENT,
                `shopify_product_id` BIGINT NOT NULL DEFAULT 0,
                `shopify_variant_id` BIGINT NOT NULL DEFAULT 0,
                `inventory_item_id`  BIGINT NOT NULL DEFAULT 0,
                `product_title`      VARCHAR(255) NOT NULL DEFAULT '',
                `variant_title`      VARCHAR(255) NOT NULL DEFAULT '',
                `from_location_id`   BIGINT NOT NULL DEFAULT 0,
                `from_location_name` VARCHAR(120) NOT NULL DEFAULT '',
                `to_location_id`     BIGINT NOT NULL DEFAULT 0,
                `to_location_name`   VARCHAR(120) NOT NULL DEFAULT '',
                `quantity`           INT NOT NULL DEFAULT 0,
                `reason`             VARCHAR(500) NULL,
                `created_by`         VARCHAR(120) NULL,
                `created_at`         DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                PRIMARY KEY (`id`),
                KEY `idx_shopify_transfers_variant` (`shopify_variant_id`),
                KEY `idx_shopify_transfers_inventory_item` (`inventory_item_id`),
                KEY `idx_shopify_transfers_created_at` (`created_at`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        ");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TABLE IF EXISTS `shopify_transfers`;");
    }
}
