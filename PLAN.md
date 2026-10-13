# E-Commerce Web API: Build Plan

A single-store e-commerce **backend** in **ASP.NET Core (.NET 10), EF Core and SQL Server**.
Customers browse, add to cart, check out and pay with **Paystack**. Admins manage the store through **admin-only API endpoints**. Emails go out over **SMTP**.

Kept deliberately simple: every part should be explainable in an interview.

---

## What this project demonstrates

| Concept | Where |
|---|---|
| Layered architecture (Api → Core ← Infrastructure) | Solution structure |
| Repository + **Unit of Work** | T3, used in every service |
| **Dependency injection** (scoped / singleton / transient) | T3, `Program.cs` |
| Custom **middleware** (exception handling, request logging) | T4 |
| JWT auth + **role-based authorization** (Customer / Admin) | T5, T11 |
| **Async/await** with `CancellationToken` everywhere | All tasks |
| **Tracking vs AsNoTracking** | T6 (reads), T7/T8 (writes) |
| **Eager loading** (`Include`/`ThenInclude`); lazy loading deliberately off | T8 |
| **Simple caching** with `IMemoryCache` + invalidation | T6, T11 |
| **Decimal** money | T2 |
| Transactions + preventing overselling | T8 |
| **SMTP email** (MailKit), tested with a fake inbox | T9 |
| **Paystack** payments + webhook signature verification | T10 |

**Out of scope (on purpose):** frontend, automated tests, Pay on Delivery, saved addresses, refunds, reviews, Redis, background job queues, refresh tokens. "What would you add next?" is a good interview question to have answers for.

---

## Solution structure

```
ECommerce/
├── src/
│   ├── ECommerce.Api/            # Controllers, Middleware, Program.cs, appsettings (no secrets)
│   ├── ECommerce.Core/           # Entities, Enums, DTOs, Interfaces, Services, Exceptions
│   └── ECommerce.Infrastructure/ # AppDbContext, Configurations, Migrations, Repositories,
│                                 # UnitOfWork, Email (MailKit), Payments (Paystack), Seeding
├── docker-compose.yml            # SQL Server + Mailpit (optional; for anyone cloning the repo)
├── ECommerce.http                # Ready-made requests to try every endpoint
├── CLAUDE.md
├── PLAN.md
└── README.md
```
**Dependency direction:** Api → Core ← Infrastructure. Core defines interfaces (`IUnitOfWork`, `IEmailService`, `IPaymentService`); Infrastructure implements them; DI in `Program.cs` connects them.

---

## Data model

| Table | Key columns |
|---|---|
| users | id, full_name, email (unique), password_hash, phone, role (`Customer`/`Admin`), created_at |
| categories | id, name (unique), slug (unique) |
| products | id, category_id, name, slug (unique), description, **price decimal(18,2)**, stock_quantity, image_url, is_active, created_at, updated_at |
| carts | id, user_id (unique) |
| cart_items | id, cart_id, product_id, quantity. Unique (cart_id, product_id) |
| orders | id, user_id, order_number (unique), status, **total_amount decimal(18,2)**, shipping_full_name, shipping_phone, shipping_street, shipping_city, shipping_state, created_at, updated_at |
| order_items | id, order_id, product_id, product_name *(snapshot)*, **unit_price** *(snapshot)*, quantity, **subtotal** |
| payments | id, order_id, reference (unique), amount, status (`Pending`/`Successful`/`Failed`), paid_at |

DB-level CHECK constraints: `price > 0`, `stock_quantity >= 0`, `quantity > 0`.

**Order status flow**
```
Pending ──(Paystack webhook)──▶ Paid ──▶ Processing ──▶ Shipped ──▶ Delivered
   │
   └──(customer or admin)──▶ Cancelled   (stock returned)
```
Any other change → 409 Conflict.

---

## Tasks (one commit each)

### T1 · Scaffold
- [ ] Solution + 3 projects, references Api → Core and Infrastructure, Infrastructure → Core.
- [ ] `.gitignore`, `.editorconfig`, nullable on.
- [ ] `Program.cs`: controllers, OpenAPI + **Scalar** UI at `/scalar`.
- [ ] Config via the options pattern (`JwtSettings`, `SmtpSettings`, `PaystackSettings`). **Secrets in user-secrets**, never in `appsettings.json`.
- [ ] `docker-compose.yml` with SQL Server and Mailpit.
- **Done when:** the app runs and `/scalar` loads.

