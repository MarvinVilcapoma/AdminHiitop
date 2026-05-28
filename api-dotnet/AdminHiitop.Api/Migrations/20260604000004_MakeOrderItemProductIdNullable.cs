using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AdminHiitop.Api.Migrations
{
    /// <inheritdoc />
    public partial class MakeOrderItemProductIdNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop the existing FK before altering the column (MySQL requirement)
            migrationBuilder.Sql("ALTER TABLE `order_items` DROP FOREIGN KEY `FK_order_items_products_product_id`;");

            // Make product_id nullable
            migrationBuilder.Sql("ALTER TABLE `order_items` MODIFY COLUMN `product_id` INT NULL;");

            // Re-add FK as optional (SET NULL keeps product_description as fallback for Shopify items)
            migrationBuilder.Sql(@"ALTER TABLE `order_items`
                ADD CONSTRAINT `FK_order_items_products_product_id`
                FOREIGN KEY (`product_id`) REFERENCES `products` (`id`) ON DELETE SET NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE `order_items` DROP FOREIGN KEY `FK_order_items_products_product_id`;");

            // Nulls become 0 to allow reverting NOT NULL (rows with product_id=0 will have broken FK)
            migrationBuilder.Sql("UPDATE `order_items` SET `product_id` = 0 WHERE `product_id` IS NULL;");

            migrationBuilder.Sql("ALTER TABLE `order_items` MODIFY COLUMN `product_id` INT NOT NULL;");

            migrationBuilder.Sql(@"ALTER TABLE `order_items`
                ADD CONSTRAINT `FK_order_items_products_product_id`
                FOREIGN KEY (`product_id`) REFERENCES `products` (`id`) ON DELETE CASCADE;");
        }
    }
}
