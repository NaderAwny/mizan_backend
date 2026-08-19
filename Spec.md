# مواصفات تطبيق ميزان — Mizan Technical Specification

## 1. نظرة عامة
تطبيق ميزان لإدارة الديون والمبيعات والأقساط والملاحظات الصوتية مع لوحة إحصائيات فورية وتذكيرات ذكية.
- **المصادقة:** البريد الإلكتروني ورمز التحقق (Email OTP) + JWT Bearer Tokens مع Refresh Tokens.
- **المعرفات:** جميع المعرفات تستخدم صيغة `Guid` (UUID v4).
- **الـ Enums:** تمثل كنصوص (`String Enums` في الـ JSON).
- **الإحصائيات:** نظام إحصائيات لحظي Real-Time Statistics كبديل كامل لملفات الـ PDF الثابتة.

---

## 2. النماذج الأساسية (Core Entities)

- **المستخدم (User)**:
  - `id` (Guid), `email` (string, max 254), `first_name` (string, max 50), `last_name` (string, max 50), `user_type` ("customer" / "shop_owner"), `is_active` (bool), `created_at` (DateTime).

- **كود التحقق (OtpCode)**:
  - `id` (Guid), `email` (string), `code` (string, 6 digits), `expires_at` (DateTime, 120s), `attempts_count` (int), `is_used` (bool), `created_at` (DateTime).

- **المحل (Shop)**:
  - `id` (Guid), `owner_id` (FK → User), `shop_name` (string, max 100), `address` (string, max 200), `created_at` (DateTime).

- **رمز التحديث (RefreshToken)**:
  - `id` (Guid), `user_id` (FK → User), `token` (string), `expires_at` (DateTime, 30 days), `created_at` (DateTime), `revoked_at` (DateTime?), `replaced_by_token` (string?).

- **الطرف / جهة الاتصال (Contact)**:
  - `id` (Guid), `owner_user_id` (FK → User), `name` (string, max 100), `phone_number` (string?, max 20), `notes` (string?, max 500), `is_vip` (bool, default false - Feature 2), `contact_email` (string?, max 254 - Feature 3), `is_active` (bool, soft-delete), `created_at` (DateTime), `updated_at` (DateTime).

- **العملية (Transaction)**:
  - `id` (Guid), `shop_id` (FK → Shop), `owner_user_id` (FK → User), `contact_id` (Guid?, nullable FK → Contact), `party_name` (string, max 200), `type` ("Sale" / "Purchase"), `amount` (decimal), `payment_method` ("Cash" / "Installments"), `transaction_date` (DateTime), `is_installment` (bool), `installment_plan_mode` ("Automatic" / "Custom" / null), `note_type` (None, Text, Voice), `note_text` (string?, max 1000), `note_audio_path` (string?, internal path), `is_active` (bool, soft-delete), `created_at` (DateTime), `updated_at` (DateTime).

- **القسط (Installment)**:
  - `id` (Guid), `transaction_id` (FK → Transaction), `installment_number` (int, 1-based), `amount` (decimal), `due_date` (DateTime), `status` ("Pending", "Paid", "Overdue" [computed], "Voided"), `is_paid` (bool), `paid_at` (DateTime?), `created_at` (DateTime), `updated_at` (DateTime).

- **الملاحظة الصوتية (VoiceNote — Entity مستقل)**:
  - `id` (Guid), `shop_id` (FK → Shop), `owner_user_id` (FK → User), `contact_id` (Guid?, nullable FK → Contact), `party_name` (string, max 200), `operation_type` ("Sale" / "Purchase" / "InstallmentCollection" / "InstallmentPayment"), `amount` (decimal), `operation_date` (DateTime), `audio_path` (string, max 500), `notes` (string?, max 1000), `is_active` (bool, soft-delete), `created_at` (DateTime), `updated_at` (DateTime).

- **الإشعار (Notification)**:
  - `id` (Guid), `owner_user_id` (FK → User), `type` ("InstallmentReminder"), `title` (string, max 150), `message` (string, max 500), `transaction_id` (Guid?, nullable FK), `installment_id` (Guid?, nullable FK), `is_read` (bool, default false), `created_at` (DateTime).

- **سجل تذكيرات الأقساط (InstallmentReminderLog)**:
  - `id` (Guid), `installment_id` (FK → Installment), `days_before_due` (int), `sent_at` (DateTime), `contact_email_sent` (bool).

---

## 3. مسارات المصادقة (Auth Endpoints)
- `POST /api/auth/register`: تسجيل مستخدم جديد بالبريد والاسم الأول واسم العائلة وإرسال كود OTP.
- `POST /api/auth/send-otp`: طلب كود تسجيل الدخول بالبريد الإلكتروني فقط.
- `POST /api/auth/verify-otp`: التحقق من الكود (`code` أو `otpCode`) وإصدار Access Token (صالح 7 أيام) و Refresh Token (صالح 30 يوم).
- `POST /api/auth/select-user-type`: تحديد نوع الحساب (`customer` أو `shop_owner`) وإضافة بيانات المحل التجاري.
- `POST /api/auth/refresh-token`: تجديد رمز الوصول عند انتهائه.
- `POST /api/auth/logout`: تسجيل الخروج وإلغاء رمز التحديث.

