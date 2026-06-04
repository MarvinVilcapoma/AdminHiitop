using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AdminHiitop.Api.Migrations;

public partial class AddInvoiceDeliveryFields : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Add customer contact + PDF URL columns to invoices
        // Note: uses separate statements — ADD COLUMN IF NOT EXISTS requires MySQL 8.0.3+
        migrationBuilder.Sql("ALTER TABLE `invoices` ADD COLUMN `customer_phone` VARCHAR(30)   NULL AFTER `customer_name`;");
        migrationBuilder.Sql("ALTER TABLE `invoices` ADD COLUMN `customer_email` VARCHAR(150)  NULL AFTER `customer_phone`;");
        migrationBuilder.Sql("ALTER TABLE `invoices` ADD COLUMN `pdf_url`        VARCHAR(2000) NULL AFTER `cdr_content`;");

        // Delivery log table — one row per WhatsApp link / email attempt
        migrationBuilder.Sql(@"
            CREATE TABLE IF NOT EXISTS `invoice_delivery_logs` (
                `id`            INT NOT NULL AUTO_INCREMENT,
                `invoice_id`    INT NOT NULL,
                `channel_code`  VARCHAR(30)  NOT NULL DEFAULT '',
                `status_code`   VARCHAR(50)  NOT NULL DEFAULT '',
                `recipient`     VARCHAR(200) NULL,
                `external_url`  VARCHAR(2000) NULL,
                `error_message` VARCHAR(1000) NULL,
                `created_at`    DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                `created_by`    VARCHAR(100) NULL,
                PRIMARY KEY (`id`),
                KEY `idx_invoice_delivery_logs_invoice_id` (`invoice_id`),
                KEY `idx_invoice_delivery_logs_channel` (`channel_code`),
                CONSTRAINT `fk_invoice_delivery_logs_invoice`
                    FOREIGN KEY (`invoice_id`) REFERENCES `invoices` (`id`) ON DELETE CASCADE
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        ");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TABLE IF EXISTS `invoice_delivery_logs`;");
        migrationBuilder.Sql("ALTER TABLE `invoices` DROP COLUMN `pdf_url`;");
        migrationBuilder.Sql("ALTER TABLE `invoices` DROP COLUMN `customer_email`;");
        migrationBuilder.Sql("ALTER TABLE `invoices` DROP COLUMN `customer_phone`;");
    }
}
