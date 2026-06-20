-- Multi-Tenant E-Commerce, Inventory, and POS SaaS Platform PostgreSQL Database Schema

-- Enable UUID extension
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- Drop existing tables to start fresh
DROP TABLE IF EXISTS audit_logs CASCADE;
DROP TABLE IF EXISTS company_settings CASCADE;
DROP TABLE IF EXISTS payments CASCADE;
DROP TABLE IF EXISTS order_items CASCADE;
DROP TABLE IF EXISTS orders CASCADE;
DROP TABLE IF EXISTS products CASCADE;
DROP TABLE IF EXISTS brands CASCADE;
DROP TABLE IF EXISTS categories CASCADE;
DROP TABLE IF EXISTS user_roles CASCADE;
DROP TABLE IF EXISTS role_permissions CASCADE;
DROP TABLE IF EXISTS permissions CASCADE;
DROP TABLE IF EXISTS roles CASCADE;
DROP TABLE IF EXISTS users CASCADE;
DROP TABLE IF EXISTS companies CASCADE;
DROP TABLE IF EXISTS subscription_plans CASCADE;

-- 1. Subscription Plans Table (Platform-wide)
CREATE TABLE subscription_plans (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    name VARCHAR(100) UNIQUE NOT NULL,
    price DECIMAL(10, 2) NOT NULL,
    billing_cycle VARCHAR(50) DEFAULT 'monthly' NOT NULL,
    features JSONB,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL,
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL
);

-- 2. Companies Table (Tenants)
CREATE TABLE companies (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    name VARCHAR(255) NOT NULL,
    subdomain VARCHAR(100) UNIQUE NOT NULL,
    logo_url VARCHAR(500),
    banner_url VARCHAR(500),
    contact_email VARCHAR(255),
    contact_phone VARCHAR(50),
    address TEXT,
    delivery_charge DECIMAL(10, 2) DEFAULT 0.00 NOT NULL,
    is_active BOOLEAN DEFAULT TRUE NOT NULL,
    subscription_plan_id UUID REFERENCES subscription_plans(id) ON DELETE SET NULL,
    subscription_expires_at TIMESTAMP WITH TIME ZONE,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL,
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL
);

-- 3. Roles Table (System roles)
CREATE TABLE roles (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    name VARCHAR(50) UNIQUE NOT NULL, -- 'SUPER_ADMIN', 'COMPANY_ADMIN', 'COMPANY_MANAGER', 'SALES_STAFF'
    description VARCHAR(255),
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL
);

-- 4. Permissions Table
CREATE TABLE permissions (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    name VARCHAR(100) UNIQUE NOT NULL,
    description VARCHAR(255),
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL
);

-- 5. Role-Permissions Join Table
CREATE TABLE role_permissions (
    role_id UUID REFERENCES roles(id) ON DELETE CASCADE,
    permission_id UUID REFERENCES permissions(id) ON DELETE CASCADE,
    PRIMARY KEY (role_id, permission_id)
);

-- 6. Users Table (Multi-Tenant)
CREATE TABLE users (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    company_id UUID REFERENCES companies(id) ON DELETE CASCADE, -- Nullable for Super Admin
    email VARCHAR(255) UNIQUE NOT NULL,
    password_hash VARCHAR(255) NOT NULL,
    first_name VARCHAR(100) NOT NULL,
    last_name VARCHAR(100) NOT NULL,
    phone_number VARCHAR(50),
    is_active BOOLEAN DEFAULT TRUE NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL,
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL
);

-- 7. User-Roles Join Table
CREATE TABLE user_roles (
    user_id UUID REFERENCES users(id) ON DELETE CASCADE,
    role_id UUID REFERENCES roles(id) ON DELETE CASCADE,
    PRIMARY KEY (user_id, role_id)
);

-- 8. Categories Table (Tenant-isolated)
CREATE TABLE categories (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    company_id UUID REFERENCES companies(id) ON DELETE CASCADE NOT NULL,
    name VARCHAR(255) NOT NULL,
    slug VARCHAR(255) NOT NULL,
    description TEXT,
    parent_id UUID REFERENCES categories(id) ON DELETE SET NULL,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL,
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL,
    UNIQUE (company_id, slug)
);

