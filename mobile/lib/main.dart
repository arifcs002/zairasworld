import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:go_router/go_router.dart';
import 'package:google_fonts/google_fonts.dart';

// State Managers
class AuthState extends ChangeNotifier {
  Map<String, dynamic>? _user;

  Map<String, dynamic>? get user => _user;
  bool get isAuthenticated => _user != null;
  String get role => _user != null ? _user!['role'] : 'GUEST';

  void loginAs(String role) {
    if (role == 'CUSTOMER') {
      _user = {'name': 'General Customer', 'email': 'customer@gmail.com', 'role': 'CUSTOMER'};
    } else if (role == 'SALES_STAFF') {
      _user = {'name': 'Sales Staff', 'email': 'sales@demo.com', 'role': 'SALES_STAFF'};
    } else if (role == 'COMPANY_MANAGER') {
      _user = {'name': 'Store Manager', 'email': 'manager@demo.com', 'role': 'COMPANY_MANAGER'};
    } else if (role == 'COMPANY_ADMIN') {
      _user = {'name': 'Company Admin', 'email': 'admin@demo.com', 'role': 'COMPANY_ADMIN'};
    } else if (role == 'SUPER_ADMIN') {
      _user = {'name': 'SaaS Platform Owner', 'email': 'superadmin@saas.com', 'role': 'SUPER_ADMIN'};
    }
    notifyListeners();
  }

  void logout() {
    _user = null;
    notifyListeners();
  }
}

class TenantState extends ChangeNotifier {
  String _tenantId = "b1111111-1111-1111-1111-111111111111"; // Demo Company ID
  String _tenantName = "SaaS Demo Retailer";

  String get tenantId => _tenantId;
  String get tenantName => _tenantName;

  void switchTenant(String id, String name) {
    _tenantId = id;
    _tenantName = name;
    notifyListeners();
  }
}

class MobilePOSCartState extends ChangeNotifier {
  final List<Map<String, dynamic>> _items = [];
  double _discount = 0.0;
  String _paymentMethod = "CASH";
  String _customerName = "Walk-in Customer";
  String _customerPhone = "";
  String _transactionId = "";

  List<Map<String, dynamic>> get items => _items;
  double get discount => _discount;
  String get paymentMethod => _paymentMethod;
  String get customerName => _customerName;
  String get customerPhone => _customerPhone;
  String get transactionId => _transactionId;

  double get subtotal => _items.fold(0.0, (sum, item) => sum + (item['price'] * item['quantity']));
  double get total => (subtotal - _discount) < 0 ? 0.0 : (subtotal - _discount);

  void addToCart(Map<String, dynamic> product) {
    int index = _items.indexWhere((item) => item['id'] == product['id']);
    if (index >= 0) {
      if (_items[index]['quantity'] < product['stockQuantity']) {
        _items[index]['quantity'] += 1;
      }
    } else {
      if (product['stockQuantity'] > 0) {
        _items.add({
          'id': product['id'],
          'name': product['name'],
          'price': (product['price'] as num).toDouble(),
          'stockQuantity': product['stockQuantity'],
          'quantity': 1,
        });
      }
    }
    notifyListeners();
  }

  void updateQuantity(String id, int quantity) {
    int index = _items.indexWhere((item) => item['id'] == id);
    if (index >= 0) {
      if (quantity <= 0) {
        _items.removeAt(index);
      } else {
        _items[index]['quantity'] = quantity;
      }
      notifyListeners();
    }
  }

  void setCheckoutDetails({
    double? discount,
    String? paymentMethod,
    String? customerName,
    String? customerPhone,
    String? transactionId,
  }) {
    if (discount != null) _discount = discount;
    if (paymentMethod != null) _paymentMethod = paymentMethod;
    if (customerName != null) _customerName = customerName;
    if (customerPhone != null) _customerPhone = customerPhone;
    if (transactionId != null) _transactionId = transactionId;
    notifyListeners();
  }

  void clearCart() {
    _items.clear();
    _discount = 0.0;
    _paymentMethod = "CASH";
    _customerName = "Walk-in Customer";
    _customerPhone = "";
    _transactionId = "";
    notifyListeners();
  }
}

void main() {
  runApp(
    MultiProvider(
      providers: [
        ChangeNotifierProvider(create: (_) => AuthState()),
        ChangeNotifierProvider(create: (_) => TenantState()),
        ChangeNotifierProvider(create: (_) => MobilePOSCartState()),
      ],
      child: const ECommerceApp(),
    ),
  );
}

