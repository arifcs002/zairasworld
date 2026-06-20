using Microsoft.EntityFrameworkCore;
using Ecommerce.Api.Domain;
using Microsoft.AspNetCore.Http;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace Ecommerce.Api.Infrastructure
{
    public class ApplicationDbContext : DbContext
    {
        private readonly IHttpContextAccessor? _httpContextAccessor;

        public Guid? CompanyId { get; private set; }

        public ApplicationDbContext(
            DbContextOptions<ApplicationDbContext> options,
            IHttpContextAccessor? httpContextAccessor = null) : base(options)
        {
            _httpContextAccessor = httpContextAccessor;
            ResolveTenantId();
        }

        public DbSet<SubscriptionPlan> SubscriptionPlans { get; set; }
        public DbSet<Company> Companies { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<Permission> Permissions { get; set; }
        public DbSet<UserRole> UserRoles { get; set; }
        public DbSet<RolePermission> RolePermissions { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Brand> Brands { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }
        public DbSet<Payment> Payments { get; set; }
        public DbSet<CompanySetting> CompanySettings { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<Supplier> Suppliers { get; set; }

        private void ResolveTenantId()
        {
            if (_httpContextAccessor?.HttpContext != null)
            {
                // Try to resolve from JWT claims
                var claimsPrincipal = _httpContextAccessor.HttpContext.User;
                var tenantClaim = claimsPrincipal?.FindFirst("company_id")?.Value;
                if (Guid.TryParse(tenantClaim, out var tokenTenantId))
                {
                    CompanyId = tokenTenantId;
                    return;
                }

                // Try to resolve from HTTP Header
                if (_httpContextAccessor.HttpContext.Request.Headers.TryGetValue("X-Tenant-ID", out var headerTenant))
                {
                    if (Guid.TryParse(headerTenant.ToString(), out var headerTenantId))
                    {
                        CompanyId = headerTenantId;
                    }
                }
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Composite Key for UserRole
            modelBuilder.Entity<UserRole>()
                .HasKey(ur => new { ur.UserId, ur.RoleId });

            // Composite Key for RolePermission
            modelBuilder.Entity<RolePermission>()
                .HasKey(rp => new { rp.RoleId, rp.PermissionId });

            // Composite Key for CompanySetting
            modelBuilder.Entity<CompanySetting>()
                .HasKey(cs => new { cs.CompanyId, cs.Key });

            // Unique Indexes for Tenant Isolation
            modelBuilder.Entity<Category>()
                .HasIndex(c => new { c.CompanyId, c.Slug })
                .IsUnique();

            modelBuilder.Entity<Brand>()
                .HasIndex(b => new { b.CompanyId, b.Slug })
                .IsUnique();

            modelBuilder.Entity<Product>()
                .HasIndex(p => new { p.CompanyId, p.Sku })
                .IsUnique();

            modelBuilder.Entity<Product>()
                .HasIndex(p => new { p.CompanyId, p.Barcode })
                .IsUnique();

            modelBuilder.Entity<Order>()
                .HasIndex(o => new { o.CompanyId, o.OrderNumber })
                .IsUnique();

            // Set up Foreign Keys
            modelBuilder.Entity<UserRole>()
                .HasOne(ur => ur.User)
                .WithMany(u => u.UserRoles)
                .HasForeignKey(ur => ur.UserId);

            modelBuilder.Entity<UserRole>()
                .HasOne(ur => ur.Role)
                .WithMany(r => r.UserRoles)
                .HasForeignKey(ur => ur.RoleId);

            modelBuilder.Entity<RolePermission>()
                .HasOne(rp => rp.Role)
                .WithMany(r => r.RolePermissions)
                .HasForeignKey(rp => rp.RoleId);

            modelBuilder.Entity<RolePermission>()
                .HasOne(rp => rp.Permission)
                .WithMany(p => p.RolePermissions)
                .HasForeignKey(rp => rp.PermissionId);

            // Precision for Decimal fields
            foreach (var property in modelBuilder.Model.GetEntityTypes().SelectMany(t => t.GetProperties()))
            {
                if (property.ClrType == typeof(decimal) || property.ClrType == typeof(decimal?))
                {
                    property.SetPrecision(18);
                    property.SetScale(2);
                }
            }

            // Global Query Filters (only filter if CompanyId context is resolved)
            modelBuilder.Entity<User>().HasQueryFilter(u => !CompanyId.HasValue || u.CompanyId == CompanyId);
            modelBuilder.Entity<Category>().HasQueryFilter(c => !CompanyId.HasValue || c.CompanyId == CompanyId);
            modelBuilder.Entity<Brand>().HasQueryFilter(b => !CompanyId.HasValue || b.CompanyId == CompanyId);
            modelBuilder.Entity<Product>().HasQueryFilter(p => !CompanyId.HasValue || p.CompanyId == CompanyId);
            modelBuilder.Entity<Order>().HasQueryFilter(o => !CompanyId.HasValue || o.CompanyId == CompanyId);
            modelBuilder.Entity<Payment>().HasQueryFilter(p => !CompanyId.HasValue || p.CompanyId == CompanyId);
            modelBuilder.Entity<CompanySetting>().HasQueryFilter(cs => !CompanyId.HasValue || cs.CompanyId == CompanyId);
            modelBuilder.Entity<AuditLog>().HasQueryFilter(a => !CompanyId.HasValue || a.CompanyId == CompanyId);
            modelBuilder.Entity<Supplier>().HasQueryFilter(s => !CompanyId.HasValue || s.CompanyId == CompanyId);

            // Seed SeedData
            SeedInitialData(modelBuilder);
        }

        private void SeedInitialData(ModelBuilder modelBuilder)
        {
            // Seed Subscription Plans
            var basicPlanId = new Guid("11111111-1111-1111-1111-111111111111");
            var premiumPlanId = new Guid("22222222-2222-2222-2222-222222222222");

            modelBuilder.Entity<SubscriptionPlan>().HasData(
                new SubscriptionPlan { Id = basicPlanId, Name = "Basic Plan", Price = 1500.00m, BillingCycle = "monthly", Features = "{\"max_products\": 200, \"pos_enabled\": true, \"ecommerce_enabled\": true}" },
                new SubscriptionPlan { Id = premiumPlanId, Name = "Premium Plan", Price = 3500.00m, BillingCycle = "monthly", Features = "{\"max_products\": 5000, \"pos_enabled\": true, \"ecommerce_enabled\": true, \"multi_staff\": true}" }
            );

            // Seed Roles
            var superAdminRoleId = new Guid("33333333-3333-3333-3333-333333333333");
            var companyAdminRoleId = new Guid("44444444-4444-4444-4444-444444444444");
            var companyManagerRoleId = new Guid("55555555-5555-5555-5555-555555555555");
            var salesStaffRoleId = new Guid("66666666-6666-6666-6666-666666666666");

            modelBuilder.Entity<Role>().HasData(
                new Role { Id = superAdminRoleId, Name = "SUPER_ADMIN", Description = "Platform Owner - Full access to all system tenants and diagnostics" },
                new Role { Id = companyAdminRoleId, Name = "COMPANY_ADMIN", Description = "Company Owner - Full access to settings, reports, staff management" },
                new Role { Id = companyManagerRoleId, Name = "COMPANY_MANAGER", Description = "Store Manager - Manage inventory, store config, and view basic reports" },
                new Role { Id = salesStaffRoleId, Name = "SALES_STAFF", Description = "POS Checkout Operator - Restricted access to barcodes and checkout" }
            );

            // Seed Permissions
            var diagPermId = new Guid("a1111111-1111-1111-1111-111111111111");
            var compPermId = new Guid("a2222222-2222-2222-2222-222222222222");
            var subsPermId = new Guid("a3333333-3333-3333-3333-333333333333");
            var settPermId = new Guid("a4444444-4444-4444-4444-444444444444");
            var staffPermId = new Guid("a5555555-5555-5555-5555-555555555555");
            var invPermId = new Guid("a6666666-6666-6666-6666-666666666666");
            var posPermId = new Guid("a7777777-7777-7777-7777-777777777777");
            var repFullPermId = new Guid("a8888888-8888-8888-8888-888888888888");
            var repOpPermId = new Guid("a9999999-9999-9999-9999-999999999999");

            modelBuilder.Entity<Permission>().HasData(
                new Permission { Id = diagPermId, Name = "platform:diagnostics", Description = "Access SaaS metrics and platform logs" },
                new Permission { Id = compPermId, Name = "manage:companies", Description = "Add, suspend, or upgrade tenant companies" },
                new Permission { Id = subsPermId, Name = "manage:subscriptions", Description = "Configure billing plans" },
                new Permission { Id = settPermId, Name = "company:settings", Description = "Update company-wide configuration" },
                new Permission { Id = staffPermId, Name = "manage:staff", Description = "Create/deactivate managers and checkout staff" },
                new Permission { Id = invPermId, Name = "manage:inventory", Description = "Create products and trigger barcode generation" },
                new Permission { Id = posPermId, Name = "pos:checkout", Description = "Scan barcodes and complete POS checkout" },
                new Permission { Id = repFullPermId, Name = "reports:full", Description = "Access full company financial and inventory audits" },
                new Permission { Id = repOpPermId, Name = "reports:operational", Description = "Access daily operational reports" }
            );

            // Role-Permissions Map
            modelBuilder.Entity<RolePermission>().HasData(
                // Super Admin
                new RolePermission { RoleId = superAdminRoleId, PermissionId = diagPermId },
                new RolePermission { RoleId = superAdminRoleId, PermissionId = compPermId },
                new RolePermission { RoleId = superAdminRoleId, PermissionId = subsPermId },
                // Company Admin
                new RolePermission { RoleId = companyAdminRoleId, PermissionId = settPermId },
                new RolePermission { RoleId = companyAdminRoleId, PermissionId = staffPermId },
                new RolePermission { RoleId = companyAdminRoleId, PermissionId = invPermId },
                new RolePermission { RoleId = companyAdminRoleId, PermissionId = posPermId },
                new RolePermission { RoleId = companyAdminRoleId, PermissionId = repFullPermId },
                // Company Manager
                new RolePermission { RoleId = companyManagerRoleId, PermissionId = invPermId },
                new RolePermission { RoleId = companyManagerRoleId, PermissionId = posPermId },
                new RolePermission { RoleId = companyManagerRoleId, PermissionId = repOpPermId },
                // Sales Staff
                new RolePermission { RoleId = salesStaffRoleId, PermissionId = posPermId }
            );

            // Seed Demo Company
            var demoCompanyId = new Guid("b1111111-1111-1111-1111-111111111111");
            modelBuilder.Entity<Company>().HasData(
                new Company
                {
                    Id = demoCompanyId,
                    Name = "Zaira's World",
                    Subdomain = "zairasworld",
                    ContactEmail = "info@zairasworld.com",
                    ContactPhone = "01626-458189",
                    Address = "Dhaka, Bangladesh",
                    LogoUrl = "/uploads/zairas_world_logo.png",
                    DeliveryCharge = 60.00m,
                    IsActive = true,
                    SubscriptionPlanId = premiumPlanId,
                    SubscriptionExpiresAt = DateTime.UtcNow.AddYears(1)
                }
            );

            // Seed Super Admin User (Password is hashed: '123456' using BCrypt)
            var superAdminUserId = new Guid("99999999-9999-9999-9999-999999999999");
            var compAdminUserId = new Guid("88888888-8888-8888-8888-888888888888");

            var superAdminPasswordHash = BCrypt.Net.BCrypt.HashPassword("123456");
            var passwordHash = BCrypt.Net.BCrypt.HashPassword("admin123");

            modelBuilder.Entity<User>().HasData(
                new User
                {
                    Id = superAdminUserId,
                    CompanyId = null, // Global
                    Email = "arifowneradmin.bd",
                    PasswordHash = superAdminPasswordHash,
                    FirstName = "Platform",
                    LastName = "Owner",
                    PhoneNumber = "+8801500000000",
                    IsActive = true
                },
                new User
                {
                    Id = compAdminUserId,
                    CompanyId = demoCompanyId, // Bound to Demo Company
                    Email = "admin@demo.com",
                    PasswordHash = passwordHash,
                    FirstName = "Demo",
                    LastName = "Admin",
                    PhoneNumber = "01626-458189",
                    IsActive = true
                }
            );

            // User-Roles Assignment
            modelBuilder.Entity<UserRole>().HasData(
                new UserRole { UserId = superAdminUserId, RoleId = superAdminRoleId },
                new UserRole { UserId = compAdminUserId, RoleId = companyAdminRoleId }
            );

            // Seed Demo Company Settings
            modelBuilder.Entity<CompanySetting>().HasData(
                new CompanySetting { CompanyId = demoCompanyId, Key = "shop_currency", Value = "BDT", GroupName = "GENERAL" },
                new CompanySetting { CompanyId = demoCompanyId, Key = "receipt_header", Value = "Thank you for shopping at Zaira's World!", GroupName = "POS" },
                new CompanySetting { CompanyId = demoCompanyId, Key = "receipt_footer", Value = "Follow us on FB: fb.com/profile.php?id=61583524082495", GroupName = "POS" }
            );

            // Seed Demo Suppliers
            var supplierApexId = new Guid("51111111-1111-1111-1111-111111111111");
            var supplierBataId = new Guid("52222222-2222-2222-2222-222222222222");
            modelBuilder.Entity<Supplier>().HasData(
                new Supplier { Id = supplierApexId, CompanyId = demoCompanyId, Name = "Apex Bangladesh", PhoneNumber = "01711122233", Address = "Dhaka" },
                new Supplier { Id = supplierBataId, CompanyId = demoCompanyId, Name = "Bata Bangladesh", PhoneNumber = "01799887766", Address = "Tongi, Gazipur" }
            );

            // Seed Demo Category & Brand & Product
            var clothingCatId = new Guid("c1111111-1111-1111-1111-111111111111");
            var babyShoesCatId = new Guid("c2222222-2222-2222-2222-222222222222");
            var teenageShoesCatId = new Guid("c3333333-3333-3333-3333-333333333333");
            var olderShoesCatId = new Guid("c4444444-4444-4444-4444-444444444444");

            modelBuilder.Entity<Category>().HasData(
                new Category { Id = clothingCatId, CompanyId = demoCompanyId, Name = "Sports Shoes", Slug = "sports-shoes", Description = "Running, tennis and sportswear shoes", Sizes = "39,40,41,42,43,44" },
                new Category { Id = babyShoesCatId, CompanyId = demoCompanyId, Name = "Baby Shoes", Slug = "baby-shoes", Description = "Shoe sizes 1 to 6 for toddlers", Sizes = "1,2,3,4,5,6" },
                new Category { Id = teenageShoesCatId, CompanyId = demoCompanyId, Name = "Teenage Shoes", Slug = "teenage-shoes", Description = "Shoe sizes 6 to 10 for kids & teens", Sizes = "6,7,8,9,10" },
                new Category { Id = olderShoesCatId, CompanyId = demoCompanyId, Name = "Casual Sneakers", Slug = "casual-sneakers", Description = "Shoe sizes 39 to 45 for adults", Sizes = "39,40,41,42,43,44,45" }
            );

            var ecotexBrandId = new Guid("d1111111-1111-1111-1111-111111111111");
            modelBuilder.Entity<Brand>().HasData(
                new Brand { Id = ecotexBrandId, CompanyId = demoCompanyId, Name = "Zaira Brand", Slug = "zaira-brand", Description = "Zaira's World Premium Shoe Selection" }
            );

            var poloProductId = new Guid("f1111111-1111-1111-1111-111111111111");
            modelBuilder.Entity<Product>().HasData(
                new Product
                {
                    Id = poloProductId,
                    CompanyId = demoCompanyId,
                    Name = "Air Force Retro Sneaker",
                    Slug = "air-force-retro-sneaker",
                    Sku = "Z-SNEAKER-001",
                    Barcode = "2000010010015",
                    Description = "Stylish retro sport shoe for casual wear.",
                    Price = 3200.00m,
                    WholesalePrice = 1800.00m,
                    StockQuantity = 45,
                    Status = "PUBLISHED",
                    CategoryId = olderShoesCatId,
                    BrandId = ecotexBrandId,
                    Size = "42",
                    ImageUrl = "/uploads/zairas_world_logo.png"
                }
            );
        }

        public override int SaveChanges()
        {
            PopulateTenantId();
            return base.SaveChanges();
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            PopulateTenantId();
            return base.SaveChangesAsync(cancellationToken);
        }

        private void PopulateTenantId()
        {
            if (!CompanyId.HasValue) return;

            foreach (var entry in ChangeTracker.Entries())
            {
                if (entry.State == EntityState.Added)
                {
                    var companyIdProp = entry.Entity.GetType().GetProperty("CompanyId");
                    if (companyIdProp != null && companyIdProp.PropertyType == typeof(Guid))
                    {
                        var currentVal = (Guid)companyIdProp.GetValue(entry.Entity);
                        if (currentVal == Guid.Empty)
                        {
                            companyIdProp.SetValue(entry.Entity, CompanyId.Value);
                        }
                    }
                    else if (companyIdProp != null && companyIdProp.PropertyType == typeof(Guid?))
                    {
                        var currentVal = (Guid?)companyIdProp.GetValue(entry.Entity);
                        if (currentVal == null || currentVal == Guid.Empty)
                        {
                            companyIdProp.SetValue(entry.Entity, CompanyId);
                        }
                    }
                }
            }
        }
    }
}
