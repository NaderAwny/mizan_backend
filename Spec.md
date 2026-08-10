# ميزان — مواصفة الـ Backend الكاملة + خطوات النشر

> **الغرض من الملف ده:** ده الملف اللي هتديه لـ AI (Antigravity + spec-kit) عشان يبني الـ backend بالكامل بـ .NET، ويطلعلك نفس النتيجة اللي انت متخيلها بالظبط. كل حاجة فيه — من وصف المنتج لحد آخر خطوة في النشر — مبنية على أربع ملفات: `1_ERD_قاعدة_البيانات.pdf`، `2_تصميم_التطبيق.pdf`، `4_Backend_Architecture_Skill.pdf`، والملف ده. ارفقهم كلهم مع بعض لـ Antigravity.
>
> **آخر تحديث:** الـ USERS بقت تستخدم `whatsapp_number` بس (اتشال `phone_number`)، و`full_name` اتقسم لـ `first_name` و`last_name`. نفس التغيير اتطبق على `CONTACTS` (بقت `whatsapp_number` بدل `phone_number`) لأن كل التواصل في التطبيق أساسه واتساب.

---

## 1. نظرة عامة على المنتج (Context للـ AI)

**الاسم:** ميزان
**النوع:** تطبيق SaaS لإدارة الديون، المشتريات، الأقساط، والمبيعات — مبني لموبايل (Flutter) مع Backend بـ .NET.

**الفكرة الأساسية:**
- المستخدم يسجل برقم الواتساب بس (مفيش رقم هاتف منفصل)، ويتحدد نوعه: **مستخدم عادي** (بيتابع مشترياته وأقساطه) أو **صاحب محل** (بيتابع مبيعاته وديون عملائه).
- المستخدم بيسجل أي عملية (بيع/شراء) إما **كتابة** أو **صوت** (بيتحول تلقائيًا لنص وبيانات منظمة).
- كل عملية بتولّد إشعار فوري على التطبيق + رسالة واتساب.
- لو العملية فيها تقسيط، النظام بيتابع الأقساط ويبعت تذكير في يوم الاستحقاق (تطبيق + واتساب).
- كل عدد معين من العمليات (مثلاً كل 7 أو 10)، النظام بيولّد ملخص PDF أو صورة ويبعته على واتساب المستخدم تلقائيًا.

**الأدوار:**
| المسؤول | المهمة |
|---|---|
| صاحبك | Backend بـ .NET + قاعدة البيانات + الـ API |
| انت | واجهة Flutter + ربط الـ API |
| AI (Antigravity + spec-kit) | تنفيذ كود الـ Backend فعليًا حسب المواصفة دي |

---

## 2. تدفق التطبيق الكامل (End-to-End Flow)

ده التدفق اللي التصميم (ملف 2) بيمثله بصريًا، والـ ERD (ملف 1) بيمثله كبيانات. كل خطوة هنا بتوضح الشاشة + العملية اللي بتحصل في الـ backend وراها.

### أ. التسجيل وتحديد النوع
1. المستخدم يدخل رقم الواتساب → الـ Backend يبعت OTP عبر WhatsApp Cloud API.
2. المستخدم يدخل الكود → الـ Backend يتحقق منه، ولو صح يطلع JWT token.
3. أول مرة بس: المستخدم يختار نوعه (`customer` أو `shop_owner`) → يتسجل في جدول `USERS`.
4. لو `shop_owner`، يتملى بيانات محل إضافية → يتسجل في جدول `SHOPS`.

### ب. تسجيل عملية (بيع/شراء)
1. المستخدم يختار أو يضيف طرف تاني (`CONTACTS`).
2. يدخل البيانات كتابة، أو يسجل صوت → الصوت يترفع للـ Backend → يتحول لنص عبر Speech-to-Text → يتحول لبيانات منظمة (مبلغ، وصف، طرف).
3. لو العملية تقسيط، يحدد عدد الأقساط والمواعيد → يتسجل سجل في `INSTALLMENTS`.
4. عند الحفظ: يتسجل `TRANSACTIONS`، ويتولد `NOTIFICATIONS` فوري، وتتبعت رسالة واتساب.