class ECommerceApp extends StatelessWidget {
  const ECommerceApp({super.key});

  @override
  Widget build(BuildContext context) {
    final GoRouter router = GoRouter(
      initialLocation: '/',
      routes: [
        GoRoute(
          path: '/',
          builder: (context, state) => const MainNavigationScreen(),
        ),
      ],
    );

    return MaterialApp.router(
      title: 'POS SaaS Mobile Client',
      debugShowCheckedModeBanner: false,
      theme: ThemeData(
        useMaterial3: true,
        colorScheme: ColorScheme.fromSeed(
          seedColor: const Color(0xFF0F172A),
          primary: const Color(0xFF2563EB),
          secondary: const Color(0xFF10B981),
          background: const Color(0xFFF8FAFC),
        ),
        textTheme: GoogleFonts.outfitTextTheme(Theme.of(context).textTheme),
      ),
      routerConfig: router,
    );
  }
}

class MainNavigationScreen extends StatefulWidget {
  const MainNavigationScreen({super.key});

  @override
  State<MainNavigationScreen> createState() => _MainNavigationScreenState();
}

class _MainNavigationScreenState extends State<MainNavigationScreen> {
  int _currentIndex = 0;

  // Render screens dynamically based on active login role
  Widget _buildBody(String role) {
    bool isStaff = ['SUPER_ADMIN', 'COMPANY_ADMIN', 'COMPANY_MANAGER', 'SALES_STAFF'].contains(role);
    if (isStaff) {
      if (_currentIndex == 0) return const MobilePOSDashboardScreen();
      if (_currentIndex == 1) return const InventoryManagementScreen();
      return const MobilePOSCheckoutScreen();
    } else {
      if (_currentIndex == 0) return const CustomerStorefrontScreen();
      return const CustomerCartScreen();
    }
  }

  List<NavigationDestination> _buildDestinations(String role) {
    bool isStaff = ['SUPER_ADMIN', 'COMPANY_ADMIN', 'COMPANY_MANAGER', 'SALES_STAFF'].contains(role);
    if (isStaff) {
      return const [
        NavigationDestination(icon: Icon(Icons.print_outlined), selectedIcon: Icon(Icons.print), label: 'POS Register'),
        NavigationDestination(icon: Icon(Icons.inventory_2_outlined), selectedIcon: Icon(Icons.inventory_2), label: 'Inventory'),
        NavigationDestination(icon: Icon(Icons.shopping_cart_checkout_outlined), selectedIcon: Icon(Icons.shopping_cart_checkout), label: 'Checkout'),
      ];
    } else {
      return const [
        NavigationDestination(icon: Icon(Icons.shopping_bag_outlined), selectedIcon: Icon(Icons.shopping_bag), label: 'Storefront'),
        NavigationDestination(icon: Icon(Icons.shopping_cart_outlined), selectedIcon: Icon(Icons.shopping_cart), label: 'My Cart'),
      ];
    }
  }

  @override
  Widget build(BuildContext context) {
    final auth = Provider.of<AuthState>(context);
    final destinations = _buildDestinations(auth.role);

    // Safety index reset if role switches
    if (_currentIndex >= destinations.length) {
      _currentIndex = 0;
    }

    return Scaffold(
      body: _buildBody(auth.role),
      bottomNavigationBar: NavigationBar(
        selectedIndex: _currentIndex,
        onDestinationSelected: (index) {
          setState(() {
            _currentIndex = index;
          });
        },
        destinations: destinations,
      ),
    );
  }
}

// ----------------------------------------------------------------------
// SCREEN: Customer public storefront (visible to guests and customers)
// ----------------------------------------------------------------------
class CustomerStorefrontScreen extends StatelessWidget {
  const CustomerStorefrontScreen({super.key});

  final List<Map<String, dynamic>> _catalog = const [
    {
      'id': 'f1111111-1111-1111-1111-111111111111',
      'name': 'Classic Polo Shirt',
      'sku': 'POLO-CLS-001',
      'barcode': '2000010010015',
      'price': 1200.0,
      'stockQuantity': 100,
    },
    {
      'id': '2',
      'name': 'Eco Leather Belt',
      'sku': 'BELT-ECO-002',
      'barcode': '2000010010022',
      'price': 850.0,
      'stockQuantity': 12,
    }
  ];

