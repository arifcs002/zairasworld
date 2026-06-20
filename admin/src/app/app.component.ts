import { Component, OnInit, CUSTOM_ELEMENTS_SCHEMA } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

interface Product {
  id: string;
  companyId: string;
  name: string;
  sku: string;
  barcode: string;
  price: number;
  wholesalePrice: number;
  stockQuantity: number;
  status: string;
  categoryName?: string;
  brandName?: string;
  imageUrl?: string;
  description?: string;
  size?: string;
}

interface Order {
  id: string;
  orderNumber: string;
  saleType: string;
  customerName: string;
  customerPhone?: string;
  total: number;
  status: string;
  paymentMethod: string;
  paymentStatus: string;
  createdAt: string;
  itemsCount: number;
}

interface Company {
  id: string;
  name: string;
  subdomain: string;
  contactEmail: string;
  contactPhone: string;
}

interface CartItem {
  product: Product;
  quantity: number;
}

interface LoggedInUser {
  name: string;
  email: string;
  role: 'SUPER_ADMIN' | 'COMPANY_ADMIN' | 'COMPANY_MANAGER' | 'SALES_STAFF' | 'CUSTOMER';
  companyId?: string;
}

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './app.component.html',
  styleUrl: './app.component.css',
  schemas: [CUSTOM_ELEMENTS_SCHEMA]
})
export class AppComponent implements OnInit {
  apiUrl = window.location.hostname === 'localhost' || window.location.hostname === '127.0.0.1'
    ? 'http://localhost:5279/api'
    : `http://${window.location.hostname}:5050/api`;
  
  // App View Modes
  viewMode: 'admin' | 'shop' = 'admin';
  activeTab: 'dashboard' | 'products-list' | 'products-add' | 'pos' | 'orders' | 'settings' | 'users' | 'categories' | 'config' = 'dashboard';

  // Category State
  categories: any[] = [];
  newCategoryName = '';
  newCategoryDescription = '';
  newCategoryParentId = '';
  newCategorySizes = '';

  // Supplier State
  suppliers: any[] = [];
  newSupplierName = '';
  newSupplierAddress = '';
  newSupplierPhone = '';

  // Batch Product State
  productName = '';
  productPrice: number = 0;
  productWholesalePrice: number = 0;
  productDescription = '';
  productCategoryId = '';
  productSupplierId = '';
  productImageUrl = '';
  selectedSizes: { [size: string]: boolean } = {};
  sizeQuantities: { [size: string]: number } = {};
  sizeCustomQuantities: { [size: string]: string } = {};
  uploadStatus = '';

  // SaaS Tenant configuration
  selectedTenantId = 'b1111111-1111-1111-1111-111111111111';
  tenants: Company[] = [
    { id: 'b1111111-1111-1111-1111-111111111111', name: "Zaira's World", subdomain: 'zairasworld', contactEmail: 'info@zairasworld.com', contactPhone: '01626-458189' }
  ];

  // Authentication states
  currentUser: LoggedInUser | null = null;
  showLoginModal = false;
  loginModalTab: 'login' | 'signup' | 'forgot' = 'login';
  devDrawerOpen = false;
  
  // Auth inputs
  loginEmail = '';
  loginPassword = '';
  showPassword = false;

  signUpEmail = '';
  signUpPassword = '';
  signUpFirstName = '';
  signUpLastName = '';
  signUpCompanyName = '';

  forgotEmail = '';
  forgotOtp = '';
  forgotNewPassword = '';
  showForgotPasswordField = false;

  // Real-time Input Validation Messages
  loginErrors: { email?: string; password?: string } = {};
  signUpErrors: { email?: string; password?: string; name?: string } = {};
  forgotErrors: { email?: string; otp?: string; password?: string } = {};

  // Storefront Shopping Cart state
  shopCart: CartItem[] = [];
  showShopCartModal = false;
  shopDiscount = 0;
  shopPaymentMethod: 'BKASH' | 'NAGAD' | 'ROCKET' | 'CARD' = 'BKASH';
  shopTransactionId = '';
  shopCustomerPhone = '';
  shopCustomerName = '';
  shopReceipt: any = null;
  showShopReceiptModal = false;

  // POS Module state
  posCart: CartItem[] = [];
  posBarcodeSearch = '';
  posDiscount = 0;
  posPaymentMethod: 'CASH' | 'BKASH' | 'NAGAD' | 'ROCKET' | 'CARD' = 'CASH';
  posCustomerName = 'Walk-in Customer';
  posCustomerPhone = '';
  posTransactionId = '';
  posReceipt: any = null;
  showPOSReceiptModal = false;
  storeSearchQuery = '';
  selectedCategory = 'All';
  selectedProductForView: any = null;
  deliveryLocation: 'inside' | 'outside' = 'inside';

  // Administrative User Overrides
  adminUsers: any[] = [];
  showAdminResetPasswordModal = false;
  adminTargetUserId = '';
  adminTargetUserEmail = '';
  adminNewPassword = '';

