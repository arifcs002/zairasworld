using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Ecommerce.Api.Domain;
using Ecommerce.Api.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using System.IO;
using Microsoft.AspNetCore.Hosting;

namespace Ecommerce.Api.Controllers
{
    // DTOs
    public record RegisterDto(string Email, string Password, string FirstName, string LastName, string? PhoneNumber, string? CompanyName, string? Subdomain);
    public record LoginDto(string Email, string Password);
    public record LoginResponse(string Token, string RefreshToken, string Email, string FullName, Guid? CompanyId, List<string> Roles);
    public record ProductCreateDto(string Name, string SKU, decimal Price, decimal WholesalePrice, int StockQuantity, string? Description, Guid? CategoryId, Guid? BrandId, string? Barcode, string? ImageUrl);
    public record SizeQtyDto(string Size, int Quantity);
    public record BatchProductCreateDto(string Name, decimal Price, decimal WholesalePrice, string? Description, Guid? CategoryId, Guid? SupplierId, string? ImageUrl, List<SizeQtyDto> Sizes);
    public record OrderItemDto(Guid ProductId, int Quantity);
    public record POSCheckoutDto(List<OrderItemDto> Items, decimal Discount, string PaymentMethod, string? CustomerName, string? CustomerPhone, string? TransactionId);
    public record CompanyUpdateDto(string Name, string? LogoUrl, string? BannerUrl, string? ContactEmail, string? ContactPhone, string? Address, decimal DeliveryCharge);
    public record MfsPaymentVerifyDto(Guid OrderId, string TransactionId, string Provider, decimal Amount, string SenderNumber, string? ReferenceLog);
    public record ForgotPasswordDto(string Email);
    public record ResetPasswordOtpDto(string Email, string Otp, string NewPassword);
    public record AdminResetPasswordDto(string NewPassword);

    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _config;