  void _showLoginDialog(BuildContext context) {
    showDialog(
      context: context,
      builder: (context) {
        final auth = Provider.of<AuthState>(context, listen: false);
        return AlertDialog(
          title: const Text('Auth Role Simulator'),
          content: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              ListTile(
                title: const Text('Customer (Shop View)'),
                onPressed: () {
                  auth.loginAs('CUSTOMER');
                  Navigator.pop(context);
                },
              ),
              ListTile(
                title: const Text('Sales Staff (POS View)'),
                onPressed: () {
                  auth.loginAs('SALES_STAFF');
                  Navigator.pop(context);
                },
              ),
              ListTile(
                title: const Text('Company Admin (Full View)'),
                onPressed: () {
                  auth.loginAs('COMPANY_ADMIN');
                  Navigator.pop(context);
                },
              ),
            ],
          ),
        );
      },
    );
  }

  @override
  Widget build(BuildContext context) {
    final cart = Provider.of<MobilePOSCartState>(context);
    final auth = Provider.of<AuthState>(context);

    return Scaffold(
      appBar: AppBar(
        title: const Text('Public E-Shop', style: TextStyle(fontWeight: FontWeight.bold)),
        actions: [
          if (!auth.isAuthenticated)
            TextButton(
              onPressed: () => _showLoginDialog(context),
              child: const Text('Login'),
            )
          else
            TextButton(
              onPressed: () => auth.logout(),
              child: Text('Logout (${auth.user!['name']})', style: const TextStyle(color: Colors.red)),
            )
        ],
      ),
      body: Padding(
        padding: const EdgeInsets.all(16.0),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Container(
              width: double.infinity,
              padding: const EdgeInsets.all(20),
              decoration: BoxDecoration(
                gradient: const LinearGradient(colors: [Color(0xFF0F172A), Color(0xFF1E293B)]),
                borderRadius: BorderRadius.circular(20),
              ),
              child: const Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text('E-Commerce Storefront', style: TextStyle(color: Colors.white, fontWeight: FontWeight.bold, fontSize: 18)),
                  SizedBox(height: 6),
                  Text('Shop sustainable clothing & inventory directly connected to backend systems.', style: TextStyle(color: Colors.white60, fontSize: 11)),
                ],
              ),
            ),
            const SizedBox(height: 16),
            const Text('Store Products', style: TextStyle(fontWeight: FontWeight.bold, fontSize: 16)),
            const SizedBox(height: 8),
            Expanded(
              child: GridView.builder(
                gridDelegate: const SliverGridDelegateWithFixedCrossAxisCount(
                  crossAxisCount: 2,
                  childAspectRatio: 0.8,
                  crossAxisSpacing: 12,
                  mainAxisSpacing: 12,
                ),
                itemCount: _catalog.length,
                itemBuilder: (context, index) {
                  final p = _catalog[index];
                  return Card(
                    clipBehavior: Clip.antiAlias,
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Container(height: 80, color: Colors.grey[200], child: const Center(child: Icon(Icons.image_outlined, color: Colors.grey))),
                        Padding(
                          padding: const EdgeInsets.all(8.0),
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Text(p['name'], style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 12), maxLines: 1, overflow: TextOverflow.ellipsis),
                              Text('${p['price']} BDT', style: const TextStyle(color: Colors.blue, fontWeight: FontWeight.bold, fontSize: 11)),
                              const SizedBox(height: 6),
                              ElevatedButton(
                                onPressed: () {
                                  cart.addToCart(p);
                                  ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text('Added ${p['name']} to cart'), duration: const Duration(milliseconds: 600)));
                                },
                                style: ElevatedButton.styleFrom(
                                  minimumSize: const Size(double.infinity, 28),
                                  backgroundColor: Colors.blue,
                                  foregroundColor: Colors.white,
                                  shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(8)),
                                ),
                                child: const Text('Add', style: TextStyle(fontSize: 10)),
                              )
                            ],
                          ),
                        ),
                      ],
                    ),
                  );
                },
              ),
            ),
          ],
        ),
      ),
    );
  }
}

// ----------------------------------------------------------------------
// SCREEN: Customer Cart Checkouts
// ----------------------------------------------------------------------
class CustomerCartScreen extends StatefulWidget {
  const CustomerCartScreen({super.key});

  @override
  State<CustomerCartScreen> createState() => _CustomerCartScreenState();
}

class _CustomerCartScreenState extends State<CustomerCartScreen> {
  final TextEditingController _phoneController = TextEditingController();
  final TextEditingController _trxController = TextEditingController();