### ج. التذكيرات (Background Job يومي)
1. Job مجدول (Hangfire) بيشتغل كل يوم، بيدور على `INSTALLMENTS` و`REMINDERS` اللي تاريخها = النهارده.
2. لكل واحدة، يتبعت إشعار على التطبيق + رسالة واتساب، ويتسجل في `NOTIFICATIONS`.

### د. التقرير الدوري
1. بعد كل حفظ عملية، الـ Backend يعد عدد عمليات المستخدم منذ آخر تقرير.
2. لو العدد وصل للحد المطلوب (مثلاً 7)، يتولد ملف PDF/صورة (QuestPDF) فيه ملخص العمليات، ويتسجل في `PERIODIC_REPORTS`، ويتبعت على واتساب.

### هـ. متابعة الأقساط
1. المستخدم يفتح تفاصيل عملية → يشوف جدول الأقساط وحالتها (مدفوع/مستحق النهارده/لسه).
2. لما يدفع قسط، يتسجل في `INSTALLMENT_PAYMENTS`، ويتحدث `remaining_amount` وحالة `INSTALLMENTS`.

---

## 3. المواصفات التقنية (Tech Stack)

| الغرض | الأداة |
|---|---|
| Backend Framework | ASP.NET Core Web API (.NET 8) |
| قاعدة البيانات | SQL Server (MSSQL) |
| ORM | Entity Framework Core (Code-First, Migrations) |
| المصادقة | JWT + OTP (عبر WhatsApp Cloud API) |
| رسائل واتساب | WhatsApp Cloud API (Meta) |
| تحويل الصوت لنص | Whisper API (أو نسخة self-hosted) |
| توليد PDF | QuestPDF |
| جدولة المهام | Hangfire |
| إشعارات داخل التطبيق | Firebase Cloud Messaging |

---

## 3.5 المعمارية (مرجع من ملف Skill)

> **مرفق `4_Backend_Architecture_Skill.pdf`؟** ده مستخرج من ريبو حقيقي شغال (Backend صاحبي يوسف)، وهو الأساس اللي مبني عليه القسم ده.

Backend ميزان هيتبني بنفس بنية Clean Architecture، بأربع طبقات:

| الطبقة | المحتوى |
|---|---|
| `Mizan.Core` | Entities + Exceptions + Interfaces — مفيهاش أي اعتماد على طبقة تانية |
| `Mizan.Application` | Services + DTOs + Interfaces بتاعة الخدمات الخارجية (WhatsApp، Whisper) |
| `Mizan.Infrastructure` | DbContext + Repositories + التنفيذ الفعلي للخدمات الخارجية |
| `Mizan.API` | Controllers + Middlewares + Program.cs |

**أهم الأنماط المطلوب اتباعها (تفاصيلها الكاملة في ملف Skill):**
- Repository + `IUnitOfWork` (lazy-loaded) لكل وصول لقاعدة البيانات.
- Entities غنية بمنطق العمل: `private set` + Factory Methods (زي `User.CreateManual(...)`) + validation جوه الـ Entity نفسها.
- Exceptions مخصصة (`DomainException`, `NotFoundException`, `BadRequestException`, `ForbiddenException`) مع Middleware واحد يحولهم لـ HTTP response موحد.
- `BaseController` فيه `Success()` / `Created()` helpers، و`CurrentUserId` بيتقرا من الـ JWT مباشرة.
- EF Core Fluent API (`IEntityTypeConfiguration<T>`) لكل Entity — مش Data Annotations على الـ Entity نفسها.
- DTOs منفصلة لكل Request مع `DataAnnotations` ورسائل خطأ بالعربي.

---

## 4. نموذج البيانات (مرجع من ملف الـ ERD)

كل الـ `id` و الـ Foreign Keys من نوع **int** (auto-increment / IDENTITY في قاعدة البيانات). الجداول:

`USERS`, `SHOPS`, `CONTACTS`, `TRANSACTIONS`, `INSTALLMENTS`, `INSTALLMENT_PAYMENTS`, `REMINDERS`, `NOTIFICATIONS`, `PERIODIC_REPORTS`

> **مهم:** ارفق ملف `1_ERD_قاعدة_البيانات.pdf` مع الملف ده لما تدّيهم لـ AI — فيه كل الحقول بالتفصيل، والـ AI هيستخدمه لبناء الـ Entity classes بالظبط.

