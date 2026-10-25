# ميزة: لوحة متابعة الأقساط والديون الشاملة (Installments & Debts Dashboard)

> **موجّه إلى:** وكيل Antigravity الذي يعمل على مستودع `mizan_backend` (.NET 9, Clean Architecture).
> **الهدف:** إضافة ميزة جديدة بدون كسر أو تعديل أي Endpoint موجود حالياً، بنفس الأسلوب والبنية والشكل المستخدم فعلياً فى الميزات السابقة (خصوصاً ميزة **الإحصائيات الفورية** وميزة **الأقساط والسداد**).
> **هذا الكود سيُرفع مباشرة على السيرفر بدون اختبار محلي من المستخدم — الدقة والالتزام الحرفي بالبنية الحالية أهم من السرعة.**

---

## 0) خطوة إلزامية قبل كتابة أي سطر كود

قبل البدء، افحص المشروع الحالي وطابق كل ما يلي **حرفياً** (لا تخترع أسماء جديدة أو Response Shape مختلف):

1. اقرأ `Mizan.Core` (الكيانات: `Installment`, `Transaction`, `Contact`, `Shop`, ...) واستخرج الحقول الفعلية بالضبط (أسماء، أنواع، Enums الخاصة بحالة القسط/العملية).
2. اقرأ `Mizan.Application` (DTOs الحالية الخاصة بـ Installments/Transactions/Contacts، و `IInstallmentService` أو ما يعادلها، وكل الـ Interfaces والـ Validators المستخدمة).
3. اقرأ `Mizan.Infrastructure` (طريقة كتابة الـ Repositories، `UnitOfWork`، أسلوب استعلامات EF Core Fluent API، وكيف يتم تحديد `ShopId`/`UserId` الحالي من الـ Context).
4. اقرأ `Mizan.API/Controllers/InstallmentsController.cs` و `StatisticsController.cs` تحديداً — وطابق:
   - شكل الـ Response الموحّد: `{ "success": bool, "message": string|null, "data": ... }`
   - شكل الـ Pagination الموحّد: `{ "items": [...], "totalCount": n, "page": n, "pageSize": n, "totalPages": n }`
   - أسلوب معالجة الأخطاء عبر `ExceptionHandlingMiddleware` / `DomainException` / `BadRequestException` الموجود بالفعل.
   - أسلوب الحماية `[Authorize]` واستخراج المستخدم/المحل من الـ JWT Claims كما هو مستخدم فعلياً فى بقية الـ Controllers.
5. **لا تُعدّل** أي Endpoint أو DTO أو منطق موجود حالياً فى الأقساط أو العمليات أو الإحصائيات. هذه ميزة **إضافية فقط (additive)**.

إذا وجدت اختلافاً بين ما هو مكتوب هنا وبين الكود الفعلي فى أسماء الحقول/الـ Enums، **اتّبع الكود الفعلي** واستخدم نفس تسمياته، واذكر فى تعليق أعلى الملف الجديد أنك عدّلت التسمية لتطابق الكود القائم.

---

## 1) وصف الميزة (Functional Requirement)

المستخدم (صاحب المحل) يحتاج **مكاناً واحداً** يجمع له كل الأقساط والديون الخاصة بكل عملائه (مش عميل لوحده زي بروفايل العميل الحالي)، بحيث يشوف فيه:

- إجمالي كل الأقساط/الديون القائمة عليه (المبلغ الكلي).
- إجمالي المُسدَّد من هذا المبلغ، والمتبقي، ونسبة السداد المئوية.
- كل خطة قسط نشطة (لكل عميل/عملية): المبلغ الكلي، المسدَّد، المتبقي، عدد الأقساط المدفوعة من إجمالي عددها، وموعد الدفعة القادمة.
- **تنبيه واضح** لأي قسط:
  - **متأخر السداد (Overdue):** تاريخ استحقاقه فات ولسه مش متسدد.
  - **مستحق اليوم (Due Today):** تاريخ استحقاقه هو نفس تاريخ اليوم ولسه مش متسدد.
