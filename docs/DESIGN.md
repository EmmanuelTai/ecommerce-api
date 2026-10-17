# Design decisions

Each decision is written the same way: **the problem**, **what I did**, **why**, and **the trade-off**.
File names point to where the code lives.

---

## 1. Unit of Work + repositories

**The problem.** Services need to read and write data, and some operations touch several tables that must succeed or
fail together. Checkout, for example, changes products, orders, order items and cart items. I also wanted the business
logic in `ECommerce.Core` to have no reference to EF Core.

**What I did.** Core defines `IUnitOfWork` (`Interfaces/IUnitOfWork.cs`). It exposes the repositories (`Users`,
`Products`, `Orders`, …) plus `SaveChangesAsync`, `BeginTransactionAsync`, `CommitAsync` and `RollbackAsync`.
Infrastructure implements it (`Data/UnitOfWork.cs`). All repositories are given the same `AppDbContext`, so they share
one change tracker and one connection. `UnitOfWork` is registered as **scoped**, so every service in a request gets the
same instance. The generic repository only has `GetById`, `Add`, `Update` and `Remove`. Real queries are named methods
on specific repositories (`GetActiveDtosAsync`, `TryDecreaseStockAsync`), because Core can't run `IQueryable` async
without EF Core.

**Why.**
- Services depend only on interfaces, so the rules don't know or care that EF Core is underneath.
- One commit covers changes made through any repository.
- Transactions are controlled in one place. `CommitAsync` saves before committing, so a service can't forget to save.
- Database errors are translated in one place: a unique-index violation (SQL errors 2601/2627) becomes a 409
  `ConflictException` instead of a 500.

**The trade-off.** *"Isn't DbContext already a Unit of Work, and DbSet already a repository?"* Yes. `DbContext` tracks
changes and saves them in one transaction, and that's exactly what my `UnitOfWork` relies on. The wrapper adds a layer
and some boilerplate, and every new kind of query needs a repository method instead of a quick LINQ query in the
service. I accepted that to keep Core independent of EF Core and to make the data access each service uses explicit. In
a small app that doesn't care about layering, using `DbContext` directly in services is a perfectly reasonable choice.

---

## 2. `decimal` for money

**The problem.** `double` and `float` are binary floating point. They can't represent values like 0.10 exactly, so
`0.1 + 0.2` gives `0.30000000000000004`. Across thousands of order lines, those errors show up as wrong totals.

**What I did.** Every amount (`Price`, `UnitPrice`, `Subtotal`, `TotalAmount`, `Payment.Amount`) is `decimal` in C# and
`decimal(18,2)` in SQL Server, set with the Fluent API. Paystack wants amounts in kobo as whole numbers, so
`PaystackClient` is the only class that converts (`× 100`, cast to `long`) and converts back on the way in. Order-line
subtotals are calculated in C#, so they stay at two decimal places.

**Why.** `decimal` is base-10, so 0.10 is exactly 0.10. Keeping the kobo conversion in one class means the rest of the
code never has to remember which unit an amount is in.

**The trade-off.** `decimal` is slower than `double` and takes more memory. That doesn't matter for an online store,
where correctness matters far more. Storing everything as kobo integers would also work, and some systems do that. I
kept naira-as-decimal because it reads naturally everywhere except the one Paystack boundary.

---

## 3. Oversell protection: conditional UPDATE inside a transaction

**The problem.** Two customers check out at the same moment, and one item is left. The naive code reads the stock (1),
checks `1 >= 1`, then saves `stock = 0`. Both requests can read `1` before either one saves, so both succeed and the
store sells an item it doesn't have.

**What I did.** In `OrderService.PlaceOrderAsync`, the whole checkout runs inside one Unit of Work transaction. For each
cart line, `ProductRepository.TryDecreaseStockAsync` runs one statement:

```sql
UPDATE Products SET StockQuantity = StockQuantity - @qty, UpdatedAt = @now
WHERE Id = @id AND IsActive = 1 AND StockQuantity >= @qty
```

If it changes 0 rows, the product is sold out or was deactivated. The checkout throws a 409 naming the product, and the
transaction rolls back, which also returns any stock already taken for earlier lines. If every line succeeds, the order,
its items (with snapshot name and price) and the emptied cart are saved and committed together. Cart lines are processed
in product-ID order. A database `CHECK (StockQuantity >= 0)` constraint is the last line of defence.

