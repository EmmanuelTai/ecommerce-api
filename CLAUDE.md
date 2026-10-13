# CLAUDE.md: E-Commerce Web API

Backend-only e-commerce REST API: **ASP.NET Core (.NET 10), EF Core, SQL Server**, Paystack, MailKit SMTP.
The roadmap is in `PLAN.md`. This is a portfolio project: the owner must be able to explain every line in interviews.

## How we work
- Claude writes the code; the owner reviews it. **One PLAN.md task at a time.** Stop after each task.
- Before coding a task: explain in a few short paragraphs which files you'll create and how they fit together. Then build.
- After a task: summarise what changed, point out the 2–3 things worth understanding for interviews, tick the box in PLAN.md, and suggest a commit message (`T5: auth and roles`).
- **Keep it simple.** No extra libraries, patterns or abstractions beyond what PLAN.md lists. No AutoMapper, MediatR, CQRS or FluentValidation. If something seems missing, ask; don't add it.
- No automated test projects. Verify with Scalar (`/scalar`) and `ECommerce.http`.

## Commands
```bash
dotnet build
dotnet run --project src/ECommerce.Api
dotnet ef migrations add <Name> -p src/ECommerce.Infrastructure -s src/ECommerce.Api
dotnet ef database update     -p src/ECommerce.Infrastructure -s src/ECommerce.Api
dotnet user-secrets set "<Key>" "<Value>" --project src/ECommerce.Api
```

## Architecture
- `ECommerce.Api` (controllers, middleware) → `ECommerce.Core` (entities, DTOs, interfaces, services) ← `ECommerce.Infrastructure` (EF Core, repositories, UnitOfWork, email, Paystack).
- Core has no EF Core or ASP.NET references.
- Controllers are thin: call one service method, return the standard response. No DbContext in controllers.
- Services use `IUnitOfWork`, never `AppDbContext` directly.
- Services throw `AppException` subclasses; `ExceptionHandlingMiddleware` turns them into responses.

## Rules
- **Money is `decimal`**, `decimal(18,2)` in the DB. Convert to kobo (`× 100`, to `long`) only when calling Paystack.
- **Async everywhere**, passing `CancellationToken`. Never `.Result` or `.Wait()`.
- **Reads:** `AsNoTracking()` + `Select` to DTOs. **Writes:** tracked entities.
- **Eager loading** with `Include`/`ThenInclude` where related data is needed. Lazy loading is not enabled.
- **Ownership:** take the user ID from `ICurrentUserService`, never from the route or body. Someone else's resource → 404.
- Never accept price, role, total, user ID or order status from the client (except admin status updates).
- Stock changes use a conditional `ExecuteUpdateAsync` (`WHERE stock >= qty`) inside a UnitOfWork transaction.
- Emails are sent after the commit, inside try/catch; failures are logged, never thrown.
- Products are soft-deleted (`IsActive = false`).
- All times in UTC.
- Clear the relevant `IMemoryCache` entries whenever an admin changes products or categories.

## API conventions
- Routes: `/api/...`, admin under `/api/admin/...` with `[Authorize(Roles = "Admin")]`.
- Success: `{ "success": true, "data": ..., "meta"?: {...} }`. Error: `{ "success": false, "error": { "code", "message" } }`.
- Status codes: 200, 201, 204, 400, 401, 403, 404, 409, 500.

## Security
- No secrets in code or `appsettings*.json`. Use user-secrets locally.
- Never log passwords, hashes, JWTs or Paystack keys.
- Passwords hashed with BCrypt. The webhook must verify `x-paystack-signature`.
- Add short comments explaining *why* on non-obvious code (transactions, conditional updates, webhook checks).