### T2 · Entities, DbContext, migration, seed
- [ ] Entities + enums (`Role`, `OrderStatus`, `PaymentStatus`). Enums stored as strings.
- [ ] `AppDbContext` with Fluent API configurations: `decimal(18,2)`, unique indexes, CHECK constraints, relationships (Restrict for products, Cascade for cart/order items).
- [ ] Automatic `created_at` / `updated_at` in `SaveChangesAsync`.
- [ ] Initial migration.
- [ ] Seeder: 1 admin (credentials from config), 4 categories, ~20 products, 1 test customer. Runs only if the DB is empty.
- **Interview:** why `decimal` not `double`; why snapshots; why soft delete.

### T3 · Repositories, Unit of Work, DI
- [ ] `IGenericRepository<T>` (GetById, Add, Update, Remove, Query) + specific repos where needed (`IProductRepository`, `IOrderRepository`, `ICartRepository`).
- [ ] `IUnitOfWork`: exposes the repositories + `SaveChangesAsync`, `BeginTransactionAsync`, `CommitAsync`, `RollbackAsync`.
- [ ] Register everything in DI with an extension method (`AddInfrastructure()`), choosing lifetimes deliberately.
- **Interview:** "Isn't DbContext already a Unit of Work?" Yes. The wrapper keeps services independent of EF, groups repositories behind one commit, and gives one place to control transactions.

### T4 · Middleware and error format
- [ ] `ExceptionHandlingMiddleware`: catches `AppException` subclasses (`NotFoundException` 404, `BadRequestException` 400, `ConflictException` 409, `ForbiddenException` 403) → standard JSON; anything else → 500 with a generic message, full error logged.
- [ ] `RequestLoggingMiddleware`: method, path, status code, duration, user ID. Never logs passwords or tokens.
- [ ] Standard responses:
  - Success `{ "success": true, "data": ..., "meta"?: {page, pageSize, totalCount, totalPages} }`
  - Error `{ "success": false, "error": { "code": "...", "message": "..." } }`
- [ ] Validation errors (DataAnnotations) use the same error format.
- **Interview:** pipeline order (exception handler first, then authentication, then authorization), and why.