  String _mfsProvider = "BKASH";

  void _submitOrder() {
    final cart = Provider.of<MobilePOSCartState>(context, listen: false);

    if (cart.items.isEmpty) return;

    if (_phoneController.text.isEmpty || _trxController.text.isEmpty) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text("TrxID and Phone required for payment verification"), backgroundColor: Colors.red),
      );
      return;
    }

    showDialog(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Order Placed!'),
        content: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const Text('Your payment reference log was created:'),
            const SizedBox(height: 8),
            Text('MFS provider: $_mfsProvider'),
            Text('TrxID: ${_trxController.text}'),
            Text('Order Total: ${cart.total + 60} BDT (with delivery)'),
            const SizedBox(height: 12),
            const Text('We will verify your MFS reference and process shipping.', style: TextStyle(fontSize: 11, color: Colors.grey)),
          ],
        ),
        actions: [
          ElevatedButton(
            onPressed: () {
              cart.clearCart();
              _phoneController.clear();
              _trxController.clear();
              Navigator.pop(context);
            },
            child: const Text('Dismiss'),
          )
        ],
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final cart = Provider.of<MobilePOSCartState>(context);

    return Scaffold(
      appBar: AppBar(title: const Text('Shopping Cart', style: TextStyle(fontWeight: FontWeight.bold))),
      body: Padding(
        padding: const EdgeInsets.all(16.0),
        child: cart.items.isEmpty
            ? Center(
                child: Column(
                  mainAxisAlignment: MainAxisAlignment.center,
                  children: [
                    Icon(Icons.shopping_cart_outlined, size: 48, color: Colors.grey[400]),
                    const SizedBox(height: 8),
                    const Text('Cart is empty'),
                  ],
                ),
              )
            : Column(
                children: [
                  Expanded(
                    child: ListView.builder(
                      itemCount: cart.items.length,
                      itemBuilder: (context, index) {
                        final item = cart.items[index];
                        return Card(
                          margin: const EdgeInsets.only(bottom: 8),
                          child: ListTile(
                            title: Text(item['name'], style: const TextStyle(fontWeight: FontWeight.bold)),
                            subtitle: Text('${item['price']} BDT'),
                            trailing: Text('x ${item['quantity']}'),
                          ),
                        );
                      },
                    ),
                  ),
                  const Divider(),
                  DropdownButtonFormField<String>(
                    value: _mfsProvider,
                    decoration: const InputDecoration(labelText: 'MFS Provider', border: OutlineInputBorder()),
                    items: const [
                      DropdownMenuItem(value: "BKASH", child: Text("bKash")),
                      DropdownMenuItem(value: "NAGAD", child: Text("Nagad")),
                      DropdownMenuItem(value: "ROCKET", child: Text("Rocket")),
                    ],
                    onChanged: (val) => setState(() => _mfsProvider = val!),
                  ),
                  const SizedBox(height: 12),
                  TextField(
                    controller: _phoneController,
                    decoration: const InputDecoration(labelText: 'Your MFS Phone *', border: OutlineInputBorder()),
                  ),
                  const SizedBox(height: 12),
                  TextField(
                    controller: _trxController,
                    decoration: const InputDecoration(labelText: 'Transaction ID (TrxID) *', border: OutlineInputBorder()),
                  ),
                  const SizedBox(height: 16),
                  Card(
                    color: Colors.blue.withOpacity(0.05),
                    child: Padding(
                      padding: const EdgeInsets.all(16.0),
                      child: Column(
                        children: [
                          Row(
                            mainAxisAlignment: MainAxisAlignment.between,
                            children: [
                              const Text('Total with Shipping:'),
                              Text('${cart.total + 60} BDT', style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 16)),
                            ],
                          ),
                          const SizedBox(height: 12),
                          SizedBox(
                            width: double.infinity,
                            child: ElevatedButton(
                              onPressed: _submitOrder,
                              style: ElevatedButton.styleFrom(backgroundColor: Colors.blue, foregroundColor: Colors.white),
                              child: const Text('Submit Order Checkout'),
                            ),
                          )
                        ],
                      ),
                    ),
                  ),
                ],
              ),
      ),
    );
  }
}

// ----------------------------------------------------------------------
// SCREEN: POS Dashboard (Simulates staff POS barcode scanned lookup)
// ----------------------------------------------------------------------
class MobilePOSDashboardScreen extends StatefulWidget {
  const MobilePOSDashboardScreen({super.key});

