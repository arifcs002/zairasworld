using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Ecommerce.Api.Domain
{
    public enum RoleName
    {
        SUPER_ADMIN,
        COMPANY_ADMIN,
        COMPANY_MANAGER,
        SALES_STAFF
    }

    [Table("subscription_plans")]
    public class SubscriptionPlan
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Required]
        [Column("price")]
        public decimal Price { get; set; }

        [Required]
        [Column("billing_cycle")]
        public string BillingCycle { get; set; } = "monthly";

        [Column("features", TypeName = "jsonb")]
        public string? Features { get; set; } // JSON string

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [JsonIgnore]
        public List<Company> Companies { get; set; } = new();
    }

    [Table("companies")]
    public class Company
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Required]
        [Column("subdomain")]
        public string Subdomain { get; set; } = string.Empty;

        [Column("logo_url")]
        public string? LogoUrl { get; set; }

        [Column("banner_url")]
        public string? BannerUrl { get; set; }

        [Column("contact_email")]
        public string? ContactEmail { get; set; }

        [Column("contact_phone")]
        public string? ContactPhone { get; set; }

        [Column("address")]
        public string? Address { get; set; }

        [Column("delivery_charge")]
        public decimal DeliveryCharge { get; set; } = 0.00m;

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("subscription_plan_id")]
        public Guid? SubscriptionPlanId { get; set; }

        [ForeignKey(nameof(SubscriptionPlanId))]
        public SubscriptionPlan? SubscriptionPlan { get; set; }

        [Column("subscription_expires_at")]
        public DateTime? SubscriptionExpiresAt { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        [JsonIgnore]
        public List<User> Users { get; set; } = new();
        [JsonIgnore]
        public List<Category> Categories { get; set; } = new();
        [JsonIgnore]
        public List<Brand> Brands { get; set; } = new();
        [JsonIgnore]
        public List<Product> Products { get; set; } = new();
        [JsonIgnore]
        public List<Order> Orders { get; set; } = new();
        [JsonIgnore]
        public List<Payment> Payments { get; set; } = new();
        [JsonIgnore]
        public List<CompanySetting> CompanySettings { get; set; } = new();
        [JsonIgnore]
        public List<AuditLog> AuditLogs { get; set; } = new();
    }

    [Table("roles")]
    public class Role
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [Column("name")]
        public string Name { get; set; } = string.Empty; // 'SUPER_ADMIN', 'COMPANY_ADMIN', etc.

        [Column("description")]
        public string? Description { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [JsonIgnore]
        public List<UserRole> UserRoles { get; set; } = new();
        [JsonIgnore]
        public List<RolePermission> RolePermissions { get; set; } = new();
    }

    [Table("permissions")]
    public class Permission
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Column("description")]
        public string? Description { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [JsonIgnore]
        public List<RolePermission> RolePermissions { get; set; } = new();
    }

    [Table("user_roles")]
    public class UserRole
    {
        [Column("user_id")]
        public Guid UserId { get; set; }

        [ForeignKey(nameof(UserId))]
        public User? User { get; set; }

        [Column("role_id")]
        public Guid RoleId { get; set; }

        [ForeignKey(nameof(RoleId))]
        public Role? Role { get; set; }
    }

    [Table("role_permissions")]
    public class RolePermission
    {
        [Column("role_id")]
        public Guid RoleId { get; set; }

        [ForeignKey(nameof(RoleId))]
        public Role? Role { get; set; }

        [Column("permission_id")]
        public Guid PermissionId { get; set; }

        [ForeignKey(nameof(PermissionId))]
        public Permission? Permission { get; set; }
    }

    [Table("users")]
    public class User
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Column("company_id")]
        public Guid? CompanyId { get; set; } // Nullable for Super Admin

        [ForeignKey(nameof(CompanyId))]
        public Company? Company { get; set; }

        [Required]
        [EmailAddress]
        [Column("email")]
        public string Email { get; set; } = string.Empty;

        [Required]
        [JsonIgnore]
        [Column("password_hash")]
        public string PasswordHash { get; set; } = string.Empty;

        [Required]
        [Column("first_name")]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [Column("last_name")]
        public string LastName { get; set; } = string.Empty;

        [Column("phone_number")]
        public string? PhoneNumber { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("otp")]
        public string? Otp { get; set; }

        [Column("otp_expires_at")]
        public DateTime? OtpExpiresAt { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public List<UserRole> UserRoles { get; set; } = new();
        [JsonIgnore]
        public List<Order> Orders { get; set; } = new();
        [JsonIgnore]
        public List<AuditLog> AuditLogs { get; set; } = new();
    }
    [Table("suppliers")]
    public class Supplier
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Column("company_id")]
        public Guid CompanyId { get; set; }

        [ForeignKey(nameof(CompanyId))]
        [JsonIgnore]
        public Company? Company { get; set; }

        [Required]
        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Column("address")]
        public string? Address { get; set; }

        [Column("phone_number")]
        public string? PhoneNumber { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    [Table("categories")]
    public class Category
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Column("company_id")]
        public Guid CompanyId { get; set; }

        [Column("sizes")]
        public string? Sizes { get; set; }

        [ForeignKey(nameof(CompanyId))]
        [JsonIgnore]
        public Company? Company { get; set; }

        [Required]
        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Required]
        [Column("slug")]
        public string Slug { get; set; } = string.Empty;

        [Column("description")]
        public string? Description { get; set; }

        [Column("parent_id")]
        public Guid? ParentId { get; set; }

        [ForeignKey(nameof(ParentId))]
        [JsonIgnore]
        public Category? Parent { get; set; }

        public List<Category> Children { get; set; } = new();
        
        [JsonIgnore]
        public List<Product> Products { get; set; } = new();

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    [Table("brands")]
    public class Brand
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Column("company_id")]
        public Guid CompanyId { get; set; }

        [ForeignKey(nameof(CompanyId))]
        [JsonIgnore]
        public Company? Company { get; set; }

        [Required]
        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Required]
        [Column("slug")]
        public string Slug { get; set; } = string.Empty;

        [Column("description")]
        public string? Description { get; set; }

        [Column("logo_url")]
        public string? LogoUrl { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [JsonIgnore]
        public List<Product> Products { get; set; } = new();
    }

    [Table("products")]
    public class Product
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Column("company_id")]
        public Guid CompanyId { get; set; }

        [ForeignKey(nameof(CompanyId))]
        [JsonIgnore]
        public Company? Company { get; set; }

        [Required]
        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Required]
        [Column("slug")]
        public string Slug { get; set; } = string.Empty;

        [Required]
        [Column("sku")]
        public string Sku { get; set; } = string.Empty;

        [Required]
        [Column("barcode")]
        public string Barcode { get; set; } = string.Empty;

        [Column("description")]
        public string? Description { get; set; }

        [Required]
        [Column("price")]
        public decimal Price { get; set; } // Retail Price

        [Required]
        [Column("wholesale_price")]
        public decimal WholesalePrice { get; set; }

        [Column("stock_quantity")]
        public int StockQuantity { get; set; } = 0;

        [Column("status")]
        public string Status { get; set; } = "PUBLISHED"; // 'PUBLISHED', 'DRAFT', 'OUT_OF_STOCK'

        [Column("image_url")]
        public string? ImageUrl { get; set; }

        [Column("category_id")]
        public Guid? CategoryId { get; set; }

        [ForeignKey(nameof(CategoryId))]
        public Category? Category { get; set; }

        [Column("brand_id")]
        public Guid? BrandId { get; set; }

        [ForeignKey(nameof(BrandId))]
        public Brand? Brand { get; set; }

        [Column("size")]
        public string? Size { get; set; }

        [Column("supplier_id")]
        public Guid? SupplierId { get; set; }

        [ForeignKey(nameof(SupplierId))]
        public Supplier? Supplier { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    [Table("orders")]
    public class Order
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Column("company_id")]
        public Guid CompanyId { get; set; }

        [ForeignKey(nameof(CompanyId))]
        [JsonIgnore]
        public Company? Company { get; set; }

        [Required]
        [Column("order_number")]
        public string OrderNumber { get; set; } = string.Empty;

        [Required]
        [Column("sale_type")]
        public string SaleType { get; set; } = "ECOMMERCE"; // 'ECOMMERCE', 'POS'

        [Column("sales_staff_id")]
        public Guid? SalesStaffId { get; set; }

        [ForeignKey(nameof(SalesStaffId))]
        public User? SalesStaff { get; set; }

        [Column("customer_name")]
        public string? CustomerName { get; set; }

        [Column("customer_phone")]
        public string? CustomerPhone { get; set; }

        [Column("status")]
        public string Status { get; set; } = "PENDING"; // 'PENDING', 'PROCESSING', 'COMPLETED', 'CANCELLED'

        [Column("subtotal")]
        public decimal Subtotal { get; set; }

        [Column("discount")]
        public decimal Discount { get; set; } = 0.00m;

        [Column("tax")]
        public decimal Tax { get; set; } = 0.00m;

        [Column("shipping_fee")]
        public decimal ShippingFee { get; set; } = 0.00m;

        [Column("total")]
        public decimal Total { get; set; }

        [Required]
        [Column("payment_method")]
        public string PaymentMethod { get; set; } = "CASH"; // 'CASH', 'BKASH', 'NAGAD', 'ROCKET', 'CARD'

        [Column("payment_status")]
        public string PaymentStatus { get; set; } = "PENDING"; // 'PENDING', 'PAID', 'FAILED'

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public List<OrderItem> Items { get; set; } = new();
        public List<Payment> Payments { get; set; } = new();
    }

    [Table("order_items")]
    public class OrderItem
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Column("order_id")]
        public Guid OrderId { get; set; }

        [ForeignKey(nameof(OrderId))]
        [JsonIgnore]
        public Order? Order { get; set; }

        [Column("product_id")]
        public Guid ProductId { get; set; }

        [ForeignKey(nameof(ProductId))]
        public Product? Product { get; set; }

        [Column("quantity")]
        public int Quantity { get; set; }

        [Column("price")]
        public decimal Price { get; set; }

        [Column("total_price")]
        public decimal TotalPrice { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    [Table("payments")]
    public class Payment
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Column("company_id")]
        public Guid CompanyId { get; set; }

        [ForeignKey(nameof(CompanyId))]
        [JsonIgnore]
        public Company? Company { get; set; }

        [Column("order_id")]
        public Guid OrderId { get; set; }

        [ForeignKey(nameof(OrderId))]
        [JsonIgnore]
        public Order? Order { get; set; }

        [Column("transaction_id")]
        public string? TransactionId { get; set; }

        [Required]
        [Column("provider")]
        public string Provider { get; set; } = string.Empty; // 'BKASH', 'NAGAD', 'ROCKET', 'CASH'

        [Column("amount")]
        public decimal Amount { get; set; }

        [Column("status")]
        public string Status { get; set; } = "PENDING"; // 'PENDING', 'SUCCESS', 'FAILED'

        [Column("payment_type")]
        public string PaymentType { get; set; } = "AUTOMATED"; // 'AUTOMATED', 'MANUAL'

        [Column("sender_number")]
        public string? SenderNumber { get; set; }

        [Column("reference_log")]
        public string? ReferenceLog { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    [Table("company_settings")]
    public class CompanySetting
    {
        [Column("company_id")]
        public Guid CompanyId { get; set; }

        [ForeignKey(nameof(CompanyId))]
        [JsonIgnore]
        public Company? Company { get; set; }

        [Required]
        [Column("key")]
        public string Key { get; set; } = string.Empty;

        [Required]
        [Column("value")]
        public string Value { get; set; } = string.Empty;

        [Column("group_name")]
        public string GroupName { get; set; } = "GENERAL"; // 'GENERAL', 'ECOMMERCE', 'POS', 'PAYMENT'
    }

    [Table("audit_logs")]
    public class AuditLog
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Column("company_id")]
        public Guid? CompanyId { get; set; }

        [ForeignKey(nameof(CompanyId))]
        [JsonIgnore]
        public Company? Company { get; set; }

        [Column("user_id")]
        public Guid? UserId { get; set; }

        [ForeignKey(nameof(UserId))]
        [JsonIgnore]
        public User? User { get; set; }

        [Required]
        [Column("action")]
        public string Action { get; set; } = string.Empty;

        [Column("details")]
        public string? Details { get; set; }

        [Column("ip_address")]
        public string? IpAddress { get; set; }

        [Column("user_agent")]
        public string? UserAgent { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