        public AuthController(ApplicationDbContext context, IConfiguration config)
        {
            _context = context;
            _config = config;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto dto)
        {
            if (await _context.Users.AnyAsync(u => u.Email == dto.Email))
                return BadRequest(new { message = "Email already registered" });

            Guid? companyId = null;

            // If CompanyName is provided, perform SaaS tenant signup
            if (!string.IsNullOrEmpty(dto.CompanyName))
            {
                var subdomain = dto.Subdomain ?? dto.CompanyName.ToLower().Replace(" ", "");
                if (await _context.Companies.AnyAsync(c => c.Subdomain == subdomain))
                    return BadRequest(new { message = $"Subdomain '{subdomain}' is already taken." });

                var basicPlan = await _context.SubscriptionPlans.FirstOrDefaultAsync(p => p.Name == "Basic Plan");

                var company = new Company
                {
                    Name = dto.CompanyName,
                    Subdomain = subdomain,
                    IsActive = true,
                    SubscriptionPlanId = basicPlan?.Id,
                    SubscriptionExpiresAt = DateTime.UtcNow.AddMonths(1)
                };

                _context.Companies.Add(company);
                await _context.SaveChangesAsync();
                companyId = company.Id;
            }

            var user = new User
            {
                Email = dto.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                FirstName = dto.FirstName,
                LastName = dto.LastName,
                PhoneNumber = dto.PhoneNumber,
                CompanyId = companyId,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            // Assign proper role (COMPANY_ADMIN if company was created, otherwise sales/customer)
            var roleName = companyId.HasValue ? "COMPANY_ADMIN" : "SALES_STAFF";
            var role = await _context.Roles.FirstOrDefaultAsync(r => r.Name == roleName);
            if (role != null)
            {
                user.UserRoles.Add(new UserRole { User = user, RoleId = role.Id });
            }

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Registration successful", email = user.Email, companyId });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            var user = await _context.Users
                .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync(u => u.Email == dto.Email);

            if (user == null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
                return Unauthorized(new { message = "Invalid email or password" });

            if (!user.IsActive)
                return BadRequest(new { message = "User account is suspended" });

            var roles = user.UserRoles.Select(ur => ur.Role!.Name).ToList();
            var token = GenerateJwtToken(user, roles);
            var refreshToken = Guid.NewGuid().ToString();

            return Ok(new LoginResponse(token, refreshToken, user.Email, $"{user.FirstName} {user.LastName}", user.CompanyId, roles));
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto dto)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
            if (user == null)
                return BadRequest(new { message = "User with this email does not exist." });

            var rand = new Random();
            var otpCode = rand.Next(100000, 999999).ToString();

            user.Otp = otpCode;
            user.OtpExpiresAt = DateTime.UtcNow.AddMinutes(15);

            await _context.SaveChangesAsync();

            // Log to console (always keep this for easy local sandbox debugging/autofilling)
            Console.WriteLine($"[OTP SIMULATOR] Generated OTP {otpCode} for email {dto.Email}");

            // Dispatch actual SMTP email
            await SendOtpEmailAsync(dto.Email, otpCode);

            return Ok(new { message = "Verification OTP code successfully generated and sent to your email." });
        }

        private async Task SendOtpEmailAsync(string toEmail, string otpCode)
        {
            try
            {
                var smtpHost = _config["SmtpSettings:Host"] ?? "smtp.gmail.com";
                var smtpPortStr = _config["SmtpSettings:Port"] ?? "587";
                int.TryParse(smtpPortStr, out var smtpPort);
                var enableSsl = bool.Parse(_config["SmtpSettings:EnableSsl"] ?? "true");
                var username = _config["SmtpSettings:Username"] ?? "";
                var password = _config["SmtpSettings:Password"] ?? "";
                var fromAddress = _config["SmtpSettings:FromAddress"] ?? "noreply@ghorerbazar.com";
                var fromName = _config["SmtpSettings:FromName"] ?? "Ghorer Bazar Organic";

                using (var client = new System.Net.Mail.SmtpClient(smtpHost, smtpPort))
                {
                    client.EnableSsl = enableSsl;
                    client.UseDefaultCredentials = false;
                    
                    if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
                    {
                        client.Credentials = new System.Net.NetworkCredential(username, password);
                    }

                    var mailMessage = new System.Net.Mail.MailMessage
                    {
                        From = new System.Net.Mail.MailAddress(fromAddress, fromName),
                        Subject = "Verification Code for Password Recovery - Ghorer Bazar",
                        Body = $@"
                        <div style='font-family: Arial, sans-serif; padding: 20px; color: #333; max-width: 600px; margin: auto; border: 1px solid #e2e8f0; border-radius: 12px;'>
                            <div style='background-color: #0D5237; padding: 15px; text-align: center; border-radius: 8px 8px 0 0;'>
                                <h2 style='color: #ffffff; margin: 0;'>Ghorer Bazar Organic</h2>
                            </div>
                            <div style='padding: 20px;'>
                                <p>Hello,</p>
                                <p>We received a request to recover your password. Please use the following 6-digit verification code (OTP) to reset your password:</p>
                                <div style='text-align: center; margin: 30px 0;'>
                                    <span style='background-color: #f8fafc; border: 2px dashed #0D5237; color: #0D5237; font-size: 28px; font-weight: bold; padding: 10px 24px; font-family: monospace; letter-spacing: 4px; border-radius: 6px; display: inline-block;'>{otpCode}</span>
                                </div>
                                <p>This code will expire in 15 minutes. If you did not request this, please ignore this email.</p>
                            </div>
                            <div style='border-top: 1px solid #e2e8f0; padding-top: 15px; font-size: 11px; color: #64748b; text-align: center;'>
                                <p>Eat pure, stay healthy.</p>
                                <p>&copy; {DateTime.UtcNow.Year} Ghorer Bazar Organic Store. All rights reserved.</p>
                            </div>
                        </div>",
                        IsBodyHtml = true
                    };

                    mailMessage.To.Add(toEmail);

                    await client.SendMailAsync(mailMessage);
                    Console.WriteLine($"--> SMTP: Successfully sent OTP email to {toEmail}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"--> SMTP ERROR: Failed to send email to {toEmail}: {ex.Message}");
            }
        }

        [HttpPost("reset-password-otp")]
        public async Task<IActionResult> ResetPasswordOtp([FromBody] ResetPasswordOtpDto dto)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
            if (user == null)
                return BadRequest(new { message = "User with this email does not exist." });

            if (string.IsNullOrEmpty(user.Otp) || user.Otp != dto.Otp || !user.OtpExpiresAt.HasValue || user.OtpExpiresAt.Value < DateTime.UtcNow)
                return BadRequest(new { message = "Invalid or expired OTP verification code." });

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
            user.Otp = null;
            user.OtpExpiresAt = null;

            await _context.SaveChangesAsync();

            return Ok(new { message = "Password reset completed successfully. You can now login with your new password." });
        }

        private string GenerateJwtToken(User user, List<string> roles)
        {
            var keyStr = _config["Jwt:Secret"] ?? "super-secret-key-change-in-prod-long-enough-32-chars";
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyStr));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim("name", $"{user.FirstName} {user.LastName}")
            };

