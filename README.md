# ميزان — Mizan Backend

نظام الـ Backend المتكامل لتطبيق **ميزان** لإدارة الديون والمبيعات والأقساط، مبني باستخدام **ASP.NET Core (.NET 9)** وقاعدة بيانات **SQL Server** باتباع معمارية **Clean Architecture** و **Rich Domain Model**.

---

## 🏛️ البنية المعمارية (Clean Architecture)

- **`Mizan.Core`**: الكيانات الأساسية (Entities غنية بمنطق العمل وValidation داخلي)، الاستثناءات المخصصة (`DomainException`, `NotFoundException`, ...)، واجهات الـ Repositories و `IUnitOfWork`.
- **`Mizan.Application`**: الخدمات والمنطق التطبيقي (`AuthService`, `UserService`)، الـ DTOs مع التحقق باللغة العربية، واجهات الخدمات الخارجية (`IWhatsAppService`, `IJwtProvider`).
- **`Mizan.Infrastructure`**: طبقة الوصول للبيانات (`MizanDbContext`, `UnitOfWork`, Repositories مع EF Core Fluent API)، مزود JWT، خدمة WhatsApp Cloud API.
- **`Mizan.API`**: الـ Controllers (`AuthController`, `UsersController`)، الـ Middlewares (`ExceptionHandlingMiddleware`, `AccountStatusMiddleware` مع Memory Cache)، إعدادات Rate Limiting و Swagger.
- **`Mizan.UnitTests`**: اختبارات الوحدة والاختبارات التكاملية (Integration Tests مع `WebApplicationFactory`).

---

## 🚀 التشغيل محلياً

```bash
# استعادة الحزم والبناء
dotnet restore
dotnet build

# تشغيل الاختبارات
dotnet test

# تشغيل الـ API
cd src/Mizan.API
dotnet run
```

---

## 🔑 المميزات المنفذة (Feature 1: Auth & Foundation)

1. **التسجيل والمصادقة برقم الواتساب**:
   - التحقق من صيغة أرقام الهواتف المصرية (`010/011/012/015` أو `+20` والتوحيد التلقائي إلى 11 رقماً).
   - إرسال OTP عبر WhatsApp Cloud API (صالح 120 ثانية، حد أقصى 3 محاولات).
2. **إدارة الجلسات والأمان (JWT + Refresh Tokens)**:
   - Access Token صالح 7 أيام.
   - Refresh Token صالح 30 يوماً.
   - سياسة أقصى 5 أجهزة متصلة للمستخدم في نفس الوقت (إلغاء الأقدم تلقائياً).
   - Rate Limiting (5 محاولات / دقيقة على endpoints الـ Auth).
3. **تحديد نوع الحساب**:
   - `customer` (مستخدم عادي) أو `shop_owner` (صاحب محل مع حفظ بيانات المحل).
4. **تنسيق موحد للاستجابة والأخطاء**:
   - صيغة أخطاء موحدة في كل النظام: `{ "statusCode": ..., "message": "..." }`.
   - كاش لحالة تفعيل الحساب (2 دقيقة) لتخفيف الضغط على قاعدة البيانات.
