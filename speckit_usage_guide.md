# Spec-Kit Usage Guide — First Run (Feature: Auth)

## The correct, official order

```
/speckit.constitution   ← once for the whole project
        ↓
/speckit.specify        ← per feature
        ↓
/speckit.clarify        ← per feature (it asks YOU questions)
        ↓
/speckit.plan           ← per feature
        ↓
/speckit.tasks          ← per feature
        ↓
/speckit.implement      ← per feature — this is the only step that writes code
```

**Golden rule:** review the output of every stage before moving to the next one. If the spec is wrong, the plan built on top of it will be wrong, and the code will be wrong too. Reviewing costs you a few minutes now; it saves you hours of debugging later.

---

## "Shouldn't we make one big plan.md so the AI understands the whole project?"

**Short answer: no — and here's exactly why, so you and the AI are both clear on it.**

In spec-kit, `plan.md` is **not** a whole-project document. It's generated fresh, **per feature**, right after `specify` + `clarify` for that one feature, and it only covers the technical plan for that feature (which classes, which endpoints, which tables it touches). If you tried to write one giant `plan.md` for the entire app before building anything, it would go stale the moment the first feature reveals a detail you didn't anticipate — and spec-kit isn't built to maintain a single evolving mega-plan like that.

**The whole-project understanding comes from two other things, not from plan.md:**

1. **`constitution.md`** (written once, via `/speckit.constitution`) — the non-negotiable rules every feature must follow: architecture, naming, data types, response format, auth strategy. Every later `plan` is checked against this automatically.
2. **The reference files you attach to the project** — `ERD.pdf`, `Backend_Spec.md`, `Backend_Architecture_Skill.pdf`. These give the AI (and you) the full picture of the product, the data model, and the API contract *before* any feature-specific work starts.

So the actual sequence that gives you full-project understanding **and** feature-by-feature safety is:

```
1. Attach ERD.pdf + Backend_Spec.md + Architecture_Skill.pdf to the project (once)
2. Run /speckit.constitution (once) — this is your project-wide "plan," effectively
3. For each feature: specify → clarify → plan → tasks → implement
4. Review the finished feature yourself (run it, test one endpoint)
5. Tell the AI explicitly: "This feature is done and confirmed. Let's move to feature X."
6. Repeat step 3 for the next feature
```

This way, the AI always has the full project context (from the constitution + attached files) without needing one fragile giant plan document, and each feature still gets its own focused, reviewable technical plan.

---

## Step 0 — once only: `/speckit.constitution`

Run this once, before any feature work:

```
We are building a Backend with ASP.NET Core Web API (.NET 8) and SQL Server via EF Core.
Every id and Foreign Key must be of type int (IDENTITY), exactly matching the attached
ERD.pdf — same table and field names, no changes. USERS and CONTACTS use whatsapp_number
only (no phone_number), and USERS has separate first_name and last_name fields.

Architecture: Clean Architecture with four layers (Mizan.Core, Mizan.Application,
Mizan.Infrastructure, Mizan.API). Use Repository + IUnitOfWork (lazy-loaded) for all
database access. Entities must be rich domain models: private setters + factory methods
+ validation inside the entity itself (not in the controller or service). Use custom
exceptions (DomainException, NotFoundException, BadRequestException, ForbiddenException)
with a single middleware that converts them into a unified JSON response shape:
{ "statusCode": ..., "message": "..." }
Use JWT (Access + Refresh Token, max 5 active devices per user) for authentication, with
rate limiting of 5 attempts/minute on /auth endpoints, and Hangfire for any scheduled job.
Use EF Core Fluent API (IEntityTypeConfiguration) for every entity, and separate DTOs per
request with DataAnnotations and Arabic error messages (the app's user-facing language is
Arabic, but code, comments, and identifiers stay in English).
```

**After this runs:** it generates `constitution.md`. Read it once, confirm it matches what you meant, then move on.

---

## Feature 1: Auth (Registration + OTP + JWT)

### Step 1 — `/speckit.specify`

This describes **what and why only** — no technical details:

```
Register a new user using only their WhatsApp number (whatsapp_number), with separate
first and last names (first_name, last_name). After entering the number, the user
receives a verification code (OTP) via WhatsApp. The user enters the code, and if
correct, is logged in. On first login only, the user must choose their type: regular
customer or shop owner — and if shop owner, they also enter a shop name.

The user must be able to stay logged in without re-authenticating constantly, and must
also be able to explicitly log out. If a user has been away for a long time or switches
devices, they should be able to log back in with the same WhatsApp number without
creating a new account.
```

**After this runs:** it generates `spec.md` with user stories and requirements. Read it — if something feels incomplete or ambiguous, don't fix it manually; move to the next step (`clarify`), which is built exactly for that.