---

## 4. مسارات الأطراف والعملاء المميزين (Contacts & VIP Endpoints)
جميع المسارات تتطلب JWT Bearer Token ومقيّدة بالمستخدم المسجل (owner-scoped).
- `POST /api/contacts`: إضافة طرف جديد (عميل / مورد).
- `GET /api/contacts?page=1&pageSize=20&search=`: قائمة الأطراف مع البحث والتصفح.
- `GET /api/contacts/vip?page=1&pageSize=20`: قائمة العملاء المميزين فقط (`isVip = true` - Feature 2).
- `GET /api/contacts/{id:guid}`: جلب طرف بالمعرف.
- `GET /api/contacts/{id:guid}/transactions`: استعراض بروفايل مالي متكامل للعميل متضمناً عدد العمليات وإجمالي المبالغ وقائمة العمليات (Feature 2).
- `PATCH /api/contacts/{id:guid}/toggle-vip`: ترقية / إلغاء تمييز العميل المميز بنقرة واحدة (Feature 2).
- `PUT /api/contacts/{id:guid}`: تعديل بيانات الطرف وتعيين البريد الإلكتروني (`contactEmail`) وحالة الـ VIP.
- `DELETE /api/contacts/{id:guid}`: حذف ناعم للطرف (يعيد 204 No Content).

---

## 5. مسارات العمليات (Transactions Endpoints)
- `POST /api/transactions`: إنشاء عملية بيع أو شراء (كاش، أو أقساط آلية متساوية، أو جدول أقساط مخصص).
- `GET /api/transactions?page=1&pageSize=20&contactId=&type=&dateFrom=&dateTo=`: قائمة العمليات مع الفلترة والتصفح.
- `GET /api/transactions/{id:guid}`: جلب عملية بالمعرف مع جدول أقساطها وحالة سدادها.
- `DELETE /api/transactions/{id:guid}`: حذف ناعم للعملية وإلغاء أقساطها غير المدفوعة.

---

## 6. مسارات الأقساط (Installments Endpoints)
- `POST /api/installments/{id:guid}/pay`: تسجيل سداد قسط مستحق وتحديث حالته وتاريخ سداده.

---

## 7. مسارات الإحصائيات الفورية والعمليات السريعة (Real-Time Statistics - Feature 1)
بديل لحظي لنظام الـ PDF القديم:
- `GET /api/statistics/summary`: ملخص إحصائيات اليوم الحالي (إجمالي المبيعات، المشتريات، عدد العمليات، وقائمة العمليات مرتبة من الأحدث للأقدم).
- `GET /api/statistics/daily?date=YYYY-MM-DD`: إحصائيات يوم محدد.
- `GET /api/statistics/monthly?year=YYYY&month=M`: إحصائيات شهر محدد مع التوزيع اليومي للعمليات.
- `POST /api/statistics/quick-sale`: تسجيل عملية بيع نقدي سريعة بطرف حر دون إضافة جهة اتصال مسبقة.
- `POST /api/statistics/quick-installment-collection`: تحصيل قسط فوري بنقرة واحدة من الشاشة الرئيسية.

---

## 8. مسارات الملاحظات الصوتية (Voice Notes — Entity مستقل)
نظام مستقل تماماً عن جدول العمليات:
- `POST /api/voice-notes`: رفع ملاحظة صوتية (multipart/form-data) مع الملف الصوتي، المبلغ، نوع العملية، واسم الطرف.
- `GET /api/voice-notes?page=1&pageSize=20`: قائمة الملاحظات الصوتية المرفوعة للمتجر مع تفاصيلها.
- `GET /api/voice-notes/{id:guid}`: جلب تفاصيل ملاحظة صوتية بالمعرف.
- `DELETE /api/voice-notes/{id:guid}`: حذف ملاحظة صوتية ناعماً.

---

## 9. مسارات الإشعارات والتذكيرات الثنائية (Notifications & Bidirectional Reminders - Feature 3)
- `GET /api/notifications?page=1&pageSize=20&unreadOnly=false`: قائمة الإشعارات.
- `PATCH /api/notifications/{id:guid}/read`: تعليم إشعار كمقروء.
- `PATCH /api/notifications/read-all`: تعليم كافة الإشعارات كمقروءة.
- `POST /api/notifications/run-reminders-scan`: تشغيل يدوي فوري لفحص التذكيرات الثنائية وإرسال الإشعارات وإيميلات التذكير للعملاء.

---

## 10. خدمة التذكيرات التلقائية في الخلفية (Background Reminders Service)
- تعمل خدمة `ReminderCheckService` كـ `BackgroundService` دورية.
- تفحص الأقساط المعلقة قبل استحقاقها بـ 3 أيام، بيوم واحد، وفي يوم الاستحقاق نفسه (`0` يوم).
- ترسل إشعاراً في التطبيق للتاجر، وإيميل تذكير مباشر للعميل إذا كان مسجلاً لديه `contact_email`.
- تمنع التكرار نهائياً عبر قيد فريد في قاعدة البيانات على `(installment_id, days_before_due)`.