- تاريخ/سجل الأقساط بشكل عام (Timeline كامل مش بس الملخص)، قابل للفلترة (الكل / متأخر / مستحق اليوم / قادم / مدفوع بالكامل) والتصفح بصفحات.

هذا يطابق تصميم الشاشة المرفقة (تتبع الأقساط): دائرة نسبة مئوية + إجمالي الأقساط القائمة + قائمة خطط نشطة، وكل خطة بها Progress Bar ووسم أحمر "متأخر السداد" عند التأخير.

---

## 2) الـ Endpoints الجديدة

أضِفها داخل نفس `InstallmentsController` الحالي (لأنها امتداد منطقي لنفس الـ Resource)، تحت نفس الـ Route Prefix `/api/installments`.

### 2.1 — `GET /api/installments/dashboard`

**الوظيفة:** ملخص شامل لكل الأقساط/الديون الخاصة بالمستخدم الحالي (كل عملائه)، مجمّعة على مستوى كل "خطة قسط" (أي Transaction له جدول أقساط).

**بدون Query params إجبارية.** (اختياري: `asOf` بصيغة تاريخ لعمل محاكاة "كأن اليوم كذا" — لو غير موجود فى باقي الكود اتجاهله ولا تضِفه).

**مثال Response (200 OK):**
```json
{
  "success": true,
  "message": null,
  "data": {
    "totalDebtAmount": 200000.00,
    "totalPaidAmount": 85000.00,
    "totalRemainingAmount": 115000.00,
    "paidPercentage": 42.5,
    "activePlansCount": 2,
    "overduePlansCount": 1,
    "dueTodayCount": 0,
    "plans": [
      {
        "transactionId": "c1356f91-2a3b-4c5d-6e7f-8a9b0c1d2e3f",
        "contactId": "7b0a7cb5-8d9e-4a1b-bc3d-1e2f3a4b5c6d",
        "contactName": "شركة سهيلات للتمويل",
        "phoneNumber": "01012345678",
        "totalAmount": 100000.00,
        "paidAmount": 65000.00,
        "remainingAmount": 35000.00,
        "paidPercentage": 65.0,
        "installmentsCount": 10,
        "paidInstallmentsCount": 6,
        "nextDueDate": "2026-10-05",
        "nextDueAmount": 10000.00,
        "status": "OnTrack",
        "isOverdue": false,
        "isDueToday": false,
        "daysOverdue": 0
      },
      {
        "transactionId": "d2467a02-3b4c-5d6e-7f8a-9b0c1d2e3f4a",
        "contactId": "8c1b8dc6-9e0f-5b2c-cd4e-2f3a4b5c6d7e",
        "contactName": "بنك الإنماء الوطني",
        "phoneNumber": "01122334455",
        "totalAmount": 100000.00,
        "paidAmount": 20000.00,
        "remainingAmount": 80000.00,
        "paidPercentage": 20.0,
        "installmentsCount": 10,
        "paidInstallmentsCount": 2,
        "nextDueDate": "2026-01-28",
        "nextDueAmount": 20000.00,
        "status": "Overdue",
        "isOverdue": true,
        "isDueToday": false,
        "daysOverdue": 12
      }
    ]
  }
}
```

**ملاحظات:**
- `status` من نفس نوع الـ Enum الذي يقرر Overdue/DueToday/OnTrack/Completed — لو فيه Enum مشابه مستخدم بالفعل فى الأقساط استخدمه بنفس القيم، وإلا أنشئ Enum جديد بنفس أسلوب تسمية الـ Enums الحالية فى المشروع.
- ترتيب `plans`: المتأخر أولاً، ثم المستحق اليوم، ثم الأقرب استحقاقاً.
- الحساب يشمل فقط الأقساط الخاصة بالمستخدم/المحل الحالي (نفس منطق العزل المستخدم فعلياً فى كل الـ Endpoints الأخرى).

### 2.2 — `GET /api/installments/history` (أو الاسم المطابق لو فيه Endpoint قريب منه بالفعل باسم مختلف — لو موجود لا تكرره، وسّعه)