---

## 5. عقد الـ API الكامل (Request / Response)

### Auth
```
POST /api/auth/register
Body: { "whatsappNumber": "01012345678", "firstName": "محمد", "lastName": "أحمد" }
Response: { "otpSent": true, "expiresInSeconds": 120 }

POST /api/auth/verify-otp
Body: { "whatsappNumber": "01012345678", "code": "482913" }
Response: { "token": "jwt...", "isNewUser": true }

POST /api/auth/select-user-type
Headers: Authorization: Bearer {token}
Body: { "userType": "customer" | "shop_owner", "shopName": "اختياري لو shop_owner" }
Response: { "success": true, "userId": 12 }
```

### Users
```
GET /api/users/me
Response: { "id": 12, "firstName": "محمد", "lastName": "أحمد", "whatsappNumber": "...", "userType": "customer" }
```

### Contacts
```
POST /api/contacts
Body: { "name": "أحمد سمير", "whatsappNumber": "01099998888" }
Response: { "id": 4, "name": "أحمد سمير" }

GET /api/contacts
Response: [{ "id": 4, "name": "أحمد سمير", "whatsappNumber": "..." }]
```

### Transactions
```
POST /api/transactions
Body: {
  "contactId": 4,
  "type": "sale" | "purchase",
  "amount": 6000,
  "description": "موبايل",
  "inputMethod": "text" | "voice",
  "isInstallment": true,
  "installmentsCount": 3,
  "firstDueDate": "2026-09-01"
}
Response: { "id": 101, "status": "pending", "installmentId": 15 }

POST /api/transactions/voice   (multipart/form-data: audio file)
Response: {
  "transcript": "بعت لأحمد سمير موبايل بـ 6000 جنيه على 3 أقساط",
  "parsed": { "contactName": "أحمد سمير", "amount": 6000, "installmentsCount": 3 }
}

GET /api/transactions?type=sale&status=pending&page=1
Response: [{ "id": 101, "contactName": "أحمد سمير", "amount": 6000, "status": "pending" }]

GET /api/transactions/{id}
Response: { ...تفاصيل كاملة + جدول الأقساط لو موجود }
```

### Installments
```
POST /api/installments/{id}/pay
Body: { "amountPaid": 2000, "paymentDate": "2026-08-09" }
Response: { "remainingAmount": 4000, "status": "partially_paid" }
```

### Reminders
```
GET /api/reminders?range=today|week|later
Response: [{ "id": 9, "contactName": "سارة محمود", "amount": 950, "dueDate": "...", "overdue": true }]

POST /api/reminders/{id}/send-whatsapp
Response: { "sent": true }
```

### Reports
```
GET /api/reports/{userId}
Response: [{ "id": 3, "fileUrl": "...", "periodStart": "...", "periodEnd": "...", "transactionCount": 7 }]
```

### Notifications
```
GET /api/notifications
Response: [{ "id": 55, "message": "...", "channel": "app" | "whatsapp" | "both", "createdAt": "..." }]
```

**كل الـ responses للأخطاء بنفس الشكل** (نفس النمط المستخدم في ريبو صاحبي — راجع ملف Skill):
```
{ "statusCode": 400, "message": "الكود غير صحيح أو منتهي" }
```

---

## 6. قواعد العمل (Business Rules) — مهم جدًا تديها للـ AI

- كود الـ OTP: 6 أرقام، صالح لمدة **120 ثانية**، أقصى 3 محاولات قبل ما يتطلب إعادة إرسال.
- الـ JWT token صالح لمدة **7 أيام**، مع Refresh Token صالح 30 يوم.
- التقرير الدوري بيتولد تلقائيًا كل ما `transaction_count` منذ آخر تقرير يوصل لرقم قابل للتعديل (افتراضي: **7**)، ومتخزن كـ setting لكل مستخدم مش رقم ثابت في الكود.
- حالة `INSTALLMENTS.status` بتتغير تلقائيًا: `pending` → `partially_paid` → `paid` حسب مجموع `INSTALLMENT_PAYMENTS`.
- التذكير بيتبعت **مرة واحدة بس** في يوم الاستحقاق (لازم فحص إن مفيش تذكير اتبعت قبل كده لنفس القسط في نفس اليوم).
- لو `linked_user_id` في `CONTACTS` موجود (يعني الطرف التاني مسجل في التطبيق فعلاً)، الإشعار يتبعت له كـ Push notification جوه التطبيق مش بس واتساب.

