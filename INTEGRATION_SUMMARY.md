# 📘 توثيق كامل للتكامل: خدمة الواتساب و الـ Webhooks (Vonage Messages API)

تم إعداد هذا التوثيق ليوضح كل الخدمات، التعديلات المعمارية، وضبط الإعدادات التي تمت في الـ Backend (`.NET 9 Clean Architecture`) لتشغيل خدمة إرسال أكواد التحقق (OTP) واستقبال إشعارات التسليم عبر تطبيق WhatsApp.

---

## 🏗️ 1. المعمارية المتبعة (Clean Architecture)

تم الحفاظ بالكامل على هيكل وتصميم المشروع الأصلي بدون أي تغيير في منطق توليد أو التحقق من الـ OTP، وتم توزيع المهام حسب الطبقات:

```
src/
├── Mizan.Core/                       # الكيانات والقواعد الأساسية
├── Mizan.Application/
│   ├── Interfaces/
│   │   ├── IWhatsAppMessageService.cs   ✨ [جديد] واجهة خدمة الواتساب
│   │   └── IVonageWebhookService.cs     ✨ [جديد] واجهة معالجة الـ Webhooks
│   ├── DTOs/
│   │   ├── Auth/SendOtpRequest.cs       ✨ [معدل] نموذج استقبال رقم الهاتف
│   │   └── Webhooks/                    ✨ [جديد] نماذج Vonage Webhook DTOs
│   └── Services/
│       └── AuthService.cs               🔄 [محدّث] ربط إرسال الـ OTP عبر الواتساب
├── Mizan.Infrastructure/
│   └── Services/WhatsApp/
│       ├── VonageOptions.cs             ✨ [جديد] خيارات الإعدادات (Options Pattern)
│       ├── VonageWhatsAppMessageService.cs ✨ [جديد] تنفيذ إرسال الرسائل عبر Vonage API
│       └── VonageWebhookService.cs      ✨ [جديد] تنفيذ معالجة وتتبع حالات الرسائل
└── Mizan.API/
    ├── Controllers/
    │   ├── AuthController.cs            🔄 [محدّث] نقطة POST /api/auth/send-otp
    │   └── VonageWebhooksController.cs  ✨ [جديد] مسارات الـ Webhooks لـ Vonage
    ├── appsettings.json                 🔄 [محدّث] الإعدادات العامة غير السرية
    └── Program.cs                       🔄 [محدّث] تسجيل الـ DI وحقن الـ Services
```

---

## ⚙️ 2. الخدمات التي تم إنشاؤها وتعديلها (Services Breakdown)

### 1️⃣ خدمة إرسال الواتساب (`IWhatsAppMessageService` / `VonageWhatsAppMessageService`)
- **الملفات:**
  - `src/Mizan.Application/Interfaces/IWhatsAppMessageService.cs`
  - `src/Mizan.Infrastructure/Services/WhatsApp/VonageWhatsAppMessageService.cs`
- **الوظيفة:**
  - استقبال رقم الهاتف وكود الـ OTP.
  - تنظيف وتوحيد صيغة الرقم (E.164 formatting) مثل تحويل `01206347094` إلى `201206347094`.
  - إرسال طلب HTTP POST آمن باستخدام `HttpClient` مع `Basic Authentication` (`ApiKey:ApiSecret`).
  - معالجة جميع أكواد الاستجابة من Vonage (`202 Accepted`, `400`, `401`, `403`, `429`, `500`) مع رسائل خطأ واضحة باللغة العربية.
  - عدم طباعة أي أسرار أو أكواد حساسة في الـ Logs.

---

### 2️⃣ خدمة المصادقة (`AuthService`)
- **الملف:** `src/Mizan.Application/Services/AuthService.cs`
- **ما تم تعديله:**
  - تم حقن `IWhatsAppMessageService` في الـ Constructor.
  - عند طلب التسجيل `RegisterAsync` أو إرسال الكود `SendOtpAsync`:
    1. يتم توليد كود الـ OTP بنفس الآلية الآمنة المشفرة السابقة (`RandomNumberGenerator`).
    2. يتم حفظ الكود وتاريخ انتهائه في جدول `otp_codes` بقاعدة البيانات.
    3. يتم استدعاء `_whatsAppService.SendOtpAsync(phoneNumber, otpCode)` لإرسال الكود إلى هاتف المستخدم على الواتساب فوراً.

---

### 3️⃣ خدمة استقبال الـ Webhooks (`IVonageWebhookService` / `VonageWebhookService`)
- **الملفات:**
  - `src/Mizan.Application/Interfaces/IVonageWebhookService.cs`
  - `src/Mizan.Infrastructure/Services/WhatsApp/VonageWebhookService.cs`
  - `src/Mizan.API/Controllers/VonageWebhooksController.cs`
