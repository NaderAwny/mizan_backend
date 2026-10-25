# ⚖️ Mizan Backend — نظام ميزان

نظام Backend متكامل لإدارة الديون والمبيعات والأقساط مصمم وفق معايير **Clean Architecture** باستخدام **.NET 8** و **SQL Server**.

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

## 🔔 نظام التذكيرات والإشعارات الفورية (Reminders & Push Notifications)

- **فحص ذكي وفق توقيت مصر (`EgyptTime`)**: ضبط كامل للتواريخ والمواعيد بناءً على توقيت القاهرة (`Africa/Cairo` / UTC+2/+3) ودعم التوقيت الصيفي لمنع ترحيل الأيام.
- **توليد فوري عند إنشاء المعاملات (`ReminderTriggerWorker`)**: طابور غير متزامن يقوم بفحص وتوليد الإشعارات فور تسجيل أي معاملة تحتوي على أقساط مستحقة قريباً أو اليوم، دون الحاجة لانتظار فحص الخلفية الدوري.
- **فصل قنوات الإرسال وتأكيد الحفظ (Two-Phase Persistence)**: يتم إنشاء وتثبيت الإشعارات في التطبيق وسجلات التذكير قبل تنفيذ أي اتصال خارجي، لضمان عدم تأثر إشعارات التطبيق في حال تعثر البريد الإلكتروني.
- **إشعارات الهاتف الفورية (`Firebase Cloud Messaging - FCM`)**: دعم إرسال Push Notifications لهواتف أصحاب المتاجر عبر FirebaseAdmin مع إدارة ذكية لتوكنات الأجهزة (حد أقصى 10 أجهزة للمستخدم وحذف التوكنات المنتهية تلقائياً).
- **تغطية الأقساط المتأخرة (`Overdue`)**: دعم التذكير بالأقساط المتأخرة حتى 7 أيام سابقة مع صياغة عربية مخصصة للأقساط المتأخرة.

### نقاط نهاية إدارة الأجهزة:
- `POST /api/devices/register`: تسجيل أو تحديث توكن الجهاز (`token`, `platform`, `deviceId`).
- `POST /api/devices/unregister`: إلغاء تسجيل توكن الجهاز.
- `POST /api/auth/logout`: يدعم تمرير `deviceToken` اختياري لإلغاء تسجيل الجهاز فور الخروج.

### إعدادات الـ Configuration:
```json
"Reminders": {
  "Enabled": true,
  "CheckIntervalMinutes": 5,
  "DaysBeforeDue": [3, 1],
  "OverdueLookbackDays": 7,
  "MaxChannelAttempts": 3,
  "RetryBackoffMinutes": 5
},
"Firebase": {
  "Enabled": false,
  "ServiceAccountJson": null,
  "ServiceAccountPath": null,
  "AndroidChannelId": "mizan_reminders"
}
```

### ⚙️ إعدادات Firebase Cloud Messaging (Firebase Setup):

#### 1. التطوير المحلي (Local Development via dotnet user-secrets):
لحماية مفاتيح الخدمة من التسريب، تُخزن مسارات الاعتماد خارج الـ repository حصرياً عبر `dotnet user-secrets`:
```bash
# تفعيل الخدمة محلياً وتحديد مسار ملف الاعتماد خارج المستودع
dotnet user-secrets set "Firebase:Enabled" "true" --project src/Mizan.API
dotnet user-secrets set "Firebase:ServiceAccountPath" "C:\secrets\mizan-firebase.json" --project src/Mizan.API
```
> [!NOTE]
> يدعم النظام التطوير المحلي إما عبر `Firebase:ServiceAccountPath` أو `Firebase:ServiceAccountJson`. وفي حال فقدان ملف المفتاح أو عدم صلاحيته، يتراجع التطبيق تلقائياً وبأمان إلى وضع `NoOp` ويسجل سطراً آمناً (`Push: NoOp (reason)`) دون إيقاف تشغيل الخادم.

#### 2. بيئة الإنتاج (Production on runasp.net via Environment Variables):
تُمرر إعدادات Firebase كمتغيرات بيئة بالترميز المزدوج (`__`):
- `Firebase__Enabled`: تفعيل الخدمة (`true` أو `false`).
- `Firebase__ServiceAccountJson`: نص الـ JSON الكامل لحساب الخدمة (يدعم السطور المتعددة والأحرف المهربة `\n` / `\\n` في `private_key`).
- `Firebase__ProjectId`: معرف المشروع (اختياري، يقرأ تلقائياً من الـ JSON).
- `Firebase__AndroidChannelId`: معرف قناة الإشعارات لنظام أندرويد (`mizan_reminders`).

## 🚀 تشغيل المشروع

```bash
# 1. استعادة الحزم والبناء
dotnet build

# 2. تشغيل الاختبارات الآلية
dotnet test

# 3. تشغيل الـ API
dotnet run --project src/Mizan.API
```
