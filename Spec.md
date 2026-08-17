# مواصفات تطبيق ميزان — Mizan Technical Specification

## 1. نظرة عامة
تطبيق ميزان لإدارة الديون والمبيعات والأقساط.
المصادقة: البريد الإلكتروني ورمز التحقق (Email OTP) + JWT Bearer Tokens.

## 2. النماذج الأساسية
- **المستخدم (User)**: `id`, `email`, `first_name`, `last_name`, `user_type`, `password_hash`, `is_active`, `created_at`.
- **كود التحقق (OtpCode)**: `id`, `email`, `code`, `expires_at`, `attempts_count`, `is_used`, `created_at`.
- **المحل (Shop)**: `id`, `owner_id`, `shop_name`, `address`, `created_at`.
- **رمز التحديث (RefreshToken)**: `id`, `user_id`, `token`, `expires_at`, `created_at`, `revoked_at`, `replaced_by_token`.
- **الطرف (Contact)**: `id` (Guid), `owner_user_id` (FK → User), `name`, `phone_number` (optional), `notes` (optional, max 500), `is_active` (soft-delete), `created_at`, `updated_at`.
- **العملية (Transaction)**: `id` (Guid), `owner_user_id` (FK → User), `contact_id` (FK → Contact), `type` (Sale = 0, Purchase = 1), `amount` (decimal), `transaction_date` (DateTime), `is_installment` (bool), `installment_plan_mode` (Automatic = 0, Custom = 1), `note_type` (None = 0, Text = 1, Voice = 2), `note_text` (string, max 1000), `note_audio_path` (string, internal path), `is_active` (soft-delete), `created_at`, `updated_at`.
- **القسط (Installment)**: `id` (int), `transaction_id` (FK → Transaction), `installment_number` (int, 1-based), `amount` (decimal), `due_date` (DateTime), `status` (Pending = 0, Paid = 1, Overdue = 2 [computed], Voided = 3), `paid_at` (DateTime?), `created_at`, `updated_at`.
- **الإشعار (Notification)**: `id` (int), `owner_user_id` (FK → User), `type` (InstallmentReminder = 0, PeriodicReportReady = 1), `title` (string, max 150), `message` (string, max 500), `transaction_id` (int?, nullable FK), `installment_id` (int?, nullable FK), `periodic_report_id` (int?, nullable FK), `is_read` (bool, default false), `created_at`.
- **التقرير الدوري (PeriodicReport)**: `id` (int), `owner_user_id` (FK → User), `batch_number` (int), `transaction_count` (int), `total_sales_amount` (decimal), `total_purchases_amount` (decimal), `pdf_storage_path` (string, internal path), `email_sent` (bool, default false), `generated_at` (DateTime).

## 3. مسارات المصادقة (Auth Endpoints)
- `POST /api/auth/register`: تسجيل مستخدم جديد بالبريد والاسم.
- `POST /api/auth/send-otp`: طلب كود تسجيل الدخول.
- `POST /api/auth/verify-otp`: التحقق من الكود وإصدار الـ Access & Refresh Tokens.
- `POST /api/auth/select-user-type`: تحديد نوع الحساب (`customer` أو `shop_owner`).
- `POST /api/auth/refresh-token`: تجديد رمز الوصول.
- `POST /api/auth/logout`: تسجيل الخروج وإلغاء رمز التحديث.

## 4. مسارات الأطراف (Contacts Endpoints)
جميع المسارات تتطلب JWT Bearer Token ومقيّدة بالمستخدم المسجل (owner-scoped).
- `POST /api/contacts`: إضافة طرف جديد. يعيد 201 مع بيانات الطرف.
- `GET /api/contacts?page=1&pageSize=20&search=`: قائمة الأطراف مع البحث والتصفح (pageSize مقيّد بين 1 و50).
- `GET /api/contacts/{id}`: جلب طرف بالمعرف. يعيد 404 إذا لم يوجد أو لا يملكه المستخدم (منع enumeration).
- `PUT /api/contacts/{id}`: تعديل طرف. يعيد 200 مع البيانات المحدّثة.
- `DELETE /api/contacts/{id}`: حذف ناعم (soft delete). يعيد 204.

