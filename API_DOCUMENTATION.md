# دليل توثيق واجهة برمجة التطبيقات — Mizan API Documentation (v1)

توثيق شامل ومفصل لجميع الميزات التسع (9 Features) ونقاط النهاية (Endpoints) في نظام **ميزان (Mizan)** لإدارة المعاملات المالية، الديون، الأقساط، التذكيرات الذكية، والتقارير الدورية.

---

## 🌟 القواعد المعمارية العامة (Global Standards)

1. **صيغة المعرفات (UUID v4 / Guid):**
   - جميع المعرفات (Primary Keys و Foreign Keys) في النظام هي سلاسل نصية بصيغة `UUID v4` متوافقة مع معيار RFC 4122 (مثال: `3fa85f64-5717-4562-b3fc-2c963f66afa6`).
   - جميع المسارات التي تستقبل معرفات تستخدم قيد الـ Guid الصريح: `[HttpGet("{id:guid}")]`.
2. **تمثيل الـ Enums كنصوص (String Enums):**
   - ترجع جميع الـ Enums في الـ Responses كنصوص صريحة (مثال: `"type": "Sale"`, `"status": "Pending"`).
   - تقبل جميع الـ Requests قيم الـ Enums كنصوص فقط. **إرسال رقم (مثل `0` أو `1`) يُرفض فوراً بـ 400 Bad Request**.
3. **الأمان وعزل البيانات (Multi-Tenant User Isolation):**
   - جميع المسارات المحمية تتطلب ترويسة `Authorization: Bearer {token}`.
   - في حال طلب مورد يخص مستخدماً آخر، يرجع الـ API دائماً **404 Not Found** (وليس 403) لمنع استكشاف وجود المعرفات (Prevent ID Enumeration / IDOR).
4. **الحذف الناعم (Soft Delete):**
   - العمليات والأطراف تُحذف ناعماً (`is_active = false`) مع إلغاء الأقساط غير المدفوعة تلقائياً.
5. **التقارير الدورية التلقائية:**
   - يتم احتساب وتوليد تقرير دوري بصيغة PDF وإرساله بالبريد الإلكتروني تلقائياً عند كل مضاعف للعدد 7 من العمليات النشطة للمستخدم (7، 14، 21...).

---