**الوظيفة:** سجل/تايم لاين بكل الأقساط الفردية (مش مجمّعة على مستوى خطة) عبر كل العملاء، قابل للفلترة والتصفح.

**Query params:**
| الحقل | إجباري؟ | الوصف |
|---|---|---|
| `status` | لا (افتراضي `all`) | `all` \| `overdue` \| `dueToday` \| `upcoming` \| `paid` |
| `page` | لا (افتراضي 1) | رقم الصفحة |
| `pageSize` | لا (افتراضي 20) | 1–50 |

**مثال Response (200 OK):**
```json
{
  "success": true,
  "message": null,
  "data": {
    "items": [
      {
        "installmentId": "e3578b13-4c5d-6e7f-8a9b-0c1d2e3f4a5b",
        "transactionId": "d2467a02-3b4c-5d6e-7f8a-9b0c1d2e3f4a",
        "contactId": "8c1b8dc6-9e0f-5b2c-cd4e-2f3a4b5c6d7e",
        "contactName": "بنك الإنماء الوطني",
        "amount": 20000.00,
        "dueDate": "2026-01-28",
        "isPaid": false,
        "paidAt": null,
        "status": "Overdue",
        "isOverdue": true,
        "isDueToday": false,
        "daysOverdue": 12
      }
    ],
    "totalCount": 14,
    "page": 1,
    "pageSize": 20,
    "totalPages": 1
  }
}
```

> إذا كان هناك بالفعل Endpoint لعرض كل الأقساط (list) فى `InstallmentsController`، **وسّعه** بدعم query param اسمه `status` بنفس القيم أعلاه بدل إنشاء Endpoint مكرر، وأضِف الحقول الناقصة (`isOverdue`, `isDueToday`, `daysOverdue`, `status`) على نفس الـ DTO الموجود.

---

## 3) منطق تحديد حالة القسط (مهم جداً — طبّقه فى مكان واحد فقط فى الكود، لا تكرره)

اعمله كـ Extension Method أو Helper واحد فى `Mizan.Application` يُستخدم فى كلا الـ Endpoint الجديدين (لتفادي التكرار وأي تعارض):

- `IsPaid == true` → `status = "Paid"`.
- `IsPaid == false` و `DueDate.Date < Today.Date` → `status = "Overdue"`, `isOverdue = true`, `daysOverdue = (Today.Date - DueDate.Date).Days`.
- `IsPaid == false` و `DueDate.Date == Today.Date` → `status = "DueToday"`, `isDueToday = true`.
- `IsPaid == false` و `DueDate.Date > Today.Date` → `status = "Upcoming"`.

استخدم نفس مصدر "اليوم الحالي" (UTC أو التوقيت المحلي) المستخدم بالفعل فى باقي حسابات التواريخ بالمشروع (مثلاً فى ميزة الإحصائيات الفورية) للحفاظ على الاتساق — لا تخترع مصدر توقيت جديد.

خطة القسط (Plan) فى Endpoint الـ Dashboard تأخذ حالتها من **أسوأ حالة** بين أقساطها الفرعية (لو فيه قسط Overdue واحد، الخطة كلها Overdue).

---

## 4) أين يوضع الكود (طبقات المعمارية)

- **Mizan.Application**
  - DTOs جديدة: `InstallmentsDashboardDto`, `ActiveInstallmentPlanDto`, `InstallmentHistoryItemDto` (أو أسماء مطابقة لأسلوب التسمية الموجود فعلاً فى مجلد DTOs الخاص بالأقساط).
  - إضافة Methods جديدة على الـ Service/Interface الحالي الخاص بالأقساط (`IInstallmentService`) بدل إنشاء Service منفصل، إلا لو المشروع بالفعل يفصل كل ميزة فى Service مستقل (فى هذه الحالة اتبع نفس النمط مثل `IStatisticsService`).
  - Helper منطق الحالة (البند 3).
- **Mizan.Infrastructure**
  - استعلام EF Core واحد فعّال (Projection مباشر بـ `Select` بدون تحميل كيانات زيادة) يجمع الأقساط بالـ `ShopId`/`UserId` الحالي، مع `GroupBy` على `TransactionId` لحساب المجاميع لكل خطة. تجنب N+1 queries.