  @override
  State<MobilePOSDashboardScreen> createState() => _MobilePOSDashboardScreenState();
}

class _MobilePOSDashboardScreenState extends State<MobilePOSDashboardScreen> {
  final TextEditingController _barcodeController = TextEditingController();

  final List<Map<String, dynamic>> _mockProducts = [
    {
      'id': 'f1111111-1111-1111-1111-111111111111',
      'name': 'Classic Polo Shirt',
      'sku': 'POLO-CLS-001',
      'barcode': '2000010010015',
      'price': 1200.0,
      'wholesalePrice': 750.0,
      'stockQuantity': 100,
    },
    {
      'id': '2',
      'name': 'Eco Leather Belt',
      'sku': 'BELT-ECO-002',
      'barcode': '2000010010022',
      'price': 850.0,
      'wholesalePrice': 500.0,
      'stockQuantity': 12,
    }
  ];

  void _scanBarcode(String code) {
    final cart = Provider.of<MobilePOSCartState>(context, listen: false);
    final product = _mockProducts.firstWhere(
      (p) => p['barcode'] == code.trim(),
      orElse: () => {},
    );

    if (product.isNotEmpty) {
      cart.addToCart(product);
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text("Added ${product['name']} to POS cart"),
          backgroundColor: Colors.emerald,
        ),
      );
    } else {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text("No product found matching this barcode"),
          backgroundColor: Colors.red,
        ),
      );
    }
    _barcodeController.clear();
  }

  @override
  Widget build(BuildContext context) {
    final cart = Provider.of<MobilePOSCartState>(context);
    final tenant = Provider.of<TenantState>(context);
    final auth = Provider.of<AuthState>(context);

    return Scaffold(
      appBar: AppBar(
        title: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const Text('Mobile POS Register', style: TextStyle(fontWeight: FontWeight.bold)),
            Text('Tenant: ${tenant.tenantName} (${auth.user!['name']})', style: const TextStyle(fontSize: 11, color: Colors.blue)),
          ],
        ),
        actions: [
          IconButton(
            icon: const Icon(Icons.exit_to_app, color: Colors.red),
            onPressed: () => auth.logout(),
          )
        ],
      ),
      body: Padding(
        padding: const EdgeInsets.all(16.0),
        child: Column(
          children: [
            Card(
              elevation: 0,
              color: Colors.blue.withOpacity(0.05),
              shape: RoundedRectangleBorder(
                side: BorderSide(color: Colors.blue.withOpacity(0.2)),
                borderRadius: BorderRadius.circular(16),
              ),
              child: Padding(
                padding: const EdgeInsets.all(16.0),
                child: Column(
                  children: [
                    const Row(
                      mainAxisAlignment: MainAxisAlignment.center,
                      children: [
                        Icon(Icons.camera_alt, color: Colors.blue),
                        SizedBox(width: 8),
                        Text(
                          'Simulate Device Camera Scan',
                          style: TextStyle(fontWeight: FontWeight.bold, color: Colors.blue),
                        ),
                      ],
                    ),
                    const SizedBox(height: 12),
                    TextField(
                      controller: _barcodeController,
                      decoration: const InputDecoration(
                        hintText: 'Enter barcode (e.g. 2000010010015)',
                        border: OutlineInputBorder(),
                        fillColor: Colors.white,
                        filled: true,
                        suffixIcon: Icon(Icons.arrow_forward),
                      ),
                      onSubmitted: _scanBarcode,
                    ),
                  ],
                ),
              ),
            ),
            const SizedBox(height: 16),
            const Align(
              alignment: Alignment.centerLeft,
              child: Text('POS Cart Items', style: TextStyle(fontWeight: FontWeight.bold, fontSize: 16)),
            ),
            const SizedBox(height: 8),
            Expanded(
              child: cart.items.isEmpty
                  ? Center(
                      child: Column(
                        mainAxisAlignment: MainAxisAlignment.center,
                        children: [
                          Icon(Icons.qr_code_scanner, size: 48, color: Colors.grey[400]),
                          const SizedBox(height: 8),
                          const Text('Scan barcode labels to ring up items'),
                        ],
                      ),
                    )
                  : ListView.builder(
                      itemCount: cart.items.length,
                      itemBuilder: (context, index) {
                        final item = cart.items[index];
                        return Card(
                          margin: const EdgeInsets.only(bottom: 8),
                          child: ListTile(
                            title: Text(item['name'], style: const TextStyle(fontWeight: FontWeight.bold)),
                            subtitle: Text('Price: ${item['price']} BDT'),
                            trailing: Row(
                              mainAxisSize: MainAxisSize.min,
                              children: [
                                IconButton(
                                  icon: const Icon(Icons.remove_circle_outline),
                                  onPressed: () => cart.updateQuantity(item['id'], item['quantity'] - 1),
                                ),
                                Text('${item['quantity']}', style: const TextStyle(fontWeight: FontWeight.bold)),
                                IconButton(
                                  icon: const Icon(Icons.add_circle_outline),
                                  onPressed: () => cart.updateQuantity(item['id'], item['quantity'] + 1),
                                ),
                              ],
                            ),
                          ),
                        );
                      },
                    ),
            ),
            if (cart.items.isNotEmpty)
              Card(
                child: Padding(
                  padding: const EdgeInsets.all(16.0),
                  child: Row(
                    mainAxisAlignment: MainAxisAlignment.between,
                    children: [
                      Text(
                        'Total: ${cart.total} BDT',
                        style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 16),
                      ),
                      const Text('Ready to checkout', style: TextStyle(color: Colors.green, fontWeight: FontWeight.bold)),
                    ],
                  ),
                ),
              )
          ],
        ),
      ),
    );
  }
}