**Why.**
- The check and the change are one statement, so SQL Server evaluates the `WHERE` while holding a row lock. A second
  buyer waits for the first to commit, then sees the new stock and gets 0 rows.
- The transaction makes the checkout all-or-nothing. Without it, a failure on the third line would leave stock taken
  for the first two with no order.
- Locking rows in the same order (by product ID) in every checkout prevents deadlocks. Otherwise one checkout could lock
  A then wait for B while another locks B then waits for A.

Cancelling uses the same idea in reverse. The status change is conditional (`WHERE Status = 'Pending'`), so two cancel
requests can't both return the stock.

**The trade-off.** `ExecuteUpdateAsync` bypasses EF's change tracker, so `UpdatedAt` is set by hand, and tracked
entities in memory don't see the new value (that's why the order is read back fresh afterwards). Row locks are held
until commit, so a very hot product would serialise checkouts. That's fine here. Another limitation: stock is reserved
when the order is placed, not when it's paid, so unpaid orders hold stock until cancelled (see "What I'd add next" in the README).

---

## 4. `AsNoTracking` for reads, tracking for writes

**The problem.** By default, EF keeps a snapshot of every entity it loads, so it can work out at `SaveChanges` what
changed. For a product list that's only going to be turned into JSON, that snapshot is wasted memory and CPU.

**What I did.** Every read query uses `AsNoTracking()` and projects with `Select` straight into a DTO (for example
`ProductRepository.GetActiveDtosAsync`, which uses a shared `ToDto` expression). Every write loads **tracked** entities:
the cart when adding items, the order when cancelling, the product when an admin edits it. Then it changes them and calls save.

**Why.** No tracking means less work per row on reads, which are most of the traffic. With `Select`, SQL fetches only
the columns the DTO needs. The password hash, for example, is never even read when listing customers. Tracking on
writes is what lets EF generate the right `UPDATE` without me listing the changed columns.

**The trade-off.** You have to choose deliberately each time. If you load something with `AsNoTracking` and then try to
modify it, nothing is saved unless you attach it. Projection also means one DTO-shaped query per screen, instead of
loading an entity once and reusing it.

---

## 5. Eager loading vs projection (lazy loading off)

**The problem.** Related data (an order's items, a cart line's product) has to come from somewhere. With lazy loading,
touching `order.Items` in a loop runs one extra query per order: the **N+1 problem**. It's easy to miss, because the
code looks innocent.

**What I did.** Lazy loading isn't enabled (no proxies package, no `virtual` navigation properties). When a **write**
needs related entities, the repository uses `Include`/`ThenInclude`. At checkout, for example,
`CartRepository.GetByUserIdWithItemsAsync` loads the cart, its items and each item's product in one query. When a
**read** needs related data, a `Select` projection reaches through the navigation (`p.Category.Name`), and EF turns it
into a SQL join with no `Include` needed.

**Why.** Every query is visible in the repository code, so the number of database round trips is predictable. With
lazy loading off, a missing `Include` shows up as an empty collection or a null during development, instead of as a slow
page in production.

**The trade-off.** You have to know up front what each operation needs. Over-including (pulling whole object graphs
"just in case") would be the opposite mistake, which is why reads use projection instead of `Include`.

---

## 6. Caching with `IMemoryCache`

**The problem.** The category list and individual product pages are read far more often than they change. Hitting the
database every time is wasted work. But a cache that shows stale data in the wrong place (wrong stock at checkout, an
order in the wrong status) is worse than no cache.

**What I did.** `IMemoryCache` is registered as a singleton. Key names live in one class (`Services/CacheKeys.cs`).
- **Cached:** `GET /api/categories` for 30 minutes, and `GET /api/products/{id}` for 10 minutes. Only found, active
  products are cached, so a product that is created or reactivated shows up straight away.
- **Invalidated:** admin product changes remove that product's entry. Category changes remove the category list.
  Renaming a category also removes every cached product in it, because the product DTO carries the category name.
  Checkout and cancel remove the products whose stock changed, but only after the commit, so a rollback leaves the cache correct.
- **Not cached:** the product list, because every combination of page, search, filters and sort would be its own entry
  and rarely hit again. Also not cached: carts, orders, payments, profiles, the dashboard and admin lists. They're per
  user or admin-only, and they must be exact.
- Cart and checkout always check stock against the database, never the cache.

**Why.** It's a cheap win on the hottest reads. The expiry times are a backstop in case an invalidation is ever missed.