---

### Step 2 — `/speckit.clarify`

This command is different — **you don't write a prompt, the AI asks you questions instead.** It analyzes the spec and asks up to 5 questions about anything ambiguous.

```
/speckit.clarify
```

**Anticipated questions and ready answers** (so you can answer confidently on the spot):

| Likely question | Your answer |
|---|---|
| How many digits is the OTP, and how long is it valid? | 6 digits, valid for 120 seconds |
| How many attempts are allowed before requiring a new code? | 3 attempts |
| Access Token lifetime? | 7 days |
| Refresh Token lifetime? | 30 days |
| How many devices can be logged in simultaneously? | 5 devices; the oldest is automatically revoked if exceeded |
| What happens if the WhatsApp number is already registered? | They go straight to the OTP step (login), not a new registration |
| Can the user type (customer/shop_owner) change later? | No, it's fixed after the first choice (can be a separate future feature) |

**After each answer:** `spec.md` gets updated automatically with these clarifications. Skim it again once it's done to confirm it understood you correctly.

---

### Step 3 — `/speckit.plan`

Now, and only now, do we bring in technical details — scoped to this one feature:

```
Use ASP.NET Core Web API (.NET 8) with SQL Server via EF Core Code-First.
Core entity: User (in Mizan.Core) — factory method CreateWithWhatsapp(whatsappNumber,
firstName, lastName) instead of a public constructor, with Egyptian WhatsApp number
validation happening inside the entity itself.

OTP is sent via WhatsApp Cloud API (Meta) — use an interface called IOtpService in
Mizan.Application, implemented in Mizan.Infrastructure.

Authentication: plain JWT (not ASP.NET Identity) — use IJwtTokenGenerator, following the
Repository/UnitOfWork pattern agreed in the constitution. RefreshToken is a separate
entity linked to User, with a Revoke() method.

Controller: AuthController inherits from BaseController, with endpoints:
POST /api/auth/register-or-login (accepts whatsapp number only, returns otpSent)
POST /api/auth/verify-otp (returns JWT + isNewUser flag)
POST /api/auth/select-user-type (Authorize, saves the type on first login only)
POST /api/auth/refresh-token
POST /api/auth/logout (Authorize)

Apply a rate limiting policy named "auth" (5 attempts/minute) to all of the above.
```

**After this runs:** several files are generated (`plan.md`, `data-model.md`, `contracts/`, etc.) — all scoped to this feature only. The two most important to check: `data-model.md` (do the fields match our ERD exactly?) and `contracts/` (does the API shape match what's in `3_Backend_Spec.md`?).

---

### Step 4 — `/speckit.tasks`

No prompt needed, just run it:

```
/speckit.tasks
```

It breaks the plan into small, ordered tasks (e.g., 1. Create User entity, 2. Create UserConfiguration, 3. Create IUserRepository...). **Review the order** — it should be logical: entity first, then repository, then service, then controller last. If anything looks out of order or missing, ask it to fix that before moving on.

---

### Step 5 (final) — `/speckit.implement`

```
/speckit.implement
```

This is where code actually gets written, task by task from `tasks.md`. Once it finishes:

1. Run the project locally (`dotnet run`) and test at least one endpoint yourself (Postman or Swagger) — don't just trust that it works.
2. If it works, `git commit`.
3. If there's an error, paste the **full** error message into the chat — never just say "it's not working."

---

## Once Auth is confirmed working: moving to the next feature

`/speckit.constitution` only runs once (above). The other five steps (`specify` → `clarify` → `plan` → `tasks` → `implement`) repeat **for every new feature**. When you're satisfied Auth actually works, tell the AI explicitly:

```
Auth is confirmed working and tested. Let's move to the next feature: Contacts + Users.
```

Then repeat Steps 1–5 above for that feature. The base `specify` content for the remaining six features is already written out in `3_Backend_Spec_والنشر.md` (Section 7):

1. ✅ Auth — done above
2. Contacts + Users
3. Transactions (text input)
4. Transactions (voice input)
5. Installments + Payments
6. Reminders (Hangfire)
7. Periodic Reports (QuestPDF)

---

## Quick reference (keep this handy)

| Command | Takes real input from you? | Repeats per feature? |
|---|---|---|
| `/speckit.constitution` | Yes — once for the whole project | ❌ |
| `/speckit.specify` | Yes — the functional description | ✅ |
| `/speckit.clarify` | It asks you instead | ✅ |
| `/speckit.plan` | Yes — the technical details | ✅ |
| `/speckit.tasks` | No — just run it | ✅ |
| `/speckit.implement` | No — just run it and review | ✅ |

Review every stage before the next one, and don't rush. The first feature (Auth) will take longer than normal simply because you're still learning the tool — the second and third will go noticeably faster.
