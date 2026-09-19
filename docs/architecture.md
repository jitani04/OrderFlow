# Architecture

OrderFlow is one ASP.NET Core service over one PostgreSQL database, with a React admin
panel in front of it. This document explains how it is put together and why.

---

## Layering

```
  ┌──────────────────────┐
  │   OrderFlow.Api      │  controllers, DI, auth, validation, Swagger
  └──────────┬───────────┘
             │ depends on
  ┌──────────▼───────────┐       ┌──────────────────────────┐
  │  OrderFlow.Domain    │◀──────│ OrderFlow.Infrastructure │
  │  entities + rules    │       │ DbContext, repositories  │
  │  (no dependencies)   │       │ order placement          │
  └──────────────────────┘       └──────────────────────────┘
```

Dependencies point **inward**. `Api` and `Infrastructure` both reference `Domain`;
`Domain` references nothing — not EF Core, not ASP.NET, not even a logging abstraction.

That is enforced by `OrderFlow.Domain.csproj`, which contains no `<ItemGroup>` at all. It
buys two things:

1. **Fast, honest tests.** The reservation rules are tested against plain objects in
   milliseconds, with no database and no host.
2. **Rules that cannot drift.** Nothing in `Domain` can reach for a `DbContext` mid-rule,
   because it cannot see one. If a change seems to need that, the logic belongs in
   `Infrastructure` instead.

### What lives where

| Layer | Contains | Example |
|---|---|---|
| Domain | Entities, enums, invariants, pure rules | `StockReservationService.Reserve` |
| Infrastructure | Persistence, transactions, hashing, seeding | `OrderPlacementService`, `BCryptPasswordHasher` |
| Api | HTTP shape, auth, validation, error mapping | `OrdersController`, `ValidationActionFilter` |

The interfaces are declared in `Domain` (`IOrderRepository`, `IOrderPlacementService`,
`IPasswordHasher`) and implemented in `Infrastructure`. That inversion is what lets a
controller depend on "place an order" without knowing a database exists.

---

## Data model

```
  products ──1:1── stock_levels
     │
     │ 1:N (restrict)
     ▼
  order_items ──N:1── orders

  users   (standalone; authentication only)
```

| Table | Key | Notes |
|---|---|---|
| `products` | `Id` | `Sku` has a unique index |
| `stock_levels` | `ProductId` | Keyed by product — one row per product, never two |
| `orders` | `Id` | `Status` stored as text, not an int |
| `order_items` | `Id` | FK to `orders` (cascade) and `products` (**restrict**) |
| `users` | `Id` | `Username` unique, BCrypt hash only |

Three choices worth calling out:

- **`stock_levels` is keyed by `ProductId`.** Stock is not an independent entity with its
  own identity; it is a fact about a product. Keying it that way makes "two stock rows for
  one product" unrepresentable rather than merely discouraged.
- **`order_items → products` is `Restrict`, not `Cascade`.** Deleting a product that has
  been ordered would erase order history. The delete is refused instead.
- **`Status` is text.** A few extra bytes buys a table that is readable in `psql`, which
  matters far more than the space when you are working out why an order was rejected.

---

## Placing an order

This is the core of the service and the part worth understanding in full.

```
POST /api/orders
      │
      ▼
OrdersController                  ← validation already ran in the action filter
      │
      ▼
OrderPlacementService.PlaceAsync
      │
      ├─ execution strategy opens the retry boundary
      ├─ BEGIN
      │    ├─ SELECT … FROM stock_levels WHERE … ORDER BY "ProductId" FOR UPDATE
      │    ├─ read prices and SKUs from products
      │    ├─ reject unknown product ids        → 400
      │    ├─ Order.Place(...)                  → Pending
      │    ├─ StockReservationService.Reserve   → Confirmed or Rejected (pure)
      │    ├─ INSERT order + order_items, UPDATE stock_levels
      │    └─ COMMIT
      ▼
201 Created  { status: "Confirmed" | "Rejected", rejectionReason }
```

### Why a transaction is not enough on its own

Under PostgreSQL's default `READ COMMITTED` isolation, two concurrent orders for the last
three widgets **both** read "3 on hand", both conclude they fit, and both commit. Nothing
conflicts, because neither wrote a row the other had already touched at read time.

`FOR UPDATE` takes a row lock at read time. The second transaction blocks until the first
commits, then re-reads the true remaining quantity and rejects.

This is not theoretical — `ConcurrentOrderTests` fires five simultaneous orders of four
units at a product holding ten. With the lock, exactly two succeed. Remove the `FOR UPDATE`
line and all five commit, selling twenty units from a stock of ten with no error raised
anywhere.

### Why the lock order matters

Locks are taken `ORDER BY "ProductId"`. Without that, an order for `{widget, gadget}` and
one for `{gadget, widget}` would each hold one row and wait on the other for ever, until
PostgreSQL killed one as a deadlock victim. A consistent lock order across every caller
makes that unreachable.

### Why the execution strategy wraps it

The connection enables `EnableRetryOnFailure`, so transient faults recover on their own. EF
Core then refuses to let a manually opened transaction span a retry it does not control —
retrying half a transaction is worse than failing. `CreateExecutionStrategy()` hands EF the
retry boundary, so a dropped connection replays the whole transaction rather than part of it.

### Why check-then-apply

`StockReservationService` validates every line before deducting any line, so a partly
filled order is **unreachable** rather than something the caller has to undo. The
alternative — deduct as you go, compensate on failure — leaves stock missing for an order
that is about to be rejected if the compensation itself fails.

The method is pure: it reads and mutates only the objects handed to it, touching no
database and no clock. Persisting the result, and holding the locks that make it safe, is
the caller's job. That separation is what makes it testable without infrastructure.

---

## Request pipeline

```
Serilog request logging
  → CORS
    → Authentication (JWT bearer)
      → Authorization ([Authorize], [Authorize(Roles = "Admin")])
        → Controller
            ├─ ValidationActionFilter  (FluentValidation → 400 problem details)
            └─ action
  ← DomainExceptionHandler (DomainException → 400 problem details)
```

Order matters: authentication must run before authorization, and both before the
controller. The exception handler is registered first so it wraps everything after it.

---

## Design tradeoffs

**Prices are copied onto the order line, not referenced.** Repricing a product must not
change what an existing order was charged. The cost is denormalisation; the benefit is that
order history stays true.

**The client never sends a price.** `POST /api/orders` takes only `productId` and
`quantity`. In a microservices version of this system the caller would have to supply a
price, because Orders could not synchronously ask Inventory. One service removes that
whole problem.

**Rejected orders are persisted.** They record what customers tried to buy and could not
get, which is the signal that drives restocking. They return `201`, because the order *was*
recorded — the request did not fail. The outcome is in the body.

**"Out of stock" and "no such product" are different.** The first is a rejected order; the
second is a `400`. Conflating them also violates the `order_items` foreign key, since a
rejected order still persists its lines.

**Migrations run at startup under compose, and as a Job in Kubernetes.** Startup migration
keeps local development to one command but races when there is more than one replica. The
same image run with `--migrate-only` gives Kubernetes a single owner for the schema.

**Order history is paged; the catalogue is not.** Orders only grow, so an unbounded list
endpoint fails eventually and silently. Products are bounded and the order form needs all
of them. Paging is offset-based, which suits a pager and degrades on deep pages; keyset
paging would be the answer for an endless-scroll client.

**Validation is duplicated on purpose.** FluentValidation exists for the error message;
the domain enforces the same invariants for the guarantee. If the two ever disagree, the
domain wins and the caller gets a 400 either way.