### T5 · Auth and roles
- [ ] `POST /api/auth/register`: always creates a `Customer` (role can't be sent), hashes the password (BCrypt), creates an empty cart. Duplicate email → 409.
- [ ] `POST /api/auth/login` → JWT with `sub`, `email`, `role` claims. Same message for a wrong email or a wrong password.
- [ ] `GET /api/auth/me`, `PUT /api/auth/me` (name, phone).
- [ ] `ICurrentUserService` reads the user ID from claims. Services never trust a user ID from the URL or body.
- [ ] Logout = the client deletes the token (stateless JWT; explain the trade-off).
- **Interview:** 401 vs 403; why the admin is seeded and can't register.

### T6 · Products and categories (public) + caching
- [ ] `GET /api/categories` (cached 30 min).
- [ ] `GET /api/products?page&pageSize&search&categoryId&minPrice&maxPrice&sortBy` (`newest`, `price_asc`, `price_desc`). Active only, pageSize max 50.
- [ ] `GET /api/products/{id}` (cached 10 min). Inactive → 404.
- [ ] All reads use **`AsNoTracking()`** and project to DTOs with `Select`.
- **Interview:** what change tracking costs; what you cache and why carts and orders aren't cached.

### T7 · Cart (Customer role)
- [ ] `GET /api/cart`: current prices, line subtotals, total, and a flag on lines whose product became unavailable.
- [ ] `POST /api/cart/items {productId, quantity}`: if already in the cart, increase the quantity; can't exceed stock; inactive product → 400.
- [ ] `PUT /api/cart/items/{itemId} {quantity}` (0 removes), `DELETE /api/cart/items/{itemId}`, `DELETE /api/cart`.
- [ ] Item must belong to the current user's cart, otherwise 404.
- [ ] Uses **tracked** entities, because we're updating them.

### T8 · Checkout and orders (Customer role)
- [ ] `POST /api/orders {shippingFullName, shippingPhone, street, city, state}`. Inside **one Unit of Work transaction**:
  1. Load the cart with items and products (**eager loading**). Empty → 400.
  2. For each item: `UPDATE products SET stock = stock - @qty WHERE id = @id AND stock >= @qty AND is_active = 1`. 0 rows → roll back, 409 naming the product. *(Stops two buyers both taking the last item.)*
  3. Create the order (`Pending`, order number `ORD-yyyyMMdd-XXXXXX`) and order items with **snapshot** name and price from the DB.
  4. Clear the cart, commit.
- [ ] `GET /api/orders` (mine, newest first, paginated), `GET /api/orders/{id}` (mine, with `Include(o => o.Items)`; someone else's → 404).
- [ ] `POST /api/orders/{id}/cancel`: only `Pending`; returns stock in a transaction.
- **Interview:** transactions; why the conditional update beats read-then-check; eager vs lazy loading and the N+1 problem; why 404 instead of 403 for other people's orders.

### T9 · Email over SMTP
- [ ] `IEmailService` → `SmtpEmailService` using **MailKit**; settings (host, port, user, password, from) from config.
- [ ] Emails: welcome (register), order placed, payment received, order status changed.
- [ ] Sent **after** the database commit, wrapped in try/catch: a failed email is logged and never breaks the order.
- [ ] Dev testing with **Mailpit** (Docker: SMTP on `localhost:1025`, inbox UI at `http://localhost:8025`) or **smtp4dev** (`dotnet tool install -g Rnwood.Smtp4dev`, no Docker needed).
- **Interview:** why email goes after the commit; what you'd change at scale (a queue / outbox).

### T10 · Paystack payments
- [ ] `POST /api/payments/initialize/{orderId}` (owner, order `Pending`): calls Paystack *Initialize Transaction* with `amount = total × 100` (kobo), creates a `Pending` payment with a unique reference, returns `authorizationUrl`.
- [ ] `POST /api/payments/webhook` (anonymous): verifies the **`x-paystack-signature`** header (HMAC-SHA512 of the raw body with the secret key). Invalid → 401. On `charge.success`: confirms through *Verify Transaction*, checks the amount, then payment → `Successful` and order → `Paid`. Running it twice changes nothing (idempotent).
- [ ] `GET /api/payments/verify/{reference}`: backup in case the webhook is missed.
- [ ] Typed `HttpClient` for Paystack, registered via DI. Keys from user-secrets (test keys only).
- [ ] Local webhook testing: expose the API with **ngrok** (or Cloudflare Tunnel) and set the URL in the Paystack dashboard.
- **Known limitation to mention:** unpaid `Pending` orders hold stock until cancelled; at scale, a background job would cancel them after 30 minutes.

### T11 · Admin endpoints (`[Authorize(Roles = "Admin")]`)
- [ ] Products: `GET /api/admin/products` (including inactive), `POST`, `PUT /{id}`, `DELETE /{id}` (soft delete). Clears the product and category cache.
- [ ] Categories: `POST`, `PUT /{id}`, `DELETE /{id}` (409 if it still has products).
- [ ] Orders: `GET /api/admin/orders?status&page`, `GET /api/admin/orders/{id}`, `PUT /api/admin/orders/{id}/status` (enforces the status flow; cancelling returns stock; sends the status email).
- [ ] Customers: `GET /api/admin/customers` (read only, never shows password hashes).
- [ ] Dashboard: `GET /api/admin/dashboard` (order counts by status, total revenue from paid+ orders, low-stock products, 5 latest orders).
- **Interview:** role-based vs policy-based authorization; a customer token here → 403.

### T12 · Ship it
- [ ] `ECommerce.http` with every request in order (register → login → browse → cart → checkout → pay → admin).
- [ ] README: overview, architecture diagram, tech stack, how to run (secrets, DB, Mailpit, Paystack test keys), endpoint table, **design decisions**, screenshots (Scalar, Mailpit inbox, Paystack test checkout).
- [ ] Final review: no secrets in history, clean build, consistent naming.
- [ ] Write CV bullet points from what was built.