## 📑 فهرس الأقسام التسعة (The 9 Core Features)
1. [المصادقة وتسجيل الدخول (Authentication)](#1-المصادقة-وتسجيل-الدخول-authentication)
2. [الملف الشخصي وإدارة الحساب (Users & Profile)](#2-الملف-الشخصي-وإدارة-الحساب-users--profile)
3. [جهات الاتصال والأطراف (Contacts)](#3-جهات-الاتصال-والأطراف-contacts)
4. [العمليات المالية (Transactions)](#4-العمليات-المالية-transactions)
5. [جدولة وإدارة الأقساط والسداد (Installments & Payments)](#5-جدولة-وإدارة-الأقساط-والسداد-installments--payments)
6. [الملاحظات والتسجيلات الصوتية (Voice Notes)](#6-الملاحظات-والتسجيلات-الصوتية-voice-notes)
7. [الإشعارات والتنبيهات (Notifications)](#7-الإشعارات-والتنبيهات-notifications)
8. [خدمة التذكيرات التلقائية في الخلفية (Automated Background Reminders)](#8-خدمة-التذكيرات-التلقائية-في-الخلفية-automated-background-reminders)
9. [التقارير الدورية وملفات PDF (Periodic Reports)](#9-التقارير-الدورية-وملفات-pdf-periodic-reports)

---

## 1. المصادقة وتسجيل الدخول (Authentication)

### الفيتشر دي بتعمل إيه؟
تتيح تسجيل مستخدمين جدد وتسجيل الدخول بدون كلمة مرور عبر كود تحقق لمرة واحدة (Email OTP) مكون من 6 أرقام يرسل لبريد المستخدم، مع إصدار رموز وصول JWT (`token`) صالحة لمدة 30 يوماً ورموز تجديد (`refreshToken`) صالحة لمدة 90 يوماً.

### المسارات ونقاط النهاية:
- `POST /api/auth/register` — تسجيل مستخدم جديد
- `POST /api/auth/send-otp` — إرسال كود التحقق
- `POST /api/auth/verify-otp` — التحقق من الكود وإصدار التوكن
- `POST /api/auth/refresh-token` — تجديد التوكن
- `POST /api/auth/logout` — تسجيل الخروج وإلغاء الرمز

### جدول الحقول (Field Definitions):
| الحقل | المسار | النوع | إلزامي؟ | القيود والشرح |
| :--- | :--- | :--- | :--- | :--- |
| `firstName` | `/register` | `string` | نعم | الاسم الأول (أحرف ومسافات فقط، 1-50 حرف) |
| `lastName` | `/register` | `string` | نعم | الاسم الأخير (أحرف ومسافات فقط، 1-50 حرف) |
| `email` | `/register`, `/send-otp`, `/verify-otp` | `string` | نعم | بريد إلكتروني صالح (بحد أقصى 254 حرف) |
| `code` | `/verify-otp` | `string` | نعم | كود التحقق (6 أرقام، صالح لمدة دقيقتين، بحد أقصى 3 محاولات) |
| `refreshToken` | `/refresh-token`, `/logout` | `string` | نعم | رمز التحديث النشط |

### أمثلة متعددة (Request & Response Examples):

#### مثال 1: تسجيل حساب جديد
```http
POST /api/auth/register HTTP/1.1
Content-Type: application/json

{
  "firstName": "نادر",
  "lastName": "عوني",
  "email": "owner@mizan.app"
}
```
```json
{
  "success": true,
  "message": "تم إرسال كود التحقق بنجاح",
  "data": {
    "otpSent": true,
    "expiresInSeconds": 120
  }
}
```

#### مثال 2: تأكيد الكود وتسجيل الدخول
```http
POST /api/auth/verify-otp HTTP/1.1
Content-Type: application/json

{
  "email": "owner@mizan.app",
  "code": "123456"
}
```
```json
{
  "success": true,
  "message": "تم تسجيل الدخول بنجاح",
  "data": {
    "userId": "d290f1ee-6c54-4b01-90e6-d701748f0851",
    "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
    "refreshToken": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
    "firstName": "نادر",
    "lastName": "عوني",
    "email": "owner@mizan.app",
    "userType": "shop_owner",
    "isSetupCompleted": true
  }
}
```

### صندوق الأخطاء الشائعة (Common Errors):
| الحالة | السبب | الاستجابة | الحل |
| :--- | :--- | :--- | :--- |
| `400 Bad Request` | البريد مستخدم مسبقاً في `/register` | `{"statusCode": 400, "message": "البريد الإلكتروني مسجل بالفعل"}` | استخدام `/send-otp` لتسجيل الدخول مباشرة. |
| `400 Bad Request` | كود OTP منتهي أو خاطئ | `{"statusCode": 400, "message": "Invalid or expired verification code"}` | طلب كود جديد وإدخاله خلال 120 ثانية. |
| `429 Too Many Requests` | تجاوز عدد محاولات إرسال الكود | `{"statusCode": 429, "message": "Too Many Requests"}` | الانتظار لمدة دقيقة قبل طلب كود جديد. |

---

## 2. الملف الشخصي وإدارة الحساب (Users & Profile)

### الفيتشر دي بتعمل إيه؟
تتيح للمستخدم استعراض بيانات حسابه وملفه الشخصي بعد تسجيل الدخول، وتحديد نوع حسابه (`customer` أو `shop_owner`) مع تسجيل اسم وعنوان المتجر لأصحاب المحلات.

### المسارات ونقاط النهاية:
- `GET /api/users/me` — استعراض بيانات الحساب الحالي والمحل
- `POST /api/auth/select-user-type` — تحديد نوع الحساب وتفاصيل المتجر

### جدول الحقول (Field Definitions):
| الحقل | المسار | النوع | إلزامي؟ | القيود والشرح |
| :--- | :--- | :--- | :--- | :--- |
| `userType` | `/select-user-type` | `string` | نعم | إما `"customer"` أو `"shop_owner"` |
| `shopName` | `/select-user-type` | `string` | اختياري | مطلوب إذا كان `userType = "shop_owner"` (1-100 حرف) |
| `address` | `/select-user-type` | `string` | اختياري | عنوان المتجر (1-200 حرف) |

### أمثلة متعددة (Request & Response Examples):

#### مثال 1: تحديد نوع الحساب كصاحب متجر
```http
POST /api/auth/select-user-type HTTP/1.1
Authorization: Bearer {token}
Content-Type: application/json

{
  "userType": "shop_owner",
  "shopName": "محل ميزان للمواد الغذائية والكماليات",
  "address": "القاهرة - مدينة نصر"
}
```
```json
{
  "success": true,
  "message": "تم تحديث نوع الحساب بنجاح",
  "data": {
    "userId": "d290f1ee-6c54-4b01-90e6-d701748f0851",
    "userType": "shop_owner",
    "shopName": "محل ميزان للمواد الغذائية والكماليات",
    "address": "القاهرة - مدينة نصر"
  }
}
```

#### مثال 2: استرجاع الملف الشخصي
```http
GET /api/users/me HTTP/1.1
Authorization: Bearer {token}
```
```json
{
  "success": true,
  "data": {
    "id": "d290f1ee-6c54-4b01-90e6-d701748f0851",
    "firstName": "نادر",
    "lastName": "عوني",
    "email": "owner@mizan.app",
    "userType": "shop_owner",
    "isActive": true,
    "shop": {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "shopName": "محل ميزان للمواد الغذائية والكماليات",
      "address": "القاهرة - مدينة نصر"
    }
  }
}
```

### صندوق الأخطاء الشائعة (Common Errors):
| الحالة | السبب | الاستجابة | الحل |
| :--- | :--- | :--- | :--- |
| `400 Bad Request` | ترك اسم المحل فارغاً عند اختيار `shop_owner` | `{"statusCode": 400, "message": "اسم المحل مطلوب لأصحاب المتاجر"}` | إرسال حقل `shopName` بقيمة صحيحة. |
| `401 Unauthorized` | استدعاء المسار دون توكن JWT | `{"statusCode": 401, "message": "Unauthorized"}` | إرفاق الترويسة `Authorization: Bearer {token}`. |

---

## 3. جهات الاتصال والأطراف (Contacts)

### الفيتشر دي بتعمل إيه؟
إدارة دفتر الأطراف (العملاء والموردين) التابعين للمستخدم الحالي حصراً، مع دعم التصفح المفهرس، البحث بالاسم، التعديل، والحذف الناعم الآمن مع عزل كامل للبيانات (Multi-Tenant Isolation).

### المسارات ونقاط النهاية:
- `POST /api/contacts` — إضافة طرف جديد
- `GET /api/contacts` — استعراض قائمة الأطراف مع البحث والترقيم
- `GET /api/contacts/{id:guid}` — جلب بيانات طرف محدد
- `PUT /api/contacts/{id:guid}` — تعديل بيانات طرف
- `DELETE /api/contacts/{id:guid}` — حذف طرف ناعماً (Soft Delete)

### جدول الحقول (Field Definitions):
| الحقل | المسار | النوع | إلزامي؟ | القيود والشرح |
| :--- | :--- | :--- | :--- | :--- |
| `name` | `POST`, `PUT` | `string` | نعم | اسم الطرف (1-100 حرف، أحرف ومسافات فقط بدون أرقام) |
| `phoneNumber` | `POST`, `PUT` | `string` | اختياري | رقم الهاتف (8-15 رقماً) |
| `notes` | `POST`, `PUT` | `string` | اختياري | ملاحظات إضافية (بحد أقصى 500 حرف) |
| `page` | `GET` | `int` | اختياري | رقم الصفحة (الافتراضي: 1) |
| `pageSize` | `GET` | `int` | اختياري | حجم الصفحة (بين 1 و 50، الافتراضي: 20) |
| `search` | `GET` | `string` | اختياري | نص البحث لتصفية الأسماء |

### أمثلة متعددة (Request & Response Examples):

#### مثال 1: إنشاء طرف عميل جديد
```http
POST /api/contacts HTTP/1.1
Authorization: Bearer {token}
Content-Type: application/json

{
  "name": "محمود إبراهيم الكردي",
  "phoneNumber": "+201012345678",
  "notes": "عميل منتظم في السداد"
}
```
```json
{
  "success": true,
  "message": "تم إضافة الطرف بنجاح",
  "data": {
    "id": "8f3b6c41-2a1d-4f78-bc91-5a2e3d4c5b6a",
    "name": "محمود إبراهيم الكردي",
    "phoneNumber": "+201012345678",
    "notes": "عميل منتظم في السداد",
    "isActive": true,
    "createdAt": "2026-08-17T20:00:00Z"
  }
}
```

#### مثال 2: البحث واستعراض الأطراف
```http
GET /api/contacts?page=1&pageSize=10&search=محمود HTTP/1.1
Authorization: Bearer {token}
```
```json
{
  "success": true,
  "data": {
    "items": [
      {
        "id": "8f3b6c41-2a1d-4f78-bc91-5a2e3d4c5b6a",
        "name": "محمود إبراهيم الكردي",
        "phoneNumber": "+201012345678",
        "notes": "عميل منتظم في السداد",
        "isActive": true,
        "createdAt": "2026-08-17T20:00:00Z"
      }
    ],
    "page": 1,
    "pageSize": 10,
    "totalCount": 1,
    "totalPages": 1
  }
}
```

### صندوق الأخطاء الشائعة (Common Errors):
| الحالة | السبب | الاستجابة | الحل |
| :--- | :--- | :--- | :--- |
| `400 Bad Request` | احتواء الاسم على أرقام أو رموز غير مسموحة | `{"statusCode": 400, "message": "اسم الطرف يجب أن يحتوي على أحرف ومسافات فقط"}` | إدخال اسم صحيح خالٍ من الأرقام. |
| `404 Not Found` | طلب طرف يخص مستخدماً آخر أو غير موجود | `{"statusCode": 404, "message": "Contact not found"}` | التحقق من صحة الـ UUID وملكية الحساب له. |

---

## 4. العمليات المالية (Transactions)

### الفيتشر دي بتعمل إيه؟
تسجيل المعاملات المالية اليومية (بيع `Sale` أو شراء `Purchase`) سواء نقداً (كاش) أو بالأقساط، مع فلترة وتصفح العمليات بحسب التاريخ أو الطرف أو النوع، والحذف الناعم للعمليات مع إلغاء الأقساط غير المدفوعة. تطلق تلقائياً احتساب التقرير الدوري عند كل مضاعف للعدد 7 من العمليات.

### المسارات ونقاط النهاية:
- `POST /api/transactions` — إنشاء عملية جديدة
- `GET /api/transactions` — استعراض العمليات مع الفلترة والتصفح
- `GET /api/transactions/{id:guid}` — جلب عملية محددة بتفاصيلها وأقساطها
- `DELETE /api/transactions/{id:guid}` — حذف ناعم للعملية

### جدول الحقول الشامل (Field Definitions):
| الحقل | المسار | النوع الفعلي | إلزامي؟ | القيم والقيود |
| :--- | :--- | :--- | :--- | :--- |
| `contactId` | `POST` | `string (UUID v4)` | نعم | معرف الطرف التابع لنفس المستخدم |
| `type` | `POST`, `GET` | `string (Enum)` | نعم | `"Sale"` (بيع) أو `"Purchase"` (شراء) |
| `amount` | `POST` | `decimal` | نعم | إجمالي المبلغ (> 0 و <= 1,000,000,000) |
| `transactionDate` | `POST` | `DateTime (ISO)` | نعم | تاريخ العملية (لا يتجاوز الغد) |
| `noteType` | `POST` | `string (Enum)` | اختياري | `"None"` أو `"Text"` (الافتراضي: `"None"`) |
| `noteText` | `POST` | `string` | اختياري | نص الملاحظة (بحد أقصى 1000 حرف) |
| `isInstallment` | `POST` | `bool` | اختياري | `true` للتقسيط، `false` للكاش (الافتراضي: `false`) |

### أمثلة متعددة (Request & Response Examples):

#### مثال 1: مبيعات كاش فورية بدون أقساط
```http
POST /api/transactions HTTP/1.1
Authorization: Bearer {token}
Content-Type: application/json

{
  "contactId": "8f3b6c41-2a1d-4f78-bc91-5a2e3d4c5b6a",
  "type": "Sale",
  "amount": 1500.00,
  "transactionDate": "2026-08-17T10:00:00Z",
  "noteType": "None",
  "isInstallment": false
}
```
```json
{
  "success": true,
  "message": "تم إنشاء العملية بنجاح",
  "data": {
    "id": "c1f2e3d4-5b6a-7c8d-9e0f-1a2b3c4d5e6f",
    "contactId": "8f3b6c41-2a1d-4f78-bc91-5a2e3d4c5b6a",
    "contactName": "محمود إبراهيم الكردي",
    "type": "Sale",
    "amount": 1500.00,
    "transactionDate": "2026-08-17T10:00:00Z",
    "isInstallment": false,
    "installmentPlanMode": null,
    "noteType": "None",
    "noteText": null,
    "hasVoiceNote": false,
    "totalPaid": 1500.00,
    "totalRemaining": 0.00,
    "isActive": true,
    "createdAt": "2026-08-17T10:00:00Z",
    "updatedAt": "2026-08-17T10:00:00Z",
    "installments": []
  }
}
```

#### مثال 2: مشتريات نقدية مع ملاحظة نصية
```http
POST /api/transactions HTTP/1.1
Authorization: Bearer {token}
Content-Type: application/json

{
  "contactId": "8f3b6c41-2a1d-4f78-bc91-5a2e3d4c5b6a",
  "type": "Purchase",
  "amount": 800.00,
  "transactionDate": "2026-08-17T11:00:00Z",
  "noteType": "Text",
  "noteText": "شراء بضاعة ألبان كاش",
  "isInstallment": false
}
```

### صندوق الأخطاء الشائعة (Common Errors):
| الحالة | السبب | الاستجابة | الحل |
| :--- | :--- | :--- | :--- |
| `400 Bad Request` | إرسال رقم بدل نص في حقل `type` (مثل `"type": 0`) | `{"statusCode": 400, "message": "The JSON value could not be converted to Mizan.Core.Enums.TransactionType"}` | إرسال القيمة كنص: `"Sale"` أو `"Purchase"`. |
| `404 Not Found` | إرسال `contactId` لطرف يخص مستخدماً آخر | `{"statusCode": 404, "message": "Contact not found"}` | التأكد من استخدام معرف طرف تابع للمستخدم المصادق به. |

---

## 5. جدولة وإدارة الأقساط والسداد (Installments & Payments)

### الفيتشر دي بتعمل إيه؟
تتيح تقسيط العمليات المالية بأسلوبين: تقسيط تلقائي متساوي الأقساط (`Automatic`) بفترات دورية (أسبوعية، شهرية، سنوية) أو تقسيط مخصص (`Custom`) بمبالغ وتواريخ يحددها المستخدم يدوياً، مع إمكانية استعراض جدول الأقساط وتسجيل سداد الأقساط وتحديث إجمالي المدفوع والمتبقي فوراً.

### المسارات ونقاط النهاية:
- `POST /api/transactions` (مع تفعيل `isInstallment: true`)
- `GET /api/transactions/{id:guid}/installments` — استعراض أقساط العملية
- `POST /api/transactions/{id:guid}/installments/{installmentId:guid}/pay` — تسجيل سداد قسط محدد

### جدول الحقول الخاصة بالأقساط:
| الحقل | المسار | النوع الفعلي | إلزامي؟ | القيم والقيود |
| :--- | :--- | :--- | :--- | :--- |
| `installmentPlanMode` | `POST /transactions` | `string (Enum)` | نعم إذا مقسطة | `"Automatic"` أو `"Custom"` |
| `installmentCount` | `POST /transactions` | `int` | نعم للتقسيط التلقائي | عدد الأقساط (>= 2) |
| `firstInstallmentDate` | `POST /transactions` | `DateTime (ISO)` | نعم للتقسيط التلقائي | تاريخ استحقاق القسط الأول |
| `frequency` | `POST /transactions` | `string (Enum)` | نعم للتقسيط التلقائي | `"Weekly"`, `"Monthly"`, `"Yearly"` |
| `customInstallments` | `POST /transactions` | `array` | نعم للتقسيط المخصص | قائمة تحتوي مبالغ وتواريخ الأقساط |
| `customInstallments[].amount` | `POST /transactions` | `decimal` | نعم | قيمة القسط (> 0 ومجموعها = إجمالي العملية) |
| `customInstallments[].dueDate` | `POST /transactions` | `DateTime (ISO)` | نعم | تاريخ الاستحقاق |

### أمثلة متعددة (Request & Response Examples):

#### مثال 1: إنشاء بيع بتقسيط تلقائي شهري على 3 دفعات
```http
POST /api/transactions HTTP/1.1
Authorization: Bearer {token}
Content-Type: application/json

{
  "contactId": "8f3b6c41-2a1d-4f78-bc91-5a2e3d4c5b6a",
  "type": "Sale",
  "amount": 3000.00,
  "transactionDate": "2026-08-17T20:00:00Z",
  "noteType": "Text",
  "noteText": "بيع هاتف ذكي بالتقسيط على 3 شهور",
  "isInstallment": true,
  "installmentPlanMode": "Automatic",
  "installmentCount": 3,
  "firstInstallmentDate": "2026-09-01T00:00:00Z",
  "frequency": "Monthly"
}
```
```json
{
  "success": true,
  "message": "تم إنشاء العملية بنجاح",
  "data": {
    "id": "c1f2e3d4-5b6a-7c8d-9e0f-1a2b3c4d5e6f",
    "contactId": "8f3b6c41-2a1d-4f78-bc91-5a2e3d4c5b6a",
    "contactName": "محمود إبراهيم الكردي",
    "type": "Sale",
    "amount": 3000.00,
    "transactionDate": "2026-08-17T20:00:00Z",
    "isInstallment": true,
    "installmentPlanMode": "Automatic",
    "noteType": "Text",
    "noteText": "بيع هاتف ذكي بالتقسيط على 3 شهور",
    "hasVoiceNote": false,
    "totalPaid": 0.00,
    "totalRemaining": 3000.00,
    "isActive": true,
    "createdAt": "2026-08-17T20:00:00Z",
    "updatedAt": "2026-08-17T20:00:00Z",
    "installments": [
      {
        "id": "a1b2c3d4-e5f6-4a5b-8c9d-0e1f2a3b4c5d",
        "installmentNumber": 1,
        "amount": 1000.00,
        "dueDate": "2026-09-01T00:00:00Z",
        "status": "Pending",
        "paidAt": null
      },
      {
        "id": "b2c3d4e5-f6a7-4b5c-9d0e-1f2a3b4c5d6e",
        "installmentNumber": 2,
        "amount": 1000.00,
        "dueDate": "2026-10-01T00:00:00Z",
        "status": "Pending",
        "paidAt": null
      },
      {
        "id": "c3d4e5f6-a7b8-4c5d-0e1f-2a3b4c5d6e7f",
        "installmentNumber": 3,
        "amount": 1000.00,
        "dueDate": "2026-11-01T00:00:00Z",
        "status": "Pending",
        "paidAt": null
      }
    ]
  }
}
```

#### مثال 2: سداد القسط الأول
```http
POST /api/transactions/c1f2e3d4-5b6a-7c8d-9e0f-1a2b3c4d5e6f/installments/a1b2c3d4-e5f6-4a5b-8c9d-0e1f2a3b4c5d/pay HTTP/1.1
Authorization: Bearer {token}
```
```json
{
  "success": true,
  "message": "تم تسجيل سداد القسط بنجاح",
  "data": {
    "id": "c1f2e3d4-5b6a-7c8d-9e0f-1a2b3c4d5e6f",
    "amount": 3000.00,
    "totalPaid": 1000.00,
    "totalRemaining": 2000.00,
    "installments": [
      {
        "id": "a1b2c3d4-e5f6-4a5b-8c9d-0e1f2a3b4c5d",
        "installmentNumber": 1,
        "amount": 1000.00,
        "status": "Paid",
        "paidAt": "2026-08-17T20:05:00Z"
      }
    ]
  }
}
```

### صندوق الأخطاء الشائعة (Common Errors):
| الحالة | السبب | الاستجابة | الحل |
| :--- | :--- | :--- | :--- |
| `400 Bad Request` | عدم تطابق مجموع الأقساط المخصصة مع إجمالي العملية | `{"statusCode": 400, "message": "Installment amounts must sum exactly to the total transaction amount"}` | ضبط مبالغ الأقساط ليكون مجموعها مساوياً تماماً لـ `amount`. |
| `400 Bad Request` | محاولة سداد قسط مدفوع مسبقاً | `{"statusCode": 400, "message": "القسط مسدد بالفعل"}` | التحقق من حالة القسط قبل إرسال طلب السداد. |

---

## 6. الملاحظات والتسجيلات الصوتية (Voice Notes)

### الفيتشر دي بتعمل إيه؟
إرفاق تسجيل صوتي للعملية المالية وتخزينه في مسار داخلي آمن، مع إمكانية البث المباشر (Audio Streaming) المحمي للملف الصوتي عبر تقنية Range Requests لتمكين التقديم والترجيع في مشغلات الصوت.

### المسارات ونقاط النهاية:
- `POST /api/transactions/{id:guid}/voice-note` — رفع ملف صوتي للعملية
- `GET /api/transactions/{id:guid}/voice-note` — بث والاستماع للتسجيل الصوتي

### جدول الحقول والقيود:
| الحقل | المسار | النوع | إلزامي؟ | القيود والشرح |
| :--- | :--- | :--- | :--- | :--- |
| `file` | `POST /voice-note` | `file (Form-Data)` | نعم | صيغ مدعومة: `audio/mpeg`, `audio/mp4`, `audio/wav`, `audio/x-m4a`, `audio/webm` بحد أقصى 10 ميجابايت |
| `id` | `GET`, `POST` | `string (UUID v4)` | نعم | معرف العملية التابعة للمستخدم الحالي |

### أمثلة متعددة (Request & Response Examples):

#### مثال 1: رفع ملف صوتي
```http
POST /api/transactions/c1f2e3d4-5b6a-7c8d-9e0f-1a2b3c4d5e6f/voice-note HTTP/1.1
Authorization: Bearer {token}
Content-Type: multipart/form-data; boundary=----WebKitFormBoundary

------WebKitFormBoundary
Content-Disposition: form-data; name="file"; filename="voice_record.m4a"
Content-Type: audio/x-m4a

[binary audio data]
------WebKitFormBoundary--
```
```json
{
  "success": true,
  "message": "تم رفع الملاحظة الصوتية بنجاح",
  "data": {
    "hasVoiceNote": true,
    "noteType": "Voice"
  }
}
```

#### مثال 2: الاستماع للبث الصوتي
```http
GET /api/transactions/c1f2e3d4-5b6a-7c8d-9e0f-1a2b3c4d5e6f/voice-note HTTP/1.1
Authorization: Bearer {token}
Range: bytes=0-102400
```
```http
HTTP/1.1 206 Partial Content
Content-Type: audio/x-m4a
Content-Range: bytes 0-102400/524288
Content-Length: 102401

[audio stream bytes]
```

### صندوق الأخطاء الشائعة (Common Errors):
| الحالة | السبب | الاستجابة | الحل |
| :--- | :--- | :--- | :--- |
| `400 Bad Request` | رفع صيغة غير مدعومة (مثل `text/plain` أو `image/png`) | `{"statusCode": 400, "message": "نوع الملف غير مدعوم. الصيغ المدعومة: mp3, wav, m4a, mp4, webm"}` | اختيار ملف صوتي بالصيغ المعتمدة. |
| `404 Not Found` | محاولة الاستماع لصوتية عملية غير موجودة أو تخص مستخدماً آخر | `{"statusCode": 404, "message": "Transaction not found"}` | عزل أمني: لا يمكن الوصول للتسجيلات الصوتية الخاصة بالآخرين. |

---

## 7. الإشعارات والتنبيهات (Notifications)

### الفيتشر دي بتعمل إيه؟
استعراض وإدارة الإشعارات الداخلية المنشأة في النظام (تذكيرات الأقساط المستحقة، تنبيهات جاهزية التقارير الدورية)، مع دعم التصفية للإشعارات غير المقروءة، وتمييز إشعار محدد أو جميع الإشعارات كمقروءة بطلب واحد.

### المسارات ونقاط النهاية:
- `GET /api/notifications` — استعراض قائمة الإشعارات
- `GET /api/notifications/unread-count` — عدد الإشعارات غير المقروءة
- `POST /api/notifications/{id:guid}/read` — تمييز إشعار كمقروء
- `POST /api/notifications/read-all` — تمييز كل الإشعارات كمقروءة

### جدول الحقول (Field Definitions):
| الحقل | المسار | النوع | إلزامي؟ | القيود والشرح |
| :--- | :--- | :--- | :--- | :--- |
| `page` | `GET` | `int` | اختياري | رقم الصفحة (الافتراضي: 1) |
| `pageSize` | `GET` | `int` | اختياري | عدد العناصر بالصفحة (1-50) |
| `unreadOnly` | `GET` | `bool` | اختياري | `true` لجلب الإشعارات غير المقروءة فقط |
| `id` | `POST /read` | `string (UUID v4)` | نعم | معرف الإشعار |

### أمثلة متعددة (Request & Response Examples):

#### مثال 1: استعراض قائمة الإشعارات
```http
GET /api/notifications?page=1&pageSize=20 HTTP/1.1
Authorization: Bearer {token}
```
```json
{
  "success": true,
  "data": {
    "items": [
      {
        "id": "f5a4b3c2-1d0e-4f9a-8b7c-6d5e4f3a2b1c",
        "type": "InstallmentReminder",
        "title": "تذكير بقسط مستحق قريباً",
        "message": "قسط مستحق على محمود إبراهيم الكردي بقيمة 1000.00 ج.م خلال 3 أيام",
        "transactionId": "c1f2e3d4-5b6a-7c8d-9e0f-1a2b3c4d5e6f",
        "installmentId": "a1b2c3d4-e5f6-4a5b-8c9d-0e1f2a3b4c5d",
        "periodicReportId": null,
        "isRead": false,
        "createdAt": "2026-08-29T06:00:00Z"
      }
    ],
    "page": 1,
    "pageSize": 20,
    "totalCount": 1,
    "totalPages": 1
  }
}
```

#### مثال 2: جلب عدد غير المقروء
```http
GET /api/notifications/unread-count HTTP/1.1
Authorization: Bearer {token}
```
```json
{
  "success": true,
  "data": {
    "unreadCount": 1
  }
}
```

### صندوق الأخطاء الشائعة (Common Errors):
| الحالة | السبب | الاستجابة | الحل |
| :--- | :--- | :--- | :--- |
| `404 Not Found` | محاولة تمييز إشعار يخص مستخدماً آخر كمقروء | `{"statusCode": 404, "message": "Notification not found"}` | استخدام معرف إشعار صالح ومملوك لنفس الحساب. |

---

## 8. خدمة التذكيرات التلقائية في الخلفية (Automated Background Reminders)

### الفيتشر دي بتعمل إيه؟
خدمة خلفية مستضافة (`ReminderCheckService`) تعمل دورياً (افتراضياً كل 60 دقيقة) لفحص الأقساط المعلقة (`Pending`) التي اقترب موعد استحقاقها (قبل 3 أيام، قبل يوم واحد، ويوم الاستحقاق نفسه 0 يوم). تقوم بإنشاء إشعار داخلي فوري وإرسال بريد إلكتروني تذكيري، مع حماية تامة ضد التكرار باستخدام قيد فريد في قاعدة البيانات على `(InstallmentId, DaysBeforeDue)`.

### جدول معايير وقواعد الخدمة:
| الإعداد / المعيار | القيمة الافتراضية | التوصيف |
| :--- | :--- | :--- |
| `Reminders:CheckIntervalMinutes` | `60` دقيقة | الفترة الزمنية بين كل دورة فحص للخدمة |
| `Reminders:DaysBeforeDue` | `[3, 1]` | الأيام المسبقة للتذكير بالإضافة ليوم الاستحقاق (0) |
| قيد عدم التكرار | `IX_installment_reminder_logs` | فهرس فريد يمنع تكرار إرسال التذكير لنفس المرحلة نهائياً |
| مرونة الفشل | Non-blocking | في حال فشل إرسال البريد، لا يتم تسجيل log ليعاد المحاولة بالدورة التالية |

### أمثلة الإشعارات والرسائل المولدة:
- **قبل 3 أيام:** `"تذكير: قسط مستحق على [اسم الطرف] بقيمة [المبلغ] ج.م خلال 3 أيام (تاريخ الاستحقاق: YYYY-MM-DD)"`
- **يوم الاستحقاق (اليوم):** `"تنبيه عاجل: قسط مستحق اليوم على [اسم الطرف] بقيمة [المبلغ] ج.م"`

### صندوق استكشاف المشكلات (Troubleshooting):
| المشكلة | السبب المحتمل | المعالجة |
| :--- | :--- | :--- |
| عدم وصول بريد التذكير | تعطل خادم الـ SMTP أو بريد غير صالح | تقوم الخدمة بإعادة المحاولة في الدورة التالية لعدم تسجيل السجل عند الفشل. |
| تكرار التذكيرات | محاولة تشغيل متزامن لعدة خوادم | الفهرس الفريد في قاعدة البيانات يمنع الإدراج المزدوج ويرفضه على مستوى المحرك. |

---

## 9. التقارير الدورية وملفات PDF (Periodic Reports)

### الفيتشر دي بتعمل إيه؟
توليد تقرير دوري مالي شامل تلقائياً عند كل مضاعف للعدد 7 من العمليات النشطة للمستخدم (العملية رقم 7، 14، 21...). يتم احتساب إجمالي المبيعات والمشتريات وتوليد ملف PDF احترافي منسق باللغة العربية عبر QuestPDF وحفظه في مسار معزول، وإرسال بريد إلكتروني بالملف وإشعار داخل التطبيق، مع إمكانية استعراض قائمة التقارير وتحميل ملف الـ PDF بأي وقت.

### المسارات ونقاط النهاية:
- `GET /api/reports` — قائمة التقارير الدورية
- `GET /api/reports/{id:guid}` — تفاصيل تقرير دوري محدد
- `GET /api/reports/{id:guid}/download` — تحميل أو معاينة ملف الـ PDF

### جدول الحقول (Field Definitions):
| الحقل | المسار | النوع | إلزامي؟ | القيود والشرح |
| :--- | :--- | :--- | :--- | :--- |
| `batchNumber` | `GET /reports` | `int` | قراءة فقط | رقم الدفعة (1، 2، 3...) |
| `transactionCount` | `GET /reports` | `int` | قراءة فقط | عدد العمليات بالدفعة (دائماً 7 عمليات) |
| `totalSalesAmount` | `GET /reports` | `decimal` | قراءة فقط | إجمالي مبالغ المبيعات في الدفعة |
| `totalPurchasesAmount` | `GET /reports` | `decimal` | قراءة فقط | إجمالي مبالغ المشتريات في الدفعة |
| `emailSent` | `GET /reports` | `bool` | قراءة فقط | حالة إرسال البريد الإلكتروني للمستخدم |

### أمثلة متعددة (Request & Response Examples):

#### مثال 1: استعراض التقارير الدورية
```http
GET /api/reports?page=1&pageSize=20 HTTP/1.1
Authorization: Bearer {token}
```
```json
{
  "success": true,
  "data": {
    "items": [
      {
        "id": "9b8a7c6d-5e4f-4a3b-2c1d-0e9f8a7b6c5d",
        "batchNumber": 1,
        "transactionCount": 7,
        "totalSalesAmount": 4500.00,
        "totalPurchasesAmount": 1200.00,
        "emailSent": true,
        "generatedAt": "2026-08-17T20:00:00Z"
      }
    ],
    "page": 1,
    "pageSize": 20,
    "totalCount": 1,
    "totalPages": 1
  }
}
```

#### مثال 2: تحميل ملف الـ PDF
```http
GET /api/reports/9b8a7c6d-5e4f-4a3b-2c1d-0e9f8a7b6c5d/download HTTP/1.1
Authorization: Bearer {token}
```
```http
HTTP/1.1 200 OK
Content-Type: application/pdf
Content-Disposition: inline; filename="Mizan-Report-Batch-1.pdf"

[PDF Binary Data]
```

### صندوق الأخطاء الشائعة (Common Errors):
| الحالة | السبب | الاستجابة | الحل |
| :--- | :--- | :--- | :--- |
| `404 Not Found` | محاولة تحميل تقرير يخص مستخدماً آخر | `{"statusCode": 404, "message": "Report not found"}` | عزل أمني: التقارير وملفات الـ PDF معزولة تماماً لكل مستخدم. |