// ----------------------------------------------------------------------
// SCREEN: Inventory creation & wireless barcode direct print triggers
// ----------------------------------------------------------------------
class InventoryManagementScreen extends StatefulWidget {
  const InventoryManagementScreen({super.key});

  @override
  State<InventoryManagementScreen> createState() => _InventoryManagementScreenState();
}

class _InventoryManagementScreenState extends State<InventoryManagementScreen> {
  final _formKey = GlobalKey<FormState>();
  String _name = '';
  String _sku = '';
  double _price = 0.0;
  double _wholesalePrice = 0.0;
  int _stock = 0;
  String _barcode = '';

  void _triggerWirelessPrint(String name, String code) {
    final zpl = """
^XA
^LH30,30
^FO20,10^A0N,28,24^FD$name^FS
^FO20,40^A0N,20,16^FDSKU: $_sku  Price: $_price BDT^FS
^FO20,70^BY2
^BCN,50,Y,N,N
^FD$code^FS
^XZ
""";
    showDialog(
      context: context,
      builder: (context) => AlertDialog(
        title: const Row(
          children: [
            Icon(Icons.print, color: Colors.blue),
            SizedBox(width: 8),
            Text('Direct Printer Label'),
          ],
        ),
        content: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const Text('Sending label stream to Wi-Fi printer...'),
            const SizedBox(height: 12),
            const Text('ZPL Output:', style: TextStyle(fontWeight: FontWeight.bold, fontSize: 11)),
            const SizedBox(height: 4),
            Container(
              padding: const EdgeInsets.all(8),
              color: Colors.grey[100],
              child: Text(
                zpl,
                style: const TextStyle(fontFamily: 'monospace', fontSize: 10),
              ),
            ),
          ],
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context),
            child: const Text('Dismiss'),
          )
        ],
      ),
    );
  }

  void _submitProduct() {
    if (_formKey.currentState!.validate()) {
      _formKey.currentState!.save();

      var generatedBarcode = _barcode;
      if (generatedBarcode.isEmpty) {
        generatedBarcode = "AUTO-${DateTime.now().millisecondsSinceEpoch.toString().substring(8)}";
      }

      showDialog(
        context: context,
        builder: (context) => AlertDialog(
          title: const Text('Product Created'),
          content: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text('Title: $_name'),
              Text('Code 128: $generatedBarcode'),
              const SizedBox(height: 12),
              const Text('Do you want to print this barcode label immediately to the physical Wi-Fi printer?'),
            ],
          ),
          actions: [
            TextButton(
              onPressed: () {
                Navigator.pop(context);
              },
              child: const Text('Skip'),
            ),
            ElevatedButton(
              onPressed: () {
                Navigator.pop(context);
                _triggerWirelessPrint(_name, generatedBarcode);
              },
              child: const Text('Print barcode'),
            ),
          ],
        ),
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Inventory Creation', style: TextStyle(fontWeight: FontWeight.bold)),
      ),
      body: SingleChildScrollView(
        padding: const EdgeInsets.all(16.0),
        child: Form(
          key: _formKey,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              TextFormField(
                decoration: const InputDecoration(labelText: 'Product Title *', border: OutlineInputBorder()),
                validator: (value) => value!.isEmpty ? 'Title required' : null,
                onSaved: (value) => _name = value!,
              ),
              const SizedBox(height: 12),
              TextFormField(
                decoration: const InputDecoration(labelText: 'SKU *', border: OutlineInputBorder()),
                validator: (value) => value!.isEmpty ? 'SKU required' : null,
                onSaved: (value) => _sku = value!,
              ),
              const SizedBox(height: 12),
              Row(
                children: [
                  Expanded(
                    child: TextFormField(
                      decoration: const InputDecoration(labelText: 'Retail Price (BDT) *', border: OutlineInputBorder()),
                      keyboardType: TextInputType.number,
                      validator: (value) => value!.isEmpty ? 'Required' : null,
                      onSaved: (value) => _price = double.parse(value!),
                    ),
                  ),
                  const SizedBox(width: 12),
                  Expanded(
                    child: TextFormField(
                      decoration: const InputDecoration(labelText: 'Wholesale Price (BDT) *', border: OutlineInputBorder()),
                      keyboardType: TextInputType.number,
                      validator: (value) => value!.isEmpty ? 'Required' : null,
                      onSaved: (value) => _wholesalePrice = double.parse(value!),
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 12),
              TextFormField(
                decoration: const InputDecoration(labelText: 'Initial Stock *', border: OutlineInputBorder()),
                keyboardType: TextInputType.number,
                validator: (value) => value!.isEmpty ? 'Required' : null,
                onSaved: (value) => _stock = int.parse(value!),
              ),
              const SizedBox(height: 12),
              TextFormField(
                decoration: const InputDecoration(labelText: 'Barcode (Leave blank for auto Code 128)', border: OutlineInputBorder()),
                onSaved: (value) => _barcode = value!,
              ),
              const SizedBox(height: 24),
              SizedBox(
                width: double.infinity,
                child: ElevatedButton(
                  onPressed: _submitProduct,
                  style: ElevatedButton.styleFrom(
                    backgroundColor: Colors.blue,
                    foregroundColor: Colors.white,
                    padding: const EdgeInsets.symmetric(vertical: 16),
                  ),
                  child: const Text('Add Product & Generate Barcode', style: TextStyle(fontWeight: FontWeight.bold)),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

// ----------------------------------------------------------------------
// SCREEN: POS Invoice cashier checkouts
// ----------------------------------------------------------------------
class MobilePOSCheckoutScreen extends StatefulWidget {
  const MobilePOSCheckoutScreen({super.key});

  @override
  State<MobilePOSCheckoutScreen> createState() => _MobilePOSCheckoutScreenState();
}

class _MobilePOSCheckoutScreenState extends State<MobilePOSCheckoutScreen> {
  final TextEditingController _discountController = TextEditingController();
  final TextEditingController _customerNameController = TextEditingController();
  final TextEditingController _customerPhoneController = TextEditingController();
  final TextEditingController _trxController = TextEditingController();

  String _paymentMethod = "CASH";

  void _processCheckoutSale() {
    final cart = Provider.of<MobilePOSCartState>(context, listen: false);

    if (cart.items.isEmpty) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text("POS cart is empty"), backgroundColor: Colors.red),
      );
      return;
    }

    if (_paymentMethod != "CASH" && _trxController.text.trim().isEmpty) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text("MFS TrxID is required"), backgroundColor: Colors.red),
      );
      return;
    }

    final discount = double.tryParse(_discountController.text) ?? 0.0;
    cart.setCheckoutDetails(
      discount: discount,
      paymentMethod: _paymentMethod,
      customerName: _customerNameController.text.isNotEmpty ? _customerNameController.text : "Walk-in Customer",
      customerPhone: _customerPhoneController.text,
      transactionId: _trxController.text,
    );

    showDialog(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Checkout Complete', style: TextStyle(fontWeight: FontWeight.bold)),
        content: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const Center(
              child: Text('--- POS RECEIPT ---', style: TextStyle(fontWeight: FontWeight.bold, fontFamily: 'monospace')),
            ),
            const SizedBox(height: 8),
            Text('Invoice: POS-${DateTime.now().millisecondsSinceEpoch.toString().substring(6)}'),
            Text('Cashier: Demo Staff'),
            const Divider(color: Colors.black),
            ...cart.items.map((i) => Text('${i['name']} x${i['quantity']} - ${(i['price'] * i['quantity']).toStringAsFixed(2)} BDT')),
            const Divider(color: Colors.black),
            Text('Subtotal: ${cart.subtotal.toStringAsFixed(2)} BDT'),
            Text('Discount: ${cart.discount.toStringAsFixed(2)} BDT'),
            Text('TOTAL PAID: ${cart.total.toStringAsFixed(2)} BDT', style: const TextStyle(fontWeight: FontWeight.bold)),
            const SizedBox(height: 12),
            Text('Payment Method: $_paymentMethod'),
            if (_paymentMethod != "CASH") Text('TrxID: ${cart.transactionId}'),
          ],
        ),
        actions: [
          ElevatedButton(
            onPressed: () {
              cart.clearCart();
              _discountController.clear();
              _customerNameController.clear();
              _customerPhoneController.clear();
              _trxController.clear();
              Navigator.pop(context);
            },
            child: const Text('Dismiss'),
          ),
        ],
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final cart = Provider.of<MobilePOSCartState>(context);

    return Scaffold(
      appBar: AppBar(title: const Text('POS Checkout Register', style: TextStyle(fontWeight: FontWeight.bold))),
      body: SingleChildScrollView(
        padding: const EdgeInsets.all(16.0),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const Text('Invoice Summary', style: TextStyle(fontWeight: FontWeight.bold, fontSize: 16)),
            const SizedBox(height: 8),
            Card(
              child: Padding(
                padding: const EdgeInsets.all(16.0),
                child: Column(
                  children: [
                    Row(
                      mainAxisAlignment: MainAxisAlignment.between,
                      children: [
                        const Text('Items count:'),
                        Text('${cart.items.fold(0, (sum, i) => sum + (i['quantity'] as int))}'),
                      ],
                    ),
                    const SizedBox(height: 8),
                    Row(
                      mainAxisAlignment: MainAxisAlignment.between,
                      children: [
                        const Text('Subtotal:'),
                        Text('${cart.subtotal} BDT'),
                      ],
                    ),
                  ],
                ),
              ),
            ),
            const SizedBox(height: 16),
            const Text('Cashier Billing Details', style: TextStyle(fontWeight: FontWeight.bold, fontSize: 16)),
            const SizedBox(height: 12),
            TextField(
              controller: _discountController,
              decoration: const InputDecoration(labelText: 'Discount (BDT)', border: OutlineInputBorder()),
              keyboardType: TextInputType.number,
            ),
            const SizedBox(height: 12),
            TextField(
              controller: _customerNameController,
              decoration: const InputDecoration(labelText: 'Customer Name (Optional)', border: OutlineInputBorder()),
            ),
            const SizedBox(height: 12),
            TextField(
              controller: _customerPhoneController,
              decoration: const InputDecoration(labelText: 'Customer Phone (Optional)', border: OutlineInputBorder()),
            ),
            const SizedBox(height: 12),
            DropdownButtonFormField<String>(
              value: _paymentMethod,
              decoration: const InputDecoration(labelText: 'Payment Method', border: OutlineInputBorder()),
              items: const [
                DropdownMenuItem(value: "CASH", child: Text("Cash Checkout")),
                DropdownMenuItem(value: "BKASH", child: Text("bKash MFS")),
                DropdownMenuItem(value: "NAGAD", child: Text("Nagad MFS")),
                DropdownMenuItem(value: "ROCKET", child: Text("Rocket MFS")),
              ],
              onChanged: (val) {
                setState(() {
                  _paymentMethod = val!;
                });
              },
            ),
            if (_paymentMethod != "CASH") ...[
              const SizedBox(height: 12),
              TextField(
                controller: _trxController,
                decoration: const InputDecoration(
                  labelText: 'MFS Transaction ID (TrxID) *',
                  border: OutlineInputBorder(),
                  labelStyle: TextStyle(color: Colors.red),
                ),
              ),
            ],
            const SizedBox(height: 24),
            SizedBox(
              width: double.infinity,
              child: ElevatedButton(
                onPressed: _processCheckoutSale,
                style: ElevatedButton.styleFrom(
                  backgroundColor: Colors.emerald,
                  foregroundColor: Colors.white,
                  padding: const EdgeInsets.symmetric(vertical: 16),
                ),
                child: const Text('Complete Sale Invoice', style: TextStyle(fontWeight: FontWeight.bold)),
              ),
            ),
          ],
        ),
      ),
    );
  }
}