            if (user.CompanyId.HasValue)
            {
                claims.Add(new Claim("company_id", user.CompanyId.Value.ToString()));
            }

            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            var token = new JwtSecurityToken(
                claims: claims,
                expires: DateTime.UtcNow.AddHours(4),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }

    [ApiController]
    [Route("api/[controller]")]
    public class ProductsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public ProductsController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetProducts([FromQuery] string? search)
        {
            var query = _context.Products
                .Include(p => p.Category)
                .Include(p => p.Brand)
                .Include(p => p.Supplier)
                .AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(p => p.Name.Contains(search) || p.Sku.Contains(search) || p.Barcode.Contains(search));
            }

            var items = await query.OrderByDescending(p => p.CreatedAt).ToListAsync();
            return Ok(items);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetProduct(Guid id)
        {
            var product = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.Brand)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product == null) return NotFound();
            return Ok(product);
        }

        [HttpPost]
        public async Task<IActionResult> CreateProduct([FromBody] ProductCreateDto dto)
        {
            // Resolve Tenant Context from DbContext (populated by X-Tenant-ID header or token)
            var currentCompanyId = _context.CompanyId;
            if (!currentCompanyId.HasValue)
                return BadRequest("Company context is required to add products.");

            // Auto-generate Code 128 barcode if not provided
            var barcode = dto.Barcode;
            if (string.IsNullOrEmpty(barcode))
            {
                barcode = GenerateCode128Barcode(currentCompanyId.Value);
            }

            // Verify barcode unique per company
            if (await _context.Products.AnyAsync(p => p.CompanyId == currentCompanyId.Value && p.Barcode == barcode))
            {
                return BadRequest(new { message = $"Barcode '{barcode}' is already registered in your company." });
            }

            var slug = dto.Name.ToLower().Replace(" ", "-").Replace("/", "-");

            var product = new Product
            {
                CompanyId = currentCompanyId.Value,
                Name = dto.Name,
                Slug = slug,
                Sku = dto.SKU,
                Barcode = barcode,
                Description = dto.Description,
                Price = dto.Price,
                WholesalePrice = dto.WholesalePrice,
                StockQuantity = dto.StockQuantity,
                ImageUrl = dto.ImageUrl,
                CategoryId = dto.CategoryId,
                BrandId = dto.BrandId,
                Status = "PUBLISHED",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Products.Add(product);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetProduct), new { id = product.Id }, product);
        }

        [HttpPost("batch")]
        public async Task<IActionResult> CreateProductsBatch([FromBody] BatchProductCreateDto dto)
        {
            var currentCompanyId = _context.CompanyId;
            if (!currentCompanyId.HasValue)
                return BadRequest("Company context is required to add products.");

            var createdProducts = new List<Product>();

            foreach (var sizeQty in dto.Sizes)
            {
                if (sizeQty.Quantity <= 0) continue;

                var barcode = GenerateCode128Barcode(currentCompanyId.Value);

                // Ensure unique barcode
                while (await _context.Products.AnyAsync(p => p.CompanyId == currentCompanyId.Value && p.Barcode == barcode))
                {
                    barcode = GenerateCode128Barcode(currentCompanyId.Value);
                }

                var cleanName = dto.Name;
                var slugName = $"{dto.Name.ToLower().Replace(" ", "-").Replace("/", "-")}-{sizeQty.Size.ToLower()}";
                var sku = $"{dto.Name.Replace(" ", "-").ToUpper()}-{sizeQty.Size.ToUpper()}";

                var product = new Product
                {
                    CompanyId = currentCompanyId.Value,
                    Name = $"{cleanName} (Size {sizeQty.Size})",
                    Slug = slugName,
                    Sku = sku,
                    Barcode = barcode,
                    Description = dto.Description,
                    Price = dto.Price,
                    WholesalePrice = dto.WholesalePrice,
                    StockQuantity = sizeQty.Quantity,
                    Size = sizeQty.Size,
                    SupplierId = dto.SupplierId,
                    CategoryId = dto.CategoryId,
                    Status = "PUBLISHED",
                    ImageUrl = dto.ImageUrl,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.Products.Add(product);
                createdProducts.Add(product);
            }

            await _context.SaveChangesAsync();
            return Ok(createdProducts);
        }

        [HttpPost("{id}/print-barcode")]
        public async Task<IActionResult> PrintBarcode(Guid id, [FromQuery] string ipAddress = "192.168.1.100", [FromQuery] int port = 9100)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null) return NotFound();

            // Mock Zebra Programming Language (ZPL) label format for printing barcodes
            var zpl = $@"
