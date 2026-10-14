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

**Naming:** tables and columns use EF Core's default **PascalCase** (`Products.StockQuantity`), the same as the C# entities. This is the SQL Server convention, and it needs no naming-convention config or extra package.

| Table | Key columns |
|---|---|
| Users | Id, FullName, Email (unique), PasswordHash, Phone, Role (`Customer`/`Admin`), CreatedAt |
| Categories | Id, Name (unique), Slug (unique) |
| Products | Id, CategoryId, Name, Slug (unique), Description, **Price decimal(18,2)**, StockQuantity, ImageUrl, IsActive, CreatedAt, UpdatedAt |
| Carts | Id, UserId (unique) |
| CartItems | Id, CartId, ProductId, Quantity. Unique (CartId, ProductId) |
| Orders | Id, UserId, OrderNumber (unique), Status, **TotalAmount decimal(18,2)**, ShippingFullName, ShippingPhone, ShippingStreet, ShippingCity, ShippingState, CreatedAt, UpdatedAt |
| OrderItems | Id, OrderId, ProductId, ProductName *(snapshot)*, **UnitPrice** *(snapshot)*, Quantity, **Subtotal** |
| Payments | Id, OrderId, Reference (unique), Amount, Status (`Pending`/`Successful`/`Failed`), PaidAt |

DB-level CHECK constraints: `Price > 0`, `StockQuantity >= 0`, `Quantity > 0`.

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
- [x] Solution + 3 projects, references Api → Core and Infrastructure, Infrastructure → Core.
- [x] `.gitignore`, `.editorconfig`, nullable on.
- [x] `Program.cs`: controllers, OpenAPI + **Scalar** UI at `/scalar`.
- [x] Config via the options pattern (`JwtSettings`, `SmtpSettings`, `PaystackSettings`). **Secrets in user-secrets**, never in `appsettings.json`.
- [x] `docker-compose.yml` with SQL Server and Mailpit.
- **Done when:** the app runs and `/scalar` loads.

### T2 · Entities, DbContext, migration, seed
- [x] Entities + enums (`Role`, `OrderStatus`, `PaymentStatus`). Enums stored as strings.
- [x] `AppDbContext` with Fluent API configurations: `decimal(18,2)`, unique indexes, CHECK constraints, relationships (Restrict for products, Cascade for cart/order items).
- [x] Automatic `created_at` / `updated_at` in `SaveChangesAsync`.
- [x] Initial migration.
- [x] Seeder: 1 admin (credentials from config), 4 categories, ~20 products, 1 test customer (Development only). Runs only if the DB is empty. BCrypt work factor 12.
- **Interview:** why `decimal` not `double`; why snapshots; why soft delete.

### T3 · Repositories, Unit of Work, DI
- [x] `IGenericRepository<T>` (GetById, Add, Update, Remove) + specific repos where needed (`IProductRepository`, `IOrderRepository`, `ICartRepository`). No `IQueryable` on the interface: Core has no EF Core reference, so services couldn't run it async. Queries live in the specific repos, and each task adds the ones it needs.
- [x] `IUnitOfWork`: exposes the repositories + `SaveChangesAsync`, `BeginTransactionAsync`, `CommitAsync` (saves, then commits), `RollbackAsync`.
- [x] Register everything in DI with an extension method (`AddInfrastructure()`: DbContext, UnitOfWork, seeder), choosing lifetimes deliberately.
- **Interview:** "Isn't DbContext already a Unit of Work?" Yes. The wrapper keeps services independent of EF, groups repositories behind one commit, and gives one place to control transactions.

### T4 · Middleware and error format
- [x] `ExceptionHandlingMiddleware`: catches `AppException` subclasses (`NotFoundException` 404, `BadRequestException` 400, `ConflictException` 409, `ForbiddenException` 403) → standard JSON; anything else → 500 with a generic message, full error logged.
- [x] `RequestLoggingMiddleware`: method, path, status code, duration, user ID. Never logs passwords or tokens.
- [x] Standard responses:
  - Success `{ "success": true, "data": ..., "meta"?: {page, pageSize, totalCount, totalPages} }`
  - Error `{ "success": false, "error": { "code": "...", "message": "..." } }`
- [x] Validation errors (DataAnnotations) use the same error format.
- **Interview:** pipeline order (exception handler first, then authentication, then authorization), and why.

### T5 · Auth and roles
- [x] `POST /api/auth/register`: always creates a `Customer` (role can't be sent), hashes the password (BCrypt), creates an empty cart. Duplicate email → 409.
- [x] `POST /api/auth/login` → JWT with `sub`, `email`, `role` claims. Same message for a wrong email or a wrong password.
- [x] `GET /api/auth/me`, `PUT /api/auth/me` (name, phone).
- [x] `ICurrentUserService` reads the user ID from claims. Services never trust a user ID from the URL or body.
- [x] Logout = the client deletes the token (stateless JWT; explain the trade-off).
- **Interview:** 401 vs 403; why the admin is seeded and can't register.

### T6 · Products and categories (public) + caching
- [x] `GET /api/categories` (cached 30 min).
- [x] `GET /api/products?page&pageSize&search&categoryId&minPrice&maxPrice&sortBy` (`newest`, `price_asc`, `price_desc`). Active only, pageSize max 50.
- [x] `GET /api/products/{id}` (cached 10 min). Inactive → 404.
- [x] All reads use **`AsNoTracking()`** and project to DTOs with `Select`.
- **Interview:** what change tracking costs; what you cache and why carts and orders aren't cached.

### T7 · Cart (Customer role)
- [x] `GET /api/cart`: current prices, line subtotals, total, and a flag on lines whose product became unavailable.
- [x] `POST /api/cart/items {productId, quantity}`: if already in the cart, increase the quantity; can't exceed stock; inactive product → 400.
- [x] `PUT /api/cart/items/{itemId} {quantity}` (0 removes), `DELETE /api/cart/items/{itemId}`, `DELETE /api/cart`.
- [x] Item must belong to the current user's cart, otherwise 404.
- [x] Uses **tracked** entities, because we're updating them.

### T8 · Checkout and orders (Customer role)
- [x] `POST /api/orders {shippingFullName, shippingPhone, street, city, state}`. Inside **one Unit of Work transaction**:
  1. Load the cart with items and products (**eager loading**). Empty → 400.
  2. For each item: `UPDATE Products SET StockQuantity = StockQuantity - @qty WHERE Id = @id AND StockQuantity >= @qty AND IsActive = 1`. 0 rows → roll back, 409 naming the product. *(Stops two buyers both taking the last item.)*
  3. Create the order (`Pending`, order number `ORD-yyyyMMdd-XXXXXX`) and order items with **snapshot** name and price from the DB.
  4. Clear the cart, commit.
- [x] `GET /api/orders` (mine, newest first, paginated), `GET /api/orders/{id}` (mine, with `Include(o => o.Items)`; someone else's → 404).
- [x] `POST /api/orders/{id}/cancel`: only `Pending`; returns stock in a transaction.
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