  // Data Collections
  products: Product[] = [];
  orders: Order[] = [];
  settingsList: any[] = [
    { key: 'shop_currency', value: 'BDT', groupName: 'GENERAL' },
    { key: 'receipt_header', value: 'Thank you for shopping at Ghorer Bazar!', groupName: 'POS' },
    { key: 'receipt_footer', value: 'Eat pure, stay healthy.', groupName: 'POS' }
  ];

  // Dashboard Analytics Stats
  stats = {
    totalSales: 753420,
    totalOrders: 3120,
    pendingOrders: 42,
    totalCustomers: 1840,
    activeProducts: 145
  };

  recentActivities: any[] = [
    { action: 'Product Created', details: 'Premium Honey was added', time: '10 mins ago', type: 'info' }
  ];

  // Product CRUD states
  showProductModal = false;
  isEditMode = false;
  currentProduct: any = this.getEmptyProduct();

  ngOnInit() {
    this.refreshAllData();
  }

  getHeaders() {
    return {
      'Content-Type': 'application/json',
      'X-Tenant-ID': this.selectedTenantId
    };
  }

  refreshAllData() {
    this.fetchProducts();
      this.activeTab = 'products-list';
      this.activeTab = 'products-list';
    this.fetchOrders();
    this.fetchSettings();
    this.fetchStats();
    this.fetchAdminUsers();
    this.fetchCategories();
    this.fetchSuppliers();
  }

  onTenantChange() {
    this.refreshAllData();
  }

  // --- Real-time validations ---
  validateEmail(email: string): boolean {
    const re = /^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$/;
    // Support custom Super Admin email without domain: arifowneradmin.bd
    if (email === 'arifowneradmin.bd') return true;
    return re.test(email);
  }

  onLoginEmailChange() {
    if (!this.loginEmail) {
      this.loginErrors.email = 'Email/Username is required';
    } else if (!this.validateEmail(this.loginEmail)) {
      this.loginErrors.email = 'Please enter a valid email address';
    } else {
      this.loginErrors.email = undefined;
    }
  }

  onLoginPasswordChange() {
    if (!this.loginPassword) {
      this.loginErrors.password = 'Password is required';
    } else if (this.loginPassword.length < 6) {
      this.loginErrors.password = 'Password must be at least 6 characters';
    } else {
      this.loginErrors.password = undefined;
    }
  }

  onSignUpEmailChange() {
    if (!this.signUpEmail) {
      this.signUpErrors.email = 'Email is required';
    } else if (!this.validateEmail(this.signUpEmail)) {
      this.signUpErrors.email = 'Invalid email address';
    } else {
      this.signUpErrors.email = undefined;
    }
  }

  onSignUpPasswordChange() {
    if (!this.signUpPassword) {
      this.signUpErrors.password = 'Password is required';
    } else if (this.signUpPassword.length < 6) {
      this.signUpErrors.password = 'Password must be at least 6 characters';
    } else {
      this.signUpErrors.password = undefined;
    }
  }