---

## 7. استخدام spec-kit — النصوص الجاهزة

ابدأ بالـ constitution، وبعدين فيتشر فيتشر بنفس الترتيب اللي في القسم 8. لكل فيتشر، انسخ النص المقابل في `/speckit.specify`.

### `/speckit.constitution`
```
هنبني Backend بـ ASP.NET Core Web API (.NET 8) مع SQL Server عبر EF Core.
كل الـ id و Foreign Keys لازم تكون من نوع int (IDENTITY)، زي ملف ERD.md المرفق بالظبط —
نفس أسماء الجداول والحقول من غير أي تغيير. USERS و CONTACTS بيستخدموا whatsapp_number
بس (مفيش phone_number)، و USERS فيها first_name و last_name منفصلين.

المعمارية: Clean Architecture بأربع طبقات (Mizan.Core, Mizan.Application,
Mizan.Infrastructure, Mizan.API). استخدم Repository + IUnitOfWork (lazy-loaded) لكل
وصول لقاعدة البيانات. الـ Entities تكون غنية بمنطق العمل: private setters + Factory
Methods + validation جوه الـ Entity نفسها (زي التحقق من رقم الواتساب المصري). استخدم
Exceptions مخصصة (DomainException, NotFoundException, BadRequestException,
ForbiddenException) مع Middleware واحد يحولهم لرد JSON موحد بالشكل:
{ "statusCode": ..., "message": "..." }
استخدم JWT (Access + Refresh Token، حد أقصى 5 أجهزة متصلة) للمصادقة، مع Rate Limiting
5 محاولات/دقيقة على /auth endpoints، و Hangfire لأي مهمة مجدولة. استخدم EF Core Fluent
API (IEntityTypeConfiguration) لكل Entity، وDTOs منفصلة لكل Request مع DataAnnotations
ورسائل خطأ بالعربي.
```

### فيتشر 1 — Auth
```
سجل مستخدم جديد برقم الواتساب بس (whatsapp_number — من غير phone_number منفصل)، مع first_name
و last_name منفصلين. ابعت OTP عبر WhatsApp Cloud API (كود 6 أرقام، صالح 120 ثانية، أقصى 3 محاولات).
بعد التحقق يتطلع JWT Access Token صالح 7 أيام + Refresh Token صالح 30 يوم، مع حد أقصى 5 أجهزة
متصلة في نفس الوقت (زي ما هو متبع في Backend صاحبي يوسف — لو زاد العدد، أقدم Refresh Token يتلغي).
أول مرة، المستخدم يختار نوعه: customer أو shop_owner، ولو shop_owner يدخل بيانات محل.
Rate Limiting على endpoints الـ Auth: 5 محاولات في الدقيقة.
```

### فيتشر 2 — Contacts + Users
```
CRUD بسيط لجهات الاتصال (CONTACTS) مرتبطة بالمستخدم الحالي. Endpoint لعرض بيانات المستخدم الحالي (GET /users/me).
```

### فيتشر 3 — Transactions (نص)
```
تسجيل عملية بيع/شراء بالنص: طرف تاني، نوع، مبلغ، وصف، واختياريًا تقسيط (عدد أقساط + تاريخ أول استحقاق).
لو تقسيط، اتسجل في INSTALLMENTS مرتبطة بالعملية. عند الحفظ، ولّد إشعار في NOTIFICATIONS، وابعت رسالة واتساب.
```

### فيتشر 4 — Transactions (صوت)
```
Endpoint يستقبل ملف صوتي (multipart/form-data)، يبعته لـ Whisper API لتحويله لنص، وبعدين يستخرج منه
(اسم الطرف، المبلغ، عدد الأقساط لو موجود) ويرجعهم كمعاينة للمستخدم قبل الحفظ النهائي.
```

### فيتشر 5 — Installments + Payments
```
Endpoint لتسجيل دفعة قسط (POST /installments/{id}/pay). حدّث remaining_amount وstatus تلقائيًا
(pending → partially_paid → paid) حسب مجموع الدفعات.
```

