# API Hiitop - Laravel 11 (PHP 8.2)
FROM php:8.2-fpm

# Dependencias del sistema
RUN apt-get update && apt-get install -y \
    zlib1g-dev \
    libpng-dev \
    libjpeg-dev \
    libfreetype6-dev \
    libzip-dev \
    libxml2-dev \
    libicu-dev \
    libonig-dev \
    unzip \
    && rm -rf /var/lib/apt/lists/*

# Extensiones PHP
RUN docker-php-ext-configure gd --with-freetype --with-jpeg \
    && docker-php-ext-install -j$(nproc) \
        gd \
        pdo_mysql \
        zip \
        mbstring \
        intl \
        opcache \
        soap

# Composer
COPY --from=composer:latest /usr/bin/composer /usr/bin/composer

WORKDIR /var/www

# Copiar código (se sobrescribe con volúmenes en dev)
COPY . /var/www

# Dependencias (sin dev para producción; en dev se monta vendor o se ejecuta dentro del contenedor)
RUN composer install --no-interaction --optimize-autoloader --no-dev 2>/dev/null || true

# Permisos
RUN chown -R www-data:www-data /var/www \
    && chmod -R 755 /var/www/storage \
    && chmod -R 775 /var/www/bootstrap/cache 2>/dev/null || true

EXPOSE 9000
CMD ["php-fpm"]