**The trade-off.** `IMemoryCache` lives inside one process. With two API instances behind a load balancer, each would
have its own cache, and an admin edit on one wouldn't clear the other. At that point you'd move to a shared cache such
as Redis. The cached stock count can be up to 10 minutes stale on the product page, which is acceptable for display only.

---

## 7. Middleware order and one error format

**The problem.** Errors come from many places: validation, services, authentication, unknown routes, unexpected bugs.
Without a plan, each produces a different response shape, and an unhandled exception can leak a stack trace or SQL to the client.

**What I did.** In `Program.cs` the pipeline is, in order:
1. `RequestLoggingMiddleware`: method, path, status code, duration and user ID. It's outermost, so it records the final
   status code, even a 500 set further in. It never logs bodies, headers or query strings, where passwords and tokens travel.
2. `ExceptionHandlingMiddleware`: catches `AppException` subclasses (`BadRequest` 400, `Unauthorized` 401,
   `Forbidden` 403, `NotFound` 404, `Conflict` 409) and writes `{ "success": false, "error": { "code", "message" } }`.
   Any other exception is logged in full and the client gets a generic 500. A cancelled request (client disconnected) isn't logged as an error.
3. `UseStatusCodePages`: gives empty 401/403/404/405 responses (from the auth middleware or unknown routes) the same JSON body.
4. `UseAuthentication`, then `UseAuthorization`, then the controllers.

DataAnnotations validation errors are reshaped into the same format (`VALIDATION_ERROR`, or `INVALID_JSON` for a broken body).

**Why.** Each middleware wraps everything registered after it. The exception handler has to sit outside authentication
and the controllers to catch their errors. Authentication has to run before authorization: first who you are, then
what you may do. Services just throw, so controllers stay thin with no try/catch, and clients only ever parse one error shape.

**The trade-off.** Exceptions are used for expected outcomes like "not found". That costs a little more than returning
a result object, but at this scale the clarity is worth it. The middleware order is also something you have to know:
moving one line in `Program.cs` changes behaviour.

---

## 8. JWT + role-based authorization (401 vs 403)

**The problem.** The API is stateless. Each request has to prove who is calling and what they're allowed to do, without
the server storing sessions.

**What I did.** `POST /api/auth/login` checks the BCrypt hash (work factor 12) and returns a JWT signed with HMAC-SHA256,
with `sub` (user ID), `email` and `role` claims. It expires after 60 minutes, with 30 seconds of clock skew. Login gives
the same message for a wrong email and a wrong password, and it checks against a dummy hash when the email doesn't
exist, so response timing doesn't reveal which emails are registered. Registration always creates a `Customer`: the
request DTO has no role property, and the only admin is seeded from user-secrets. Controllers use
`[Authorize(Roles = "Customer")]` or `[Authorize(Roles = "Admin")]`. `ICurrentUserService` reads the user ID from the
token. Services never accept a user ID from the URL or body.

- **401 Unauthorized:** no token, an expired token, or an invalid one. *We don't know who you are.*
- **403 Forbidden:** a valid customer token on an admin route. *We know who you are, and you can't do this.*
- **404 Not Found:** another customer's order, cart item or payment. A 403 would confirm that the ID exists, so the API
  answers as if it doesn't.

**Why.** JWTs need no server-side session store, and roles are enough for two kinds of user. Taking the user ID only
from the signed token means a customer can't act as someone else by editing a request.

**The trade-off.** A JWT can't be revoked before it expires. "Logout" is the client deleting its token, and a stolen
token stays valid for up to an hour. Refresh tokens (short-lived access tokens plus revocable refresh tokens) are the
standard fix and are top of my "next" list. Role-based checks are simple. Finer rules, like "support staff can view but
not edit orders", would need policy-based authorization.

---

## 9. Paystack: webhook signature verification + the verify call

**The problem.** The webhook endpoint has to be anonymous, because Paystack doesn't send a JWT. That means anyone on the
internet can POST a fake "charge.success" to it. Even a genuine webhook could be replayed, arrive twice, or arrive at the
same moment as the customer's own verify call.

**What I did.**
- **Initialize:** `POST /api/payments/initialize/{orderId}` (owner only, order must be `Pending`) takes the amount from
  the order in the database, never from the client. It calls Paystack's *Initialize Transaction* with a random
  `PAY-<guid>` reference and stores a `Pending` payment.