- **الوظيفة:**
  - **Inbound Webhook (`POST /api/webhooks/vonage/inbound`):** يستقبل أي رسالة واردة من العميل على الواتساب.
  - **Status Webhook (`POST /api/webhooks/vonage/status`):** يستقبل تحديثات حالة تسليم الرسالة في الوقت الفعلي (`submitted` ⬅️ `delivered` ⬅️ `read` ⬅️ `rejected`).
  - يقوم بتسجيل الـ Metadata غير الحساسة (`MessageUuid`, `Status`, `Channel`) لسهولة التتبع والمراقبة.

---

### 4️⃣ إعدادات الأمان وحقن الاعتماديات (`Program.cs` & `User Secrets`)
- **الملفات:**
  - `src/Mizan.API/Program.cs`
  - `src/Mizan.Infrastructure/Services/WhatsApp/VonageOptions.cs`
- **ما تم ضبطه:**
  - تم تسجيل `VonageOptions` من خلال الـ `IOptions<VonageOptions>` pattern.
  - تم تسجيل الـ Service عبر `builder.Services.AddHttpClient<IWhatsAppMessageService, VonageWhatsAppMessageService>();` للاستفادة من الـ Connection Pooling وإدارة الـ Sockets.
  - تم تسجيل `IVonageWebhookService` كـ Scoped Service.
  - **حماية الأسرار:** الـ `ApiKey` والـ `ApiSecret` تم تخزينهما عبر **.NET User Secrets**، ولا توجد أي أسرار مكتوبة داخل كود المشروع أو `appsettings.json`.

---

## 🔒 3. مفاتيح التكوين (Configuration Keys)

| المفتاح | الموقع | الوصف | القيمة الحالية |
|---------|--------|-------|----------------|
| `Vonage:ApiKey` | User Secrets | مفتاح حساب Vonage | `cfb459bc` |
| `Vonage:ApiSecret` | User Secrets | السر الخاص بحساب Vonage | `L2Hk10O8XSh2ucDf` |
| `Vonage:SandboxUrl` | `appsettings.json` | رابط نقطة نهاية Vonage Sandbox | `https://messages-sandbox.nexmo.com/v1/messages` |
| `Vonage:WhatsAppFrom` | `appsettings.json` | رقم المرسل المعتمد في الساندبوكس | `14157386102` |

> 💡 **ملاحظة:** في بيئة الساندبوكس (Sandbox)، رقم المرسل `From` يجب أن يكون دائماً `14157386102`.

---

## 🌐 4. مسارات الـ Webhooks والربط الخارجي (Tunneling)

تم ربط السيرفر المحلي بالإنترنت لاستقبال الـ Webhooks عبر **Cloudflare Tunnel**:

- **الرابط العام (Tunnel Public URL):**
  `https://uploaded-arbor-collectables-apartment.trycloudflare.com`

- **المسارات المربوطة في لوحة تحكم Vonage:**
  - **Inbound Webhook URL:**
    `https://uploaded-arbor-collectables-apartment.trycloudflare.com/api/webhooks/vonage/inbound`
  - **Status Webhook URL:**
    `https://uploaded-arbor-collectables-apartment.trycloudflare.com/api/webhooks/vonage/status`

---

## 🧪 5. الاختبارات وضمان الجودة (Testing & QA)

1. **الاختبارات الآلية (Automated Unit & Integration Tests):**
   - تم عزل خدمة الواتساب في بيئة الاختبارات باستخدام Mocking لمنع إجراء اتصالات خارجية أثناء الـ CI/CD.
   - نتيجة تشغيل الاختبارات: **54 / 54 Passed (نجاح بنسبة 100%)**.
   - نتيجة البناء: **0 Errors, 0 Warnings**.

2. **الاختبار الفعلي (Manual WhatsApp Delivery):**
   - تم إرسال طلب `POST /api/auth/send-otp` برقم الهاتف `201206347094`.
   - استجاب الـ Vonage Sandbox برمز `202 Accepted`.
   - وصلت رسالة الكود بنجاح إلى تطبيق WhatsApp.

---

## 📦 6. ملفات Postman الجاهزة

تم تجهيز 3 ملفات لمشاركتها مع فريق العمل والمطورين:

1. **`Mizan_API.postman_collection.json`**: يحتوي على الـ Endpoints كاملة مرتبة تسلسلياً (Register ➡️ Send OTP ➡️ Verify OTP ➡️ Select User Type ➡️ Refresh Token ➡️ Logout).
2. **`Mizan_Local.postman_environment.json`**: للاختبار المحلي على جهاز السيرفر (`http://localhost:5210`).
3. **`Mizan_Public.postman_environment.json`**: لاختبار الفريق عن بُعد عبر الـ Public Cloudflare URL بدون الحاجة لوجودهم على نفس الشبكة.