- **Mizan.API**
  - Action جديدة `GetDashboard()` و`GetHistory(...)` داخل `InstallmentsController` الحالي، بنفس أسلوب الـ Attributes والـ Authorization والتوثيق (XML comments / Swagger) المستخدم فى باقي الـ Actions.

---

## 5) تحديث مجموعة Postman (`Mizan_API.postman_collection.json`)

أضِف عنصرين جديدين داخل مجلد **"📅 5. إدارة وتسديد الأقساط (Installments & Payments)"** الموجود بالفعل، بنفس ترقيم الفولدر (مثلاً `5.x` و`5.y` بعد آخر عنصر موجود فيه)، وبنفس البنية الحرفية للعناصر الأخرى فى نفس الملف:
- نفس شكل `event.listen: "test"` مع `pm.test(...)`.
- نفس شكل حقل `description` (Markdown بعناوين: 📌 الوظيفة، 📥 المدخلات، 💡 سيناريوهات، ⚠️ أخطاء شائعة، 🎯 الاستجابة المتوقعة).
- نفس شكل `response` array بأمثلة 200/400/404 كأمثلة Preview.

لا تُنشئ مجلداً جديداً منفصلاً للميزة — هي امتداد طبيعي لمجلد الأقساط الموجود.

---

## 6) اختبارات الوحدة (`tests/Mizan.UnitTests`)

أضِف اختبارات لِـ:
1. حساب `status` الصحيح لكل الحالات الأربع (Paid / Overdue / DueToday / Upcoming) — خصوصاً حافة اليوم بالظبط (Due Today) وحافة التأخير بيوم واحد.
2. أن `totalDebtAmount = SUM(كل الأقساط)`, `totalPaidAmount = SUM(المدفوع)`, و`paidPercentage` محسوبة صح مع حالة القسمة على صفر (مفيش أقساط أصلاً → 0% مش استثناء).
3. أن الخطة تاخد حالة "الأسوأ" بين أقساطها الفرعية.
4. أن العزل حسب `ShopId`/`UserId` يعمل صح (مستخدم تاني ميشوفش أقساط غيره).
5. الترتيب فى `plans` (متأخر أولاً).

---

## 7) معايير القبول (Acceptance Criteria)

- [ ] لا يوجد أي تعديل على أي Endpoint أو Response موجود سابقاً.
- [ ] `GET /api/installments/dashboard` يرجّع نفس شكل الـ Envelope الموحّد `{success, message, data}`.
- [ ] الأرقام (`totalDebtAmount`, `totalPaidAmount`, `totalRemainingAmount`, `paidPercentage`) صحيحة رياضياً ومتسقة مع بعضها.
- [ ] القسط المستحق اليوم يظهر `isDueToday = true` وليس `isOverdue`.
- [ ] القسط المتأخر يظهر `isOverdue = true` و`daysOverdue` صحيح.
- [ ] `GET /api/installments/history` يدعم الفلترة بكل قيم `status` المذكورة، والتصفح بصفحات بنفس شكل الـ Pagination الموحّد.
- [ ] الأداء: استعلام واحد (أو الحد الأدنى من الاستعلامات) بدون N+1، مع Indexes مناسبة لو لزم (راجع الجدول الحالي قبل إضافة أي Index جديد).
- [ ] تمت إضافة عناصر Postman الجديدة بنفس الأسلوب الحرفي للملف.
- [ ] كل الاختبارات الحالية فى `Mizan.UnitTests` ما زالت تنجح (`dotnet test`) + الاختبارات الجديدة تنجح.
- [ ] `dotnet build` بدون أي Warning جديد.

---

## 8) ملاحظة أخيرة

هذه الميزة **قراءة فقط (Read-only)** — لا تضيف أي منطق كتابة/تعديل جديد على الأقساط (تسجيل السداد نفسه يتم بالفعل عبر Endpoint القائم `POST /api/installments/{id}/pay` ولا داعي لمسّه). دور هذه الميزة هو التجميع والعرض فقط.