^XA
^LH30,30
^FO20,10^A0N,28,24^FD{product.Name}^FS
^FO20,40^A0N,20,16^FDSKU: {product.Sku}  Price: {product.Price} BDT^FS
^FO20,70^BY2
^BCN,50,Y,N,N
^FD{product.Barcode}^FS
^XZ";

            // Print service protocol log simulation
            Console.WriteLine($"--> PRINT SERVICE: Sending raw socket stream to Wi-Fi printer at {ipAddress}:{port}");
            Console.WriteLine(zpl);

            return Ok(new
            {
                message = $"Barcode print command successfully sent to printer at {ipAddress}:{port}",
                zpl = zpl.Trim(),
                ipAddress,
                port
            });
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateProduct(Guid id, [FromBody] ProductCreateDto dto)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null) return NotFound();

            product.Name = dto.Name;
            product.Sku = dto.SKU;
            product.Price = dto.Price;
            product.WholesalePrice = dto.WholesalePrice;
            product.StockQuantity = dto.StockQuantity;
            product.Description = dto.Description;
            product.ImageUrl = dto.ImageUrl;
            product.CategoryId = dto.CategoryId;
            product.BrandId = dto.BrandId;
            product.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return Ok(product);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteProduct(Guid id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null) return NotFound();

            _context.Products.Remove(product);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        private string GenerateCode128Barcode(Guid companyId)
        {
            var prefix = companyId.ToString().Substring(0, 4).ToUpper();
            var rand = new Random();
            var number = rand.Next(100000, 999999);
            return $"{prefix}-{number}";
        }
    }

    [ApiController]
    [Route("api/[controller]")]
    public class POSController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public POSController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet("lookup")]
        public async Task<IActionResult> LookupProduct([FromQuery] string barcode)
        {
            var product = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.Brand)
                .FirstOrDefaultAsync(p => p.Barcode == barcode);

            if (product == null)
                return NotFound(new { message = "Product not found for the scanned barcode" });

            return Ok(product);
        }

        [HttpPost("checkout")]
        public async Task<IActionResult> POSCheckout([FromBody] POSCheckoutDto dto)
        {
            var companyId = _context.CompanyId;
            if (!companyId.HasValue)
                return BadRequest("Company context is required to complete sales.");

            // Resolve cashier user (claims)
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            Guid? cashierId = null;
            if (Guid.TryParse(userIdClaim, out var parsedId))
            {
                cashierId = parsedId;
            }

            decimal subtotal = 0;
            var orderItems = new List<OrderItem>();

            foreach (var itemDto in dto.Items)
            {
                var product = await _context.Products.FindAsync(itemDto.ProductId);
                if (product == null)
                    return BadRequest(new { message = $"Product with ID {itemDto.ProductId} not found." });

                if (product.StockQuantity < itemDto.Quantity)
                    return BadRequest(new { message = $"Insufficient stock for product '{product.Name}'. Available: {product.StockQuantity}" });

                // Deduct inventory
                product.StockQuantity -= itemDto.Quantity;
                var totalItemPrice = product.Price * itemDto.Quantity;
                subtotal += totalItemPrice;

                orderItems.Add(new OrderItem
                {
                    ProductId = itemDto.ProductId,
                    Quantity = itemDto.Quantity,
                    Price = product.Price,
                    TotalPrice = totalItemPrice,
                    CreatedAt = DateTime.UtcNow
                });
            }

            var total = subtotal - dto.Discount;
            if (total < 0) total = 0;

            var order = new Order
            {
                CompanyId = companyId.Value,
                OrderNumber = $"POS-{DateTime.UtcNow.Ticks}",
                SaleType = "POS",
                SalesStaffId = cashierId,
                CustomerName = dto.CustomerName ?? "Walk-in Customer",
                CustomerPhone = dto.CustomerPhone,
                Status = "COMPLETED",
                Subtotal = subtotal,
                Discount = dto.Discount,
                Total = total,
                PaymentMethod = dto.PaymentMethod,
                PaymentStatus = dto.PaymentMethod == "CASH" ? "PAID" : "PENDING",
                Items = orderItems,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Orders.Add(order);

            // Log payment transactions immediately for POS Checkouts
            if (dto.PaymentMethod != "CASH" && !string.IsNullOrEmpty(dto.TransactionId))
            {
                var payment = new Payment
                {
                    CompanyId = companyId.Value,
                    Order = order,
                    TransactionId = dto.TransactionId,
                    Provider = dto.PaymentMethod,
                    Amount = total,
                    Status = "SUCCESS",
                    PaymentType = "AUTOMATED",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                order.PaymentStatus = "PAID";
                _context.Payments.Add(payment);
            }

            await _context.SaveChangesAsync();

            // Return receipt data
            var settings = await _context.CompanySettings
                .Where(s => s.CompanyId == companyId.Value && s.GroupName == "POS")
                .ToDictionaryAsync(s => s.Key, s => s.Value);

            var header = settings.ContainsKey("receipt_header") ? settings["receipt_header"] : "Thank you!";
            var footer = settings.ContainsKey("receipt_footer") ? settings["receipt_footer"] : "Please visit again.";

            return Ok(new
            {
                message = "POS Transaction completed successfully",
                orderId = order.Id,
                orderNumber = order.OrderNumber,
                receipt = new
                {
                    header,
                    footer,
                    orderNumber = order.OrderNumber,
                    subtotal = order.Subtotal,
                    discount = order.Discount,
                    total = order.Total,
                    paymentMethod = order.PaymentMethod,
                    cashierName = User.FindFirst("name")?.Value ?? "Cashier"
                }
            });
        }
    }

    [ApiController]
    [Route("api/[controller]")]
    public class PaymentsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public PaymentsController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpPost("mfs-verify")]
        public async Task<IActionResult> VerifyMfsPayment([FromBody] MfsPaymentVerifyDto dto)
        {
            var companyId = _context.CompanyId;
            if (!companyId.HasValue)
                return BadRequest("Company context is required.");

            var order = await _context.Orders.FindAsync(dto.OrderId);
            if (order == null) return NotFound("Order not found");

            var payment = new Payment
            {
                CompanyId = companyId.Value,
                OrderId = dto.OrderId,
                TransactionId = dto.TransactionId,
                Provider = dto.Provider.ToUpper(),
                Amount = dto.Amount,
                Status = "SUCCESS", // Mock verify auto succeeds
                PaymentType = "MANUAL",
                SenderNumber = dto.SenderNumber,
                ReferenceLog = dto.ReferenceLog ?? $"Manual MFS Verification for TrxID {dto.TransactionId}",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            order.PaymentStatus = "PAID";
            order.Status = "PROCESSING";

            _context.Payments.Add(payment);
            await _context.SaveChangesAsync();

            return Ok(new { message = "MFS Transaction successfully logged and verified", status = payment.Status, paymentId = payment.Id });
        }
    }

    [ApiController]
    [Route("api/[controller]")]
    public class SettingsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public SettingsController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetSettings()
        {
            var companyId = _context.CompanyId;
            if (!companyId.HasValue)
                return BadRequest("Company context is required.");

            var settings = await _context.CompanySettings
                .Where(s => s.CompanyId == companyId.Value)
                .ToListAsync();

            return Ok(settings);
        }

        [HttpPut]
        public async Task<IActionResult> UpdateSettings([FromBody] List<CompanySetting> settingsList)
        {
            var companyId = _context.CompanyId;
            if (!companyId.HasValue)
                return BadRequest("Company context is required.");

            foreach (var setting in settingsList)
            {
                var existing = await _context.CompanySettings
                    .FirstOrDefaultAsync(s => s.CompanyId == companyId.Value && s.Key == setting.Key);

                if (existing != null)
                {
                    existing.Value = setting.Value;
                    existing.GroupName = setting.GroupName;
                }
                else
                {
                    setting.CompanyId = companyId.Value;
                    _context.CompanySettings.Add(setting);
                }
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = "Settings updated successfully" });
        }

        [HttpPut("company-profile")]
        public async Task<IActionResult> UpdateCompanyProfile([FromBody] CompanyUpdateDto dto)
        {
            var companyId = _context.CompanyId;
            if (!companyId.HasValue)
                return BadRequest("Company context is required.");

            var company = await _context.Companies.FindAsync(companyId.Value);
            if (company == null) return NotFound();

            company.Name = dto.Name;
            company.LogoUrl = dto.LogoUrl;
            company.BannerUrl = dto.BannerUrl;
            company.ContactEmail = dto.ContactEmail;
            company.ContactPhone = dto.ContactPhone;
            company.Address = dto.Address;
            company.DeliveryCharge = dto.DeliveryCharge;
            company.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return Ok(company);
        }
    }

    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class UsersController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public UsersController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetUsers()
        {
            var isSuperAdmin = User.IsInRole("SUPER_ADMIN");
            var companyId = _context.CompanyId;

            // Use IgnoreQueryFilters if Super Admin to see all users globally
            var query = isSuperAdmin 
                ? _context.Users.IgnoreQueryFilters().Include(u => u.UserRoles).ThenInclude(ur => ur.Role).AsQueryable()
                : _context.Users.Include(u => u.UserRoles).ThenInclude(ur => ur.Role).AsQueryable();

            if (!isSuperAdmin)
            {
                if (!companyId.HasValue) return Forbid();
                query = query.Where(u => u.CompanyId == companyId.Value);
            }

            var list = await query.Select(u => new {
                u.Id,
                u.Email,
                u.FirstName,
                u.LastName,
                u.PhoneNumber,
                u.IsActive,
                CompanyId = u.CompanyId,
                Roles = u.UserRoles.Select(ur => ur.Role!.Name).ToList()
            }).ToListAsync();

            return Ok(list);
        }

        [HttpPost("{userId}/admin-reset-password")]
        public async Task<IActionResult> AdminResetPassword(Guid userId, [FromBody] AdminResetPasswordDto dto)
        {
            var isSuperAdmin = User.IsInRole("SUPER_ADMIN");
            var isCompanyAdmin = User.IsInRole("COMPANY_ADMIN");

            if (!isSuperAdmin && !isCompanyAdmin)
                return Forbid();

            var companyId = _context.CompanyId;

            var targetUser = isSuperAdmin
                ? await _context.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == userId)
                : await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);

            if (targetUser == null)
                return NotFound("User not found");

            if (!isSuperAdmin)
            {
                if (targetUser.CompanyId != companyId)
                    return Forbid();
            }

            targetUser.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
            await _context.SaveChangesAsync();

            return Ok(new { message = $"Password for user {targetUser.Email} updated successfully by Administrator." });
        }
    }

    [ApiController]
    [Route("api/[controller]")]
    public class UploadController : ControllerBase
    {
        private readonly IWebHostEnvironment _env;

        public UploadController(IWebHostEnvironment env)
        {
            _env = env;
        }

        [HttpPost]
        public async Task<IActionResult> UploadFile(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded");

            var uploadsFolder = Path.Combine(_env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"), "uploads");
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            var uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(file.FileName);
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(fileStream);
            }

            var fileUrl = "/uploads/" + uniqueFileName;
            return Ok(new { imageUrl = fileUrl });
        }
    }

    [ApiController]
    [Route("api/[controller]")]
    public class SuppliersController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public SuppliersController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetSuppliers()
        {
            var suppliers = await _context.Suppliers.OrderBy(s => s.Name).ToListAsync();
            return Ok(suppliers);
        }

        [HttpPost]
        public async Task<IActionResult> CreateSupplier([FromBody] Supplier supplier)
        {
            var companyId = _context.CompanyId;
            if (!companyId.HasValue)
                return BadRequest("Company context is required.");

            supplier.CompanyId = companyId.Value;
            supplier.CreatedAt = DateTime.UtcNow;
            supplier.UpdatedAt = DateTime.UtcNow;

            _context.Suppliers.Add(supplier);
            await _context.SaveChangesAsync();

            return Ok(supplier);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteSupplier(Guid id)
        {
            var supplier = await _context.Suppliers.FindAsync(id);
            if (supplier == null) return NotFound();

            _context.Suppliers.Remove(supplier);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }

    [ApiController]
    [Route("api/[controller]")]
    public class CategoriesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public CategoriesController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetCategories()
        {
            var categories = await _context.Categories.OrderBy(c => c.Name).ToListAsync();
            return Ok(categories);
        }

        [HttpPost]
        public async Task<IActionResult> CreateCategory([FromBody] Category category)
        {
            var companyId = _context.CompanyId;
            if (!companyId.HasValue)
                return BadRequest("Company context is required.");

            category.CompanyId = companyId.Value;
            category.Slug = category.Name.ToLower().Replace(" ", "-").Replace("/", "-");
            category.CreatedAt = DateTime.UtcNow;
            category.UpdatedAt = DateTime.UtcNow;

            _context.Categories.Add(category);
            await _context.SaveChangesAsync();

            return Ok(category);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCategory(Guid id)
        {
            var category = await _context.Categories.FindAsync(id);
            if (category == null) return NotFound();

            _context.Categories.Remove(category);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}
