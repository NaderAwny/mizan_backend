# ⚖️ Mizan Backend — نظام ميزان

نظام Backend متكامل لإدارة الديون والمبيعات والأقساط مصمم وفق معايير **Clean Architecture** باستخدام **.NET 9** و **SQL Server**.

## 🏗️ البنية المعمارية (Architecture)

- **`Mizan.Core`**: الكيانات الأساسية (`User`, `OtpCode`, `Shop`, `RefreshToken`)، الاستثناءات الخاصة بالمجال (`DomainException`, `BadRequestException`).
- **`Mizan.Application`**: الخدمات والمنطق التطبيقي (`AuthService`, `UserService`)، الـ DTOs مع التحقق الصارم، واجهات الخدمات الخارجية (`IEmailService`, `IJwtProvider`).
- **`Mizan.Infrastructure`**: طبقة الوصول للبيانات (`MizanDbContext`, `UnitOfWork`, Repositories مع EF Core Fluent API)، مزود JWT، خدمة Email SMTP / Mock.
- **`Mizan.API`**: وحدات التحكم (Controllers)، وسيط معالجة الأخطاء الموحد (`ExceptionHandlingMiddleware`)، توثيق Swagger/OpenAPI مع JWT Bearer، وسياسات الحماية و Rate Limiting.

## 🔐 الأمان والمصادقة (Security & Authentication)

- **المصادقة عبر البريد الإلكتروني**: يتم تسجيل الدخول وإنشاء الحسابات حصرياً عبر البريد الإلكتروني ورمز التحقق (Email OTP).
- **التحقق من الكود**: مقارنة ثابتة الوقت (`CryptographicOperations.FixedTimeEquals`) ضد هجمات التوقيت.
- **إصدار التوكن**: JWT Access Token مشفر وموقع بمفتاح آمن غير معلن في الكود، مع دعم Refresh Token وإلغاء الجلسات.
- **إخفاء التفاصيل الأمنية**: حماية كاملة ضد هجمات الاستكشاف، مع حذف أي حقول حساسة من الاستجابات (`DevCode`).

## 🚀 تشغيل المشروع

```bash
# 1. استعادة الحزم والبناء
dotnet build

# 2. تشغيل الاختبارات الآلية
dotnet test

# 3. تشغيل الـ API
dotnet run --project src/Mizan.API
```