- **Signature first:** the controller reads the **raw** request body, because model binding would re-serialise it and
  break the signature. `PaystackClient.IsValidSignature` computes HMAC-SHA512 of those bytes with the secret key and
  compares it with `x-paystack-signature` using `CryptographicOperations.FixedTimeEquals`. A wrong signature gives 401
  before the body is even parsed.
- **Verify, don't trust:** for a `charge.success` event, `PaymentService.ConfirmAsync` calls Paystack's *Verify
  Transaction* with our own key, and checks that the status is `success` and that the amount and currency (NGN) match the payment record.
- **Idempotent and race-safe:** the payment's `Pending → Successful` change and the order's `Pending → Paid` change are
  conditional updates in one transaction. If the webhook and `GET /api/payments/verify/{reference}` run at the same time,
  only one of them changes the row, and the email goes out once. Other events and unknown references return 200, so Paystack stops retrying.
- **Backup:** the verify endpoint covers a missed webhook, and it shares the same `ConfirmAsync` code.

**Why.** The signature proves the request came from someone holding the secret key. The verify call protects against
anything odd in the body itself. The conditional updates make it safe for Paystack to retry, which it does whenever it
doesn't get a 200.

**The trade-off.** Every confirmation costs an extra HTTP call to Paystack. Edge cases remain: if a customer cancels
while on the checkout page and then pays, the payment is recorded as `Successful`, the order stays `Cancelled`, and a
warning is logged for a manual refund (refunds are out of scope). Locally Paystack can't reach the webhook, so it's
tested with a Postman request that signs the body itself.

---

## 10. Emails sent after the commit

**The problem.** An email can't be rolled back. If the "order placed" email went out inside the checkout transaction
and the commit then failed, the customer would be told about an order that doesn't exist. And if the mail server is
down, that shouldn't stop people buying things.

**What I did.** `IEmailService.SendAsync` (MailKit, in `Email/SmtpEmailService.cs`) is only called **after**
`CommitAsync` succeeds. The email content is built from data loaded before the transaction, so nothing after the commit
can fail on a database read. `SendAsync` wraps everything in try/catch: a failure is logged (with the subject, not the
recipient's address) and never thrown. It uses a 10-second timeout instead of MailKit's 2 minutes, and
`CancellationToken.None`, so the email still goes out if the customer disconnects after the order exists. There are four
templates: welcome, order placed, payment received, status changed.

**Why.** The order is the thing that matters. The email is a notification about it, so its failure must never undo or
block the order.

**The trade-off.** Sending happens inside the HTTP request, so a slow mail server makes the response slower (capped by
the timeout). If the process crashes between the commit and the send, that email is lost. The standard fix is an
**outbox**: write the email to a table in the same transaction, and have a background worker send it and retry.

---

## Smaller decisions worth knowing

- **Snapshots on order items.** `ProductName` and `UnitPrice` are copied onto each order line, so an order keeps showing
  what was paid even if the product is renamed, repriced or deactivated later.
- **Soft delete for products.** "Delete" sets `IsActive = false`. Old orders and payments still reference the product
  (foreign keys use `Restrict`), so a hard delete would either fail or break history.
- **Never trust the client.** Prices, totals, roles, user IDs and order status are never read from a request (except
  an admin changing an order status, which is checked against the allowed flow).
- **The order status flow as data.** `AdminOrderService` holds a dictionary of allowed moves
  (`Pending → Cancelled`, `Paid → Processing → Shipped → Delivered`). Anything else is a 409. `Pending → Paid` only
  happens through a confirmed Paystack payment.
- **Random order numbers.** `ORD-yyyyMMdd-XXXXXX`, from an alphabet without 0/O and 1/I. They're random rather than
  sequential, so they don't reveal how many orders the store gets. The unique index catches the rare clash.
- **Secrets and startup validation.** Settings are bound with the options pattern, using `ValidateDataAnnotations()` and
  `ValidateOnStart()`. A missing user-secret stops the app at startup with a clear message, instead of failing on some
  later request. The Paystack `Authorization` header is redacted from `HttpClient` logs.
- **DI lifetimes.** `DbContext` and `UnitOfWork` are scoped (one per request; a singleton would capture the first
  request's context). `IMemoryCache` and the token service are singletons. The email service is transient. Paystack
  uses a typed `HttpClient` from `IHttpClientFactory`, which pools connections and avoids running out of sockets.