-- 9. Brands Table (Tenant-isolated)
CREATE TABLE brands (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    company_id UUID REFERENCES companies(id) ON DELETE CASCADE NOT NULL,
    name VARCHAR(255) NOT NULL,
    slug VARCHAR(255) NOT NULL,
    description TEXT,
    logo_url VARCHAR(500),
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL,
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL,
    UNIQUE (company_id, slug)
);

-- 10. Products Table (Tenant-isolated with wholesale/retail prices & stock level)
CREATE TABLE products (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    company_id UUID REFERENCES companies(id) ON DELETE CASCADE NOT NULL,
    name VARCHAR(255) NOT NULL,
    slug VARCHAR(255) NOT NULL,
    sku VARCHAR(100) NOT NULL,
    barcode VARCHAR(100) NOT NULL, -- Code 128 / EAN format
    description TEXT,
    price DECIMAL(10, 2) NOT NULL, -- Retail price
    wholesale_price DECIMAL(10, 2) NOT NULL,
    stock_quantity INT DEFAULT 0 NOT NULL,
    status VARCHAR(50) DEFAULT 'PUBLISHED' NOT NULL, -- 'PUBLISHED', 'DRAFT', 'OUT_OF_STOCK'
    image_url VARCHAR(500),
    category_id UUID REFERENCES categories(id) ON DELETE SET NULL,
    brand_id UUID REFERENCES brands(id) ON DELETE SET NULL,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL,
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL,
    UNIQUE (company_id, sku),
    UNIQUE (company_id, barcode)
);

-- 11. Orders Table (Omnichannel POS & E-commerce orders)
CREATE TABLE orders (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    company_id UUID REFERENCES companies(id) ON DELETE CASCADE NOT NULL,
    order_number VARCHAR(100) NOT NULL,
    sale_type VARCHAR(50) DEFAULT 'ECOMMERCE' NOT NULL, -- 'ECOMMERCE', 'POS'
    sales_staff_id UUID REFERENCES users(id) ON DELETE SET NULL, -- POS cashier/staff
    customer_name VARCHAR(255),
    customer_phone VARCHAR(50),
    status VARCHAR(50) DEFAULT 'PENDING' NOT NULL, -- 'PENDING', 'PROCESSING', 'COMPLETED', 'CANCELLED'
    subtotal DECIMAL(10, 2) NOT NULL,
    discount DECIMAL(10, 2) DEFAULT 0.00 NOT NULL,
    tax DECIMAL(10, 2) DEFAULT 0.00 NOT NULL,
    shipping_fee DECIMAL(10, 2) DEFAULT 0.00 NOT NULL,
    total DECIMAL(10, 2) NOT NULL,
    payment_method VARCHAR(50) NOT NULL, -- 'CASH', 'BKASH', 'NAGAD', 'ROCKET', 'CARD'
    payment_status VARCHAR(50) DEFAULT 'PENDING' NOT NULL, -- 'PENDING', 'PAID', 'FAILED'
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL,
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL,
    UNIQUE (company_id, order_number)
);

-- 12. Order Items Table
CREATE TABLE order_items (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    order_id UUID REFERENCES orders(id) ON DELETE CASCADE NOT NULL,
    product_id UUID REFERENCES products(id) ON DELETE RESTRICT NOT NULL,
    quantity INT NOT NULL,
    price DECIMAL(10, 2) NOT NULL,
    total_price DECIMAL(10, 2) NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL
);

-- 13. Payments Table (Local bKash/Nagad/Rocket logs)
CREATE TABLE payments (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    company_id UUID REFERENCES companies(id) ON DELETE CASCADE NOT NULL,
    order_id UUID REFERENCES orders(id) ON DELETE CASCADE NOT NULL,
    transaction_id VARCHAR(255), -- TrxID for MFS matching
    provider VARCHAR(50) NOT NULL, -- 'BKASH', 'NAGAD', 'ROCKET', 'CASH'
    amount DECIMAL(10, 2) NOT NULL,
    status VARCHAR(50) DEFAULT 'PENDING' NOT NULL, -- 'PENDING', 'SUCCESS', 'FAILED'
    payment_type VARCHAR(50) DEFAULT 'AUTOMATED' NOT NULL, -- 'AUTOMATED', 'MANUAL'
    sender_number VARCHAR(50),
    reference_log TEXT,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL,
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL
);