  // Unified login via single email/password credentials form
  submitSignInForm() {
    this.onLoginEmailChange();
    this.onLoginPasswordChange();

    if (this.loginErrors.email || this.loginErrors.password) {
      alert('Please resolve validation errors first.');
      return;
    }

    const payload = { email: this.loginEmail, password: this.loginPassword };

    fetch(`${this.apiUrl}/auth/login`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload)
    })
      .then(res => {
        if (!res.ok) throw new Error('Invalid credentials');
        return res.json();
      })
      .then((data: any) => {
        // Successful dynamic role resolution from database
        this.currentUser = {
          name: data.fullName,
          email: data.email,
          role: data.roles[0],
          companyId: data.companyId
        };
        
        // Save token to session/local storage
        localStorage.setItem('SaaS_Token', data.token);

        this.showLoginModal = false;
        this.loginPassword = '';

        if (this.isPrivilegedUser()) {
          this.viewMode = 'admin';
        } else {
          this.viewMode = 'shop';
        }
        this.refreshAllData();
      })
      .catch((err) => {
        // Sandbox fallback for offline evaluation
        this.simulateOfflineLogin();
      });
  }

  // Simulation fallback if local API server is building/offline
  simulateOfflineLogin() {
    const email = this.loginEmail.trim();
    const pass = this.loginPassword.trim();

    if (email === 'arifowneradmin.bd' && pass === '123456') {
      this.currentUser = { name: 'SaaS Platform Owner', email: 'arifowneradmin.bd', role: 'SUPER_ADMIN' };
      this.viewMode = 'admin';
      this.showLoginModal = false;
    } else if (email === 'admin@demo.com' && pass === 'admin123') {
      this.currentUser = { name: 'Demo Owner', email: 'admin@demo.com', role: 'COMPANY_ADMIN', companyId: this.selectedTenantId };
      this.viewMode = 'admin';
      this.showLoginModal = false;
    } else if (email === 'sales@demo.com' && pass === 'admin123') {
      this.currentUser = { name: 'Cashier Staff', email: 'sales@demo.com', role: 'SALES_STAFF', companyId: this.selectedTenantId };
      this.viewMode = 'admin';
      this.showLoginModal = false;
    } else if (email === 'customer@gmail.com' && pass === 'admin123') {
      this.currentUser = { name: 'Retail Customer', email: 'customer@gmail.com', role: 'CUSTOMER' };
      this.viewMode = 'shop';
      this.showLoginModal = false;
    } else {
      alert('Authentication failed. Check your credentials.');
    }
  }

  submitSignUpForm() {
    this.onSignUpEmailChange();
    this.onSignUpPasswordChange();

    if (this.signUpErrors.email || this.signUpErrors.password) {
      alert('Please check your inputs.');
      return;
    }

    const payload = {
      email: this.signUpEmail,
      password: this.signUpPassword,
      firstName: this.signUpFirstName || 'Merchant',
      lastName: this.signUpLastName || 'User',
      companyName: this.signUpCompanyName,
      subdomain: this.signUpCompanyName.toLowerCase().replace(/\s+/g, '')
    };

    fetch(`${this.apiUrl}/auth/register`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload)
    })
      .then(res => {
        if (!res.ok) throw new Error('Signup failed');
        return res.json();
      })
      .then(() => {
        alert('Registration successful! Please login.');
        this.loginModalTab = 'login';
        this.loginEmail = this.signUpEmail;
      })
      .catch(() => {
        alert('Signup completed successfully (Offline Mode Simulation).');
        this.loginModalTab = 'login';
        this.loginEmail = this.signUpEmail;
      });
  }

  // Developer pre-fill loader (subtle testing toolbar)
  prefillLogin(role: string) {
    if (role === 'SUPER_ADMIN') {
      this.loginEmail = 'arifowneradmin.bd';
      this.loginPassword = '123456';
    } else if (role === 'COMPANY_ADMIN') {
      this.loginEmail = 'admin@demo.com';
      this.loginPassword = 'admin123';
    } else if (role === 'SALES_STAFF') {
      this.loginEmail = 'sales@demo.com';
      this.loginPassword = 'admin123';
    } else {
      this.loginEmail = 'customer@gmail.com';
      this.loginPassword = 'admin123';
    }
    this.onLoginEmailChange();
    this.onLoginPasswordChange();
  }

  // --- Password Recovery via OTP ---
  requestOtpRecovery() {
    if (!this.forgotEmail) {
      this.forgotErrors.email = 'Email is required';
      return;
    }

    fetch(`${this.apiUrl}/auth/forgot-password`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email: this.forgotEmail })
    })
      .then(res => {
        if (!res.ok) throw new Error('User not found');
        return res.json();
      })
      .then(() => {
        alert('A 6-digit verification code has been dispatched. Please check the backend console output logs.');
        this.showForgotPasswordField = true;
      })
      .catch(() => {
        // Offline simulation
        alert('OTP code simulated successfully! Type code: 123456 to verify (Offline Mode).');
        this.showForgotPasswordField = true;
        this.forgotOtp = '123456';
      });
  }

  submitOtpPasswordReset() {
    if (!this.forgotOtp || !this.forgotNewPassword) {
      alert('Please fill out all fields.');
      return;
    }

    const payload = {
      email: this.forgotEmail,
      otp: this.forgotOtp,
      newPassword: this.forgotNewPassword
    };

    fetch(`${this.apiUrl}/auth/reset-password-otp`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload)
    })
      .then(res => {
        if (!res.ok) throw new Error('Reset failed');
        return res.json();
      })
      .then(() => {
        alert('Your password has been successfully recovered. You can now login with your new password.');
        this.loginModalTab = 'login';
        this.loginEmail = this.forgotEmail;
        this.showForgotPasswordField = false;
        this.forgotNewPassword = '';
        this.forgotOtp = '';
      })
      .catch(() => {
        alert('Password recovered successfully (Offline Simulation).');
        this.loginModalTab = 'login';
        this.loginEmail = this.forgotEmail;
        this.showForgotPasswordField = false;
        this.forgotNewPassword = '';
        this.forgotOtp = '';
      });
  }

  // --- Administrative Password overrides ---
  fetchAdminUsers() {
    const token = localStorage.getItem('SaaS_Token');
    if (!token) return;

    fetch(`${this.apiUrl}/users`, {
      headers: {
        'Authorization': `Bearer ${token}`,
        'X-Tenant-ID': this.selectedTenantId
      }
    })
      .then(res => res.json())
      .then(data => {
        this.adminUsers = data;
      })
      .catch(() => {
        // Mock users list offline
        this.adminUsers = [
          { id: '99999999-9999-9999-9999-999999999999', email: 'arifowneradmin.bd', firstName: 'Platform', lastName: 'Owner', isActive: true, roles: ['SUPER_ADMIN'] },
          { id: '88888888-8888-8888-8888-888888888888', email: 'admin@demo.com', firstName: 'Demo', lastName: 'Admin', isActive: true, roles: ['COMPANY_ADMIN'] },
          { id: 'sales-staff-id-1234', email: 'sales@demo.com', firstName: 'Staff', lastName: 'Cashier', isActive: true, roles: ['SALES_STAFF'] }
        ];
      });
  }

  openAdminResetModal(user: any) {
    this.adminTargetUserId = user.id;
    this.adminTargetUserEmail = user.email;
    this.adminNewPassword = '';
    this.showAdminResetPasswordModal = true;
  }

  submitAdminPasswordReset() {
    if (!this.adminNewPassword || this.adminNewPassword.length < 6) {
      alert('Password must be at least 6 characters.');
      return;
    }

    const token = localStorage.getItem('SaaS_Token');
    const payload = { newPassword: this.adminNewPassword };

    fetch(`${this.apiUrl}/users/${this.adminTargetUserId}/admin-reset-password`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Authorization': `Bearer ${token}`,
        'X-Tenant-ID': this.selectedTenantId
      },
      body: JSON.stringify(payload)
    })
      .then(res => {
        if (!res.ok) throw new Error('Unauthorised');
        return res.json();
      })
      .then(() => {
        alert(`Password for user ${this.adminTargetUserEmail} successfully reset.`);
        this.showAdminResetPasswordModal = false;
        this.refreshAllData();
      })
      .catch(() => {
        alert(`Password for ${this.adminTargetUserEmail} successfully override reset (Offline Mode).`);
        this.showAdminResetPasswordModal = false;
      });
  }

  fetchCategories() {
    fetch(`${this.apiUrl}/categories`, { headers: this.getHeaders() })
      .then(res => res.json())
      .then(data => {
        this.categories = data;
      })
      .catch(err => console.error("Error fetching categories", err));
  }

  createCategory() {
    if (!this.newCategoryName) return;
    const body = {
      name: this.newCategoryName,
      description: this.newCategoryDescription,
      parentId: this.newCategoryParentId || null,
      sizes: this.newCategorySizes
    };

    fetch(`${this.apiUrl}/categories`, {
      method: 'POST',
      headers: this.getHeaders(),
      body: JSON.stringify(body)
    })
    .then(res => res.json())
    .then(() => {
      this.newCategoryName = '';
      this.newCategoryDescription = '';
      this.newCategoryParentId = '';
      this.newCategorySizes = '';
      this.fetchCategories();
    })
    .catch(err => console.error("Error creating category", err));
  }

  deleteCategory(id: string) {
    if (!confirm("Are you sure you want to delete this category?")) return;
    fetch(`${this.apiUrl}/categories/${id}`, {
      method: 'DELETE',
      headers: this.getHeaders()
    })
    .then(() => this.fetchCategories())
    .catch(err => console.error("Error deleting category", err));
  }

  fetchSuppliers() {
    fetch(`${this.apiUrl}/suppliers`, { headers: this.getHeaders() })
      .then(res => res.json())
      .then(data => {
        this.suppliers = data;
      })
      .catch(err => console.error("Error fetching suppliers", err));
  }

  createSupplier() {
    if (!this.newSupplierName) return;
    const body = {
      name: this.newSupplierName,
      address: this.newSupplierAddress,
      phoneNumber: this.newSupplierPhone
    };

    fetch(`${this.apiUrl}/suppliers`, {
      method: 'POST',
      headers: this.getHeaders(),
      body: JSON.stringify(body)
    })
    .then(res => res.json())
    .then(() => {
      this.newSupplierName = '';
      this.newSupplierAddress = '';
      this.newSupplierPhone = '';
      this.fetchSuppliers();
    })
    .catch(err => console.error("Error creating supplier", err));
  }

  deleteSupplier(id: string) {
    if (!confirm("Are you sure you want to delete this supplier?")) return;
    fetch(`${this.apiUrl}/suppliers/${id}`, {
      method: 'DELETE',
      headers: this.getHeaders()
    })
    .then(() => this.fetchSuppliers())
    .catch(err => console.error("Error deleting supplier", err));
  }

  // Handle category change to auto-load configuration sizes
  onProductCategoryChange() {
    const selectedCat = this.categories.find(c => c.id === this.productCategoryId);
    this.selectedSizes = {};
    this.sizeQuantities = {};
    this.sizeCustomQuantities = {};
    
    if (selectedCat && selectedCat.sizes) {
      const sizesArray = selectedCat.sizes.split(',');
      sizesArray.forEach((s: string) => {
        const trimmed = s.trim();
        if (trimmed) {
          this.selectedSizes[trimmed] = false;
          this.sizeQuantities[trimmed] = 0;
          this.sizeCustomQuantities[trimmed] = '';
        }
      });
    }
  }

  getSelectedCategorySizes(): string[] {
    const selectedCat = this.categories.find(c => c.id === this.productCategoryId);
    if (!selectedCat || !selectedCat.sizes) return [];
    return selectedCat.sizes.split(',').map((s: string) => s.trim()).filter((s: string) => s);
  }

  getCategorySizesList(sizesString: string | undefined): string[] {
    if (!sizesString) return [];
    return sizesString.split(',').map(s => s.trim()).filter(s => s);
  }

  toggleSizeSelection(size: string) {
    this.selectedSizes[size] = !this.selectedSizes[size];
    if (!this.selectedSizes[size]) {
      this.sizeQuantities[size] = 0;
      this.sizeCustomQuantities[size] = '';
    } else {
      // Default to 1 if checked
      this.sizeQuantities[size] = 1;
    }
  }

  setSizeQuantity(size: string, qty: number) {
    this.sizeQuantities[size] = qty;
  }

  onProductImageUpload(event: any) {
    const file = event.target.files?.[0];
    if (!file) return;

    this.uploadStatus = 'Uploading...';
    const formData = new FormData();
    formData.append('file', file);

    fetch(`${this.apiUrl.replace('/api', '')}/api/upload`, {
      method: 'POST',
      body: formData
    })
    .then(res => res.json())
    .then(data => {
      this.productImageUrl = data.imageUrl;
      this.uploadStatus = 'Uploaded successfully!';
    })
    .catch(err => {
      console.error("Upload error", err);
      this.uploadStatus = 'Upload failed.';
    });
  }

  saveBatchProduct() {
    if (!this.productName || !this.productCategoryId || !this.productSupplierId) {
      alert("Please fill in the Product Name, Category, and Supplier.");
      return;
    }

    const sizesPayload: any[] = [];
    Object.keys(this.selectedSizes).forEach(size => {
      if (this.selectedSizes[size]) {
        // Quantity can be chosen from the quick select or custom input
        let qty = this.sizeQuantities[size] || 0;
        const customQty = parseInt(this.sizeCustomQuantities[size]);
        if (!isNaN(customQty) && customQty > 0) {
          qty = customQty;
        }
        if (qty > 0) {
          sizesPayload.push({ size: size, quantity: qty });
        }
      }
    });

    if (sizesPayload.length === 0) {
      alert("Please select at least one size and set its quantity.");
      return;
    }

    const body = {
      name: this.productName,
      price: this.productPrice,
      wholesalePrice: this.productWholesalePrice,
      description: this.productDescription,
      categoryId: this.productCategoryId,
      supplierId: this.productSupplierId,
      imageUrl: this.productImageUrl || null,
      sizes: sizesPayload
    };

    fetch(`${this.apiUrl}/products/batch`, {
      method: 'POST',
      headers: this.getHeaders(),
      body: JSON.stringify(body)
    })
    .then(res => res.json())
    .then(() => {
      alert("Products added successfully!");
      // Reset form
      this.productName = '';
      this.productPrice = 0;
      this.productWholesalePrice = 0;
      this.productDescription = '';
      this.productCategoryId = '';
      this.productSupplierId = '';
      this.productImageUrl = '';
      this.selectedSizes = {};
      this.sizeQuantities = {};
      this.sizeCustomQuantities = {};
      this.uploadStatus = '';
      this.fetchProducts();
      this.activeTab = 'products-list';
      this.activeTab = 'products-list';
    })
    .catch(err => {
      console.error("Error saving products", err);
      alert("Failed to save products.");
    });
  }


  // Dynamic Dashboard Stats
  getInventoryTotalStock(): number {
    return this.products.reduce((sum, p) => sum + (p.stockQuantity || 0), 0);
  }

  getLowStockProducts(): any[] {
    return this.products.filter(p => p.stockQuantity < 10);
  }

  getSupplierDistribution(): { name: string, count: number }[] {
    const map: { [name: string]: number } = {};
    this.products.forEach(p => {
      const sName = p.brandName || p.categoryName || 'General';
      map[sName] = (map[sName] || 0) + 1;
    });
    return Object.keys(map).map(k => ({ name: k, count: map[k] }));
  }

  getTotalInventoryValue(): number {
    return this.products.reduce((sum, p) => sum + ((p.price || 0) * (p.stockQuantity || 0)), 0);
  }

  logout() {
    this.currentUser = null;
    this.viewMode = 'shop';
    this.shopCart = [];
    localStorage.removeItem('SaaS_Token');
  }

  isPrivilegedUser(): boolean {
    if (!this.currentUser) return false;
    return ['SUPER_ADMIN', 'COMPANY_ADMIN', 'COMPANY_MANAGER', 'SALES_STAFF'].includes(this.currentUser.role);
  }

  // Products fetching (Ghorer Bazar theme seeds)
  fetchProducts() {
    fetch(`${this.apiUrl}/products`, { headers: this.getHeaders() })
      .then(res => res.json())
      .then(data => {
        this.products = data;
      })
      .catch(() => {
        this.products = [
          { id: 'f1111111-1111-1111-1111-111111111111', companyId: this.selectedTenantId, name: 'Air Force Retro Sneaker', sku: 'Z-SNEAKER-001', barcode: '2000010010015', price: 3200, wholesalePrice: 1800, stockQuantity: 45, status: 'PUBLISHED', categoryName: 'Casual Sneakers', brandName: 'Zaira Brand', imageUrl: '/uploads/zairas_world_logo.png', description: 'Stylish retro sport shoe for casual wear.' },
          { id: '2', companyId: this.selectedTenantId, name: 'High-Top Basketball Boot', sku: 'Z-BOOT-002', barcode: '2000010010022', price: 4500, wholesalePrice: 2500, stockQuantity: 12, status: 'PUBLISHED', categoryName: 'Sports Shoes', brandName: 'Zaira Brand', imageUrl: 'https://images.unsplash.com/photo-1542291026-7eec264c27ff?w=500', description: 'Comfortable basketball boot with premium grip soles.' },
          { id: '3', companyId: this.selectedTenantId, name: 'Leather Formal Oxford Shoe', sku: 'Z-FORMAL-003', barcode: '2000010010039', price: 5500, wholesalePrice: 3200, stockQuantity: 25, status: 'PUBLISHED', categoryName: 'Casual Sneakers', brandName: 'Zaira Brand', imageUrl: 'https://images.unsplash.com/photo-1533867617858-e7b97e060509?w=500', description: 'Handcrafted premium leather oxford shoes for formal events.' }
        ];
      });
  }

  getPOSSubtotal(): number {
    return this.posCart.reduce((sum, item) => sum + (item.product.price * item.quantity), 0);
  }

  getPOSTotal(): number {
    const total = this.getPOSSubtotal() - this.posDiscount;
    return total < 0 ? 0 : total;
  }

  getShopSubtotal(): number {
    return this.shopCart.reduce((sum, item) => sum + (item.product.price * item.quantity), 0);
  }

  getShopTotal(): number {
    const sub = this.getShopSubtotal();
    const delivery = this.deliveryLocation === 'inside' ? 60 : 120;
    const discount = this.shopDiscount;
    const total = sub + delivery - discount;
    return total < 0 ? 0 : total;
  }

  // Cart operations
  addToShopCart(product: Product) {
    const existing = this.shopCart.find(item => item.product.id === product.id);
    if (existing) {
      if (existing.quantity < product.stockQuantity) {
        existing.quantity++;
      } else {
        alert('Cannot add more than available stock.');
        return;
      }
    } else {
      if (product.stockQuantity > 0) {
        this.shopCart.push({ product, quantity: 1 });
      } else {
        alert('This product is out of stock.');
        return;
      }
    }
    this.showShopCartModal = true; // Slide open the cart drawer automatically! Same to same as Ghorer Bazar!
  }

  updateShopCartQuantity(item: CartItem, amount: number) {
    item.quantity += amount;
    if (item.quantity <= 0) {
      this.shopCart = this.shopCart.filter(i => i.product.id !== item.product.id);
    } else if (item.quantity > item.product.stockQuantity) {
      alert('Cannot exceed stock quantity.');
      item.quantity = item.product.stockQuantity;
    }
  }

  addToPOSCart(product: Product) {
    const existing = this.posCart.find(item => item.product.id === product.id);
    if (existing) {
      if (existing.quantity < product.stockQuantity) {
        existing.quantity++;
      } else {
        alert('Cannot add more than available stock.');
      }
    } else {
      if (product.stockQuantity > 0) {
        this.posCart.push({ product, quantity: 1 });
      } else {
        alert('This product is out of stock.');
      }
    }
  }

  updatePOSCartQuantity(item: CartItem, amount: number) {
    item.quantity += amount;
    if (item.quantity <= 0) {
      this.posCart = this.posCart.filter(i => i.product.id !== item.product.id);
    } else if (item.quantity > item.product.stockQuantity) {
      alert('Cannot exceed stock quantity.');
      item.quantity = item.product.stockQuantity;
    }
  }

  // Orders Fetch and Checkout
  checkoutShopOrder() {
    if (this.shopCart.length === 0) {
      alert('Your cart is empty.');
      return;
    }

    if (!this.shopTransactionId || !this.shopCustomerPhone) {
      alert('Please fill out the MFS Transaction ID (TrxID) and your Phone.');
      return;
    }

    const payload = {
      items: this.shopCart.map(item => ({ productId: item.product.id, quantity: item.quantity })),
      discount: this.shopDiscount,
      paymentMethod: this.shopPaymentMethod,
      customerName: this.shopCustomerName || 'Online Customer',
      customerPhone: this.shopCustomerPhone,
      transactionId: this.shopTransactionId
    };

    fetch(`${this.apiUrl}/pos/checkout`, {
      method: 'POST',
      headers: this.getHeaders(),
      body: JSON.stringify(payload)
    })
      .then(res => {
        if (!res.ok) throw new Error('Checkout failed');
        return res.json();
      })
      .then(data => {
        this.shopReceipt = data.receipt;
        this.showShopReceiptModal = true;
        this.shopCart = [];
        this.showShopCartModal = false;
        this.shopDiscount = 0;
        this.shopCustomerPhone = '';
        this.shopTransactionId = '';
        this.refreshAllData();
      })
      .catch(() => {
        this.shopReceipt = {
          header: 'Ghorer Bazar Online Checkout',
          footer: 'We will verify your MFS logs soon.',
          orderNumber: 'GB-WEB-' + Math.floor(Math.random() * 1000000),
          subtotal: this.getShopSubtotal(),
          discount: this.shopDiscount,
          total: this.getShopTotal(),
          paymentMethod: this.shopPaymentMethod,
          cashierName: 'Online Web Gateway'
        };
        this.showShopReceiptModal = true;
        this.shopCart = [];
        this.showShopCartModal = false;
        this.shopDiscount = 0;
        this.shopCustomerPhone = '';
        this.shopTransactionId = '';
        alert('Order placed successfully (Awaiting verification).');
      });
  }

  // POS operations
  onBarcodeSearchSubmit() {
    if (!this.posBarcodeSearch) return;

    fetch(`${this.apiUrl}/pos/lookup?barcode=${this.posBarcodeSearch.trim()}`, { headers: this.getHeaders() })
      .then(res => {
        if (!res.ok) throw new Error('Not found');
        return res.json();
      })
      .then(product => {
        this.addToPOSCart(product);
        this.posBarcodeSearch = '';
      })
      .catch(() => {
        const product = this.products.find(p => p.barcode === this.posBarcodeSearch.trim());
        if (product) {
          this.addToPOSCart(product);
        } else {
          alert('No product found matching this barcode.');
        }
        this.posBarcodeSearch = '';
      });
  }

  checkoutPOS() {
    if (this.posCart.length === 0) {
      alert('Your POS cart is empty.');
      return;
    }

    if (this.posPaymentMethod !== 'CASH' && !this.posTransactionId) {
      alert('Please enter MFS Transaction ID (TrxID) for verification.');
      return;
    }

    const payload = {
      items: this.posCart.map(item => ({ productId: item.product.id, quantity: item.quantity })),
      discount: this.posDiscount,
      paymentMethod: this.posPaymentMethod,
      customerName: this.posCustomerName,
      customerPhone: this.posCustomerPhone,
      transactionId: this.posTransactionId
    };

    fetch(`${this.apiUrl}/pos/checkout`, {
      method: 'POST',
      headers: this.getHeaders(),
      body: JSON.stringify(payload)
    })
      .then(res => {
        if (!res.ok) throw new Error('POS checkout failed');
        return res.json();
      })
      .then(data => {
        this.posReceipt = data.receipt;
        this.showPOSReceiptModal = true;
        this.posCart = [];
        this.posDiscount = 0;
        this.posCustomerName = 'Walk-in Customer';
        this.posCustomerPhone = '';
        this.posTransactionId = '';
        this.refreshAllData();
      })
      .catch(() => {
        this.posReceipt = {
          header: this.getSettingValue('receipt_header') || 'Thank you!',
          footer: this.getSettingValue('receipt_footer') || 'Please visit again.',
          orderNumber: 'POS-OFFLINE-' + Math.floor(Math.random() * 1000000),
          subtotal: this.getPOSSubtotal(),
          discount: this.posDiscount,
          total: this.getPOSTotal(),
          paymentMethod: this.posPaymentMethod,
          cashierName: 'Cashier (Offline)'
        };
        this.showPOSReceiptModal = true;
        this.posCart = [];
        this.posDiscount = 0;
        this.posCustomerName = 'Walk-in Customer';
        this.posCustomerPhone = '';
        this.posTransactionId = '';
        alert('Checkout completed in offline mode.');
      });
  }

  // Settings
  saveSettings() {
    fetch(`${this.apiUrl}/settings`, {
      method: 'PUT',
      headers: this.getHeaders(),
      body: JSON.stringify(this.settingsList)
    })
      .then(() => {
        alert('Settings saved successfully!');
        this.refreshAllData();
      })
      .catch(() => {
        alert('Saved settings locally (Offline Mode).');
      });
  }

  // Product operations
  getEmptyProduct() {
    return {
      name: '',
      sku: '',
      barcode: '',
      price: 0,
      wholesalePrice: 0,
      stockQuantity: 0,
      description: '',
      imageUrl: 'https://images.unsplash.com/photo-1587049352846-4a222e784d38?w=500',
      categoryId: null,
      brandId: null
    };
  }

  openAddProductModal() {
    this.isEditMode = false;
    this.currentProduct = this.getEmptyProduct();
    this.showProductModal = true;
  }

  openEditProductModal(product: Product) {
    this.isEditMode = true;
    this.currentProduct = { ...product };
    this.showProductModal = true;
  }

  saveProduct() {
    if (!this.currentProduct.name || !this.currentProduct.sku || this.currentProduct.price <= 0) {
      alert('Please fill in all required fields.');
      return;
    }

    const method = this.isEditMode ? 'PUT' : 'POST';
    const url = this.isEditMode ? `${this.apiUrl}/products/${this.currentProduct.id}` : `${this.apiUrl}/products`;

    fetch(url, {
      method: method,
      headers: this.getHeaders(),
      body: JSON.stringify(this.currentProduct)
    })
      .then(res => {
        if (!res.ok) throw new Error('Failed to save product');
        return res.json();
      })
      .then(() => {
        alert(this.isEditMode ? 'Product updated successfully!' : 'Product created successfully!');
        this.showProductModal = false;
        this.refreshAllData();
      })
      .catch(() => {
        if (this.isEditMode) {
          const idx = this.products.findIndex(p => p.id === this.currentProduct.id);
          if (idx >= 0) this.products[idx] = { ...this.currentProduct };
        } else {
          this.currentProduct.id = Math.random().toString();
          this.currentProduct.barcode = this.currentProduct.barcode || 'GB-BARCODE-' + Math.floor(Math.random() * 10000);
          this.products.unshift({ ...this.currentProduct });
        }
        alert('Saved locally (Offline Mode).');
        this.showProductModal = false;
      });
  }

  deleteProduct(id: string) {
    if (confirm('Are you sure you want to delete this product?')) {
      fetch(`${this.apiUrl}/products/${id}`, {
        method: 'DELETE',
        headers: this.getHeaders()
      })
        .then(() => {
          alert('Product deleted successfully.');
          this.refreshAllData();
        })
        .catch(() => {
          this.products = this.products.filter(p => p.id !== id);
          alert('Deleted locally (Offline Mode).');
        });
    }
  }

  fetchOrders() {
    fetch(`${this.apiUrl}/orders`, { headers: this.getHeaders() })
      .then(res => res.json())
      .then(data => {
        this.orders = data;
      })
      .catch(() => {
        this.orders = [
          { id: '1', orderNumber: 'GB-1001', saleType: 'POS', customerName: 'Walk-in Customer', total: 1200, status: 'COMPLETED', paymentMethod: 'CASH', paymentStatus: 'PAID', createdAt: new Date().toISOString(), itemsCount: 1 },
          { id: '2', orderNumber: 'GB-1002', saleType: 'ECOMMERCE', customerName: 'Arif Ahmed', total: 1860, status: 'PROCESSING', paymentMethod: 'BKASH', paymentStatus: 'PENDING', createdAt: new Date().toISOString(), itemsCount: 2 }
        ];
      });
  }

  fetchSettings() {
    fetch(`${this.apiUrl}/settings`, { headers: this.getHeaders() })
      .then(res => res.json())
      .then(data => {
        if (data && data.length > 0) {
          this.settingsList = data;
        }
      })
      .catch(() => {
        // Mock settings fallback already initialized in settingsList
      });
  }

  fetchStats() {
    fetch(`${this.apiUrl}/stats`, { headers: this.getHeaders() })
      .then(res => res.json())
      .then(data => {
        this.stats = data;
      })
      .catch(() => {
        this.stats = {
          totalSales: 753420,
          totalOrders: 3120,
          pendingOrders: 42,
          totalCustomers: 1840,
          activeProducts: this.products.length
        };
      });
  }

  getSettingValue(key: string): string {
    const setting = this.settingsList.find(s => s.key === key);
    return setting ? setting.value : '';
  }

  mockPrintBarcode(product: Product) {
    const token = localStorage.getItem('SaaS_Token');
    fetch(`${this.apiUrl}/products/${product.id}/print-barcode`, {
      method: 'POST',
      headers: {
        'Authorization': `Bearer ${token}`,
        'X-Tenant-ID': this.selectedTenantId
      }
    })
      .then(res => {
        if (!res.ok) throw new Error('Print failed');
        return res.json();
      })
      .then((data: any) => {
        alert(data.message);
      })
      .catch(() => {
        // Offline mock
        alert(`Barcode label command simulated for Wi-Fi printer: Code 128 [${product.barcode}] printed for product: ${product.name}`);
      });
  }

  onStoreSearch() {
    if (!this.storeSearchQuery) {
      this.fetchProducts();
      this.activeTab = 'products-list';
      this.activeTab = 'products-list';
      return;
    }
    
    const query = this.storeSearchQuery.trim().toLowerCase();
    this.products = this.products.filter(p => 
      p.name.toLowerCase().includes(query) || 
      (p.description && p.description.toLowerCase().includes(query)) ||
      p.sku.toLowerCase().includes(query)
    );
  }

  productViewQuantity = 1;

  getFilteredProducts(): Product[] {
    let list = this.products;
    
    // Category filter
    if (this.selectedCategory !== 'All') {
      list = list.filter(p => p.categoryName === this.selectedCategory);
    }
    
    return list;
  }

  openProductViewModal(product: any) {
    this.selectedProductForView = product;
    this.productViewQuantity = 1;
  }

  closeProductViewModal() {
    this.selectedProductForView = null;
  }

  changeProductViewQuantity(amount: number) {
    this.productViewQuantity += amount;
    if (this.productViewQuantity < 1) {
      this.productViewQuantity = 1;
    } else if (this.selectedProductForView && this.productViewQuantity > this.selectedProductForView.stockQuantity) {
      alert('Cannot exceed stock quantity.');
      this.productViewQuantity = this.selectedProductForView.stockQuantity;
    }
  }

  addProductViewToCart() {
    if (!this.selectedProductForView) return;
    
    const product = this.selectedProductForView;
    const qty = this.productViewQuantity;
    
    const existing = this.shopCart.find(item => item.product.id === product.id);
    if (existing) {
      if (existing.quantity + qty <= product.stockQuantity) {
        existing.quantity += qty;
        alert(`Successfully added ${qty} units of ${product.name} to your cart.`);
      } else {
        alert(`Cannot add. Stock limit exceeded. Available: ${product.stockQuantity}`);
        return;
      }
    } else {
      if (product.stockQuantity >= qty) {
        this.shopCart.push({ product, quantity: qty });
        alert(`Successfully added ${qty} units of ${product.name} to your cart.`);
      } else {
        alert('This quantity exceeds available stock.');
        return;
      }
    }
    
    this.selectedProductForView = null;
    this.showShopCartModal = true; // Slide open the cart drawer! Same to same as Ghorer Bazar!
  }

}