### فيتشر 6 — Reminders (Hangfire)
```
Background Job يومي (Hangfire) يفحص كل الأقساط المستحقة النهارده، ويبعت تذكير واحد بس لكل قسط
(تطبيق + واتساب)، ويسجله في REMINDERS و NOTIFICATIONS.
```

### فيتشر 7 — Periodic Reports (QuestPDF)
```
بعد كل عملية جديدة، اعد عدد العمليات منذ آخر تقرير لنفس المستخدم. لو وصل لحد قابل للتعديل (افتراضي 7)،
ولّد ملف PDF عبر QuestPDF فيه ملخص العمليات والمبالغ، وابعته على واتساب المستخدم، وسجله في PERIODIC_REPORTS.
```

**بعد كل فيتشر:** شغّله، اختبره، اعمل `git commit`، وبعدين انتقل للي بعده.

---

## 8. خطوات البناء والنشر بالترتيب الكامل

### المرحلة 1 — الإعداد المحلي
1. نزّل .NET 8 SDK.
2. استخدم **SQL Server** (سواء كان محلياً عبر SQL Server Express / LocalDB أو Docker، أو سحابياً عبر Azure SQL Database) وضع الـ connection string.
3. اعمل git repo، وافتحه في Antigravity.
4. حط ملفات `1_ERD_قاعدة_البيانات.pdf`، `4_Backend_Architecture_Skill.pdf`، وهذا الملف نفسه في الـ repo كمرجع دائم (اعمل منهم نسخة `.md` بسيطة لو الأداة مش بتقرأ PDF مباشرة).
5. شغّل `specify init` (زي ما اتفقنا قبل كده) وابدأ بـ `/speckit.constitution`.

### المرحلة 2 — بناء الفيتشرز
6. امشِ بالفيتشرز السبعة في القسم 7، فيتشر فيتشر، وبعد كل واحد commit.

### المرحلة 3 — التكاملات الخارجية
7. اعمل حساب WhatsApp Business على Meta for Developers، وفعّل WhatsApp Cloud API، وهات الـ access token.
8. اعمل حساب على OpenAI (أو بديل مجاني) عشان Whisper API.
9. حط كل الـ secrets دي (connection string، WhatsApp token، JWT secret) في **متغيرات بيئة (Environment Variables)** مش في الكود مباشرة.

### المرحلة 4 — النشر على سيرفر مجاني
10. اعمل حساب على **Render.com** أو **Railway.app**.
11. اربط الـ GitHub repo بتاعك بالمنصة (بتدعم Auto-deploy عند كل push).
12. ضيف نفس متغيرات البيئة من المرحلة 3 في إعدادات المنصة.
13. اعمل Deploy، وهتاخد رابط API عام زي: `https://mizan-api.onrender.com`

### المرحلة 5 — ربط Flutter
14. في تطبيق Flutter، حط الرابط ده في مكان مركزي (زي `ApiConfig.baseUrl`) مش متفرق في الكود.
15. اختبر كل endpoint من Flutter فعليًا (مش mock data) قبل ما تعتبر الفيتشر خلص.
16. كرر الخطوة دي مع كل فيتشر جديد يتضاف في الـ Backend.

### المرحلة 6 — الجاهزية للنشر (Play Store)
17. تأكد إن كل الـ endpoints بترجع أخطاء واضحة (مش 500 عام) عشان تقدر تعرضها صح في Flutter.
18. جرب التطبيق كامل مع 2-3 مستخدمين حقيقيين (بيتا) قبل النشر النهائي.

---

## ملاحظة أخيرة

الملفات الأربعة دي مع بعض — الـ ERD، التصميم، ملف الـ Architecture Skill، والملف ده — كل اللي محتاجه عشان تبدأ فعليًا النهارده. لو AI (Antigravity) طلع نتيجة مختلفة عن المتوقع في أي فيتشر، ارجع لنص الـ specify بتاع الفيتشر ده في القسم 7 وتأكد إنه مطابق تمامًا لما هو موصوف هنا، ولملف الـ Skill بالنسبة للمعمارية، قبل ما تكمل.