-- 14. Company Settings Table (Tenant-specific general/POS settings)
CREATE TABLE company_settings (
    company_id UUID REFERENCES companies(id) ON DELETE CASCADE NOT NULL,
    key VARCHAR(100) NOT NULL,
    value TEXT NOT NULL,
    group_name VARCHAR(50) DEFAULT 'GENERAL' NOT NULL, -- 'GENERAL', 'ECOMMERCE', 'POS', 'PAYMENT'
    PRIMARY KEY (company_id, key)
);

-- 15. Audit Logs Table (Isolated actions)
CREATE TABLE audit_logs (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    company_id UUID REFERENCES companies(id) ON DELETE CASCADE,
    user_id UUID REFERENCES users(id) ON DELETE SET NULL,
    action VARCHAR(255) NOT NULL,
    details TEXT,
    ip_address VARCHAR(45),
    user_agent TEXT,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL
);

-- Seed global subscriptions
INSERT INTO subscription_plans (name, price, billing_cycle, features) VALUES
('Basic Plan', 1500.00, 'monthly', '{"max_products": 200, "pos_enabled": true, "ecommerce_enabled": true}'),
('Premium Plan', 3500.00, 'monthly', '{"max_products": 5000, "pos_enabled": true, "ecommerce_enabled": true, "multi_staff": true}')
ON CONFLICT (name) DO NOTHING;

-- Seed system roles
INSERT INTO roles (name, description) VALUES
('SUPER_ADMIN', 'Platform Owner - Full access to all system tenants, subscriptions, and diagnostics'),
('COMPANY_ADMIN', 'Company Owner - Full access to specific company settings, reports, staff management'),
('COMPANY_MANAGER', 'Company Store Manager - Manage inventory, store config, and view basic reports'),
('SALES_STAFF', 'POS Checkout Operator - Restricted access to barcodes scanner, checkout register, and personal sales logs')
ON CONFLICT (name) DO NOTHING;

-- Seed default permissions
INSERT INTO permissions (name, description) VALUES
('platform:diagnostics', 'Access to SaaS metrics and platform logs'),
('manage:companies', 'Add, suspend, or upgrade tenant companies'),
('manage:subscriptions', 'Configure billing plans and approve payment references'),
('company:settings', 'Update company-wide configuration and delivery charges'),
('manage:staff', 'Create, update, and deactivate managers and checkout staff'),
('manage:inventory', 'Create products, edit prices, trigger barcode generation'),
('pos:checkout', 'Scan barcodes and complete register checkout'),
('reports:full', 'Access full company financial and inventory audits'),
('reports:operational', 'Access inventory stock alerts and daily staff cashier sheets')
ON CONFLICT (name) DO NOTHING;

-- Map Super Admin Role Permissions
INSERT INTO role_permissions (role_id, permission_id)
SELECT r.id, p.id FROM roles r, permissions p
WHERE r.name = 'SUPER_ADMIN' AND p.name IN ('platform:diagnostics', 'manage:companies', 'manage:subscriptions')
ON CONFLICT DO NOTHING;

-- Map Company Admin Role Permissions
INSERT INTO role_permissions (role_id, permission_id)
SELECT r.id, p.id FROM roles r, permissions p
WHERE r.name = 'COMPANY_ADMIN' AND p.name IN ('company:settings', 'manage:staff', 'manage:inventory', 'pos:checkout', 'reports:full')
ON CONFLICT DO NOTHING;

-- Map Company Manager Role Permissions
INSERT INTO role_permissions (role_id, permission_id)
SELECT r.id, p.id FROM roles r, permissions p
WHERE r.name = 'COMPANY_MANAGER' AND p.name IN ('manage:inventory', 'pos:checkout', 'reports:operational')
ON CONFLICT DO NOTHING;

-- Map Sales Staff Role Permissions
INSERT INTO role_permissions (role_id, permission_id)
SELECT r.id, p.id FROM roles r, permissions p
WHERE r.name = 'SALES_STAFF' AND p.name IN ('pos:checkout')
ON CONFLICT DO NOTHING;