## 5. مسارات العمليات والأقساط (Transactions & Installments Endpoints)
جميع المسارات تتطلب JWT Bearer Token ومقيّدة بالمستخدم المسجل (owner-scoped).
- `POST /api/transactions`: إنشاء عملية جديدة (مع خيار الأقساط الأوتوماتيكية أو المخصصة). يعيد 201.
- `GET /api/transactions?page=1&pageSize=20&contactId=&type=&dateFrom=&dateTo=`: قائمة العمليات مع البحث والتصفية بحسب الطرف، نوع العملية، أو نطاق التاريخ.
- `GET /api/transactions/{id}`: جلب عملية بالمعرف مع أقساطها المجدولة وحالة السداد. يعيد 404 إذا لم توجد أو تخص مستخدم آخر.
- `DELETE /api/transactions/{id}`: حذف ناعم للعملية وإلغاء أقساطها غير المدفوعة. يعيد 204.
- `POST /api/transactions/{id}/voice-note`: إرفاق ملاحظة صوتية (multipart/form-data, 10MB limit, formats: mp3, mp4, wav, m4a, webm).
- `GET /api/transactions/{id}/voice-note`: بث استماع محمي للملاحظة الصوتية (FileStream).
- `POST /api/transactions/{id}/installments/{installmentId}/pay`: تسجيل سداد قسط وتحديث إجمالي المدفوع والمتبقي.

## 6. مسارات التذكيرات والإشعارات (Notifications Endpoints)
جميع المسارات تتطلب JWT Bearer Token ومقيّدة بالمستخدم المسجل (owner-scoped) مع تطبيق سياسة الـ Rate Limiting.
- `GET /api/notifications?page=1&pageSize=20&unreadOnly=false`: استرجاع قائمة الإشعارات مع التصفح والفلترة (pageSize مقيّد بين 1 و50).
- `GET /api/notifications/unread-count`: استرجاع عدد الإشعارات غير المقروءة `{ unreadCount: int }`.
- `POST /api/notifications/{id}/read`: تمييز إشعار كمقروء (يعيد 204، و404 إذا لم يوجد أو يخص مستخدم آخر).
- `POST /api/notifications/read-all`: تمييز جميع إشعارات المستخدم كمقروءة (يعيد 204).

## 7. مسارات التقارير الدورية (Periodic Reports Endpoints)
جميع المسارات تتطلب JWT Bearer Token ومقيّدة بالمستخدم المسجل (owner-scoped) مع تطبيق سياسة الـ Rate Limiting.
- `GET /api/reports?page=1&pageSize=20`: استرجاع قائمة التقارير الدورية المفهرسة مع التصفح (pageSize مقيّد بين 1 و50).
- `GET /api/reports/{id}`: استرجاع تفاصيل التقرير بالمعرف (يعيد 404 إذا لم يوجد أو يخص مستخدم آخر لمنع enumeration).
- `GET /api/reports/{id}/download`: تحميل أو معاينة ملف الـ PDF الخاص بالتقرير الدوري (بث محمي `FileStreamResult` بصيغة `application/pdf`).

### آلية تفعيل وتوليد التقارير الدورية (Trigger Rule & Delivery)
- عند إنشاء العملية رقم 7، 14، 21... (كل 7 عمليات نشطة للمستخدم) عبر `POST /api/transactions`:
  1. يتم حساب إجمالي المبيعات والمشتريات وتوليد ملف PDF ملخص وشامل للدفعة باستخدام QuestPDF.
  2. يتم حفظ ملف الـ PDF في مسار داخلي معزول `App_Data/reports/{ownerUserId}/{guid}.pdf`.
  3. يتم إنشاء سجل `PeriodicReport` مرتبط بقيد فريد على `(OwnerUserId, BatchNumber)` لضمان حماية التزامن ومنع تكرار التقارير عند السباق.
  4. يتم إنشاء إشعار داخلي فوري في التطبيق (`PeriodicReportReady`) مرتبط بالتقرير للوصول السريع.
  5. يتم إطلاق مهمة إرسال البريد الإلكتروني في الخلفية مع ملف الـ PDF كمرفق بصورة غير حاجبة (Non-blocking) عبر `IServiceScopeFactory`.
  6. تعمل خدمة `PeriodicReportEmailRetryService` في الخلفية دورياً لإعادة محاولة إرسال أي تقارير تعذر تسليم بريدها.

## 8. خدمة التذكيرات التلقائية في الخلفية (Background Reminders Job)
- تعمل خدمة `ReminderCheckService` كـ `BackgroundService` مستضافة بفترة دورية قابلة للتهيئة عبر `Reminders:CheckIntervalMinutes` (الافتراضي 60 دقيقة).
- تفحص الأقساط المعلقة (`Pending`) وفق المراحل المسبقة المحددة في الإعدادات `Reminders:DaysBeforeDue` (مثال: `[3, 1]`) بالإضافة إلى يوم الاستحقاق نفسه (`0` يوم - إلزامي دائماً).
- ترسل إشعاراً داخلياً في التطبيق (`Notification`) وبريداً إلكترونياً تذكيرياً (`Email Reminder`).
- تمنع التكرار نهائياً حتى عبر إعادة تشغيل التطبيق بفضل قيد فريد في قاعدة البيانات على `(InstallmentId, DaysBeforeDue)`.
- في حال تعذر إرسال البريد الإلكتروني، لا يتم حفظ سجل التذكير لضمان إعادة المحاولة في الدورة التالية.
