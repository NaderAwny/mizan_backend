# 📘 دليل اختبارات ميزان الشامل على Postman (Mizan API Testing Guide)

مرحباً بك! هذا الدليل الشامل مصمم ليغطي **كل سيناريوهات الاختبار الممكنة (100%)** لتطبيق **ميزان (Mizan Backend)** على Postman خطوة بخطوة، سواء لنوعي الحسابات (**صاحب محل Shop Owner** أو **عميل Customer**)، وعمليات **المبيعات (Sales)** و**المشتريات (Purchases)**، والإدخال **اليدوي (Text/Manual)** و**الصوتي (Voice Recording/Transcript)**، ونظام **الكاش** و**الأقساط (Installments)**، واختبار **توليد التقارير الدورية (Periodic Reports - كل 7 عمليات)**، مع التأكد التام من **عزل البيانات والأمان (Owner-scoped 404 Not Found)** والحالات السلبية.

---

## 📑 فهرس أقسام الاختبار

1. [⚙️ إعداد البيئة ومتغيرات Postman](#1-إعداد-البيئة-ومتغيرات-postman)
2. [🔐 القسم 1: المصادقة وإنشاء الحسابات (Shop Owner & Customer)](#2-القسم-1-المصادقة-وإنشاء-الحسابات)
3. [👤 القسم 2: الملف الشخصي (User Profile)](#3-القسم-2-الملف-الشخصي)
4. [👥 القسم 3: إدارة الأطراف (Contacts - عملاء وموردين)](#4-القسم-3-إدارة-الأطراف)
5. [💸 القسم 4: العمليات — إدخال يدوي كاش وأقساط (Manual Transactions)](#5-القسم-4-العمليات--إدخال-يدوي)
6. [🎙️ القسم 5: العمليات — إدخال صوتي وملفات صوتية (Voice Notes)](#6-القسم-5-العمليات--إدخال-صوتي)
7. [📅 القسم 6: إدارة وتسديد الأقساط (Installments Management)](#7-القسم-6-إدارة-وتسديد-الأقساط)
8. [📊 القسم 7: التقارير الدورية عند كل 7 عمليات (Periodic Reports)](#8-القسم-7-التقارير-الدورية)
9. [🔔 القسم 8: الإشعارات والتنبيهات (Notifications)](#9-القسم-8-الإشعارات-والتنبيهات)
10. [🛡️ القسم 9: اختبارات الأمان والعزل والتحقق السلبي (Security & Negative Tests)](#10-القسم-9-اختبارات-الأمان-والعزل)

---

## 1. إعداد البيئة ومتغيرات Postman

قبل البدء، تأكد من استيراد ملف البيئة `Mizan_Local.postman_environment.json` في Postman وتحديد البيئة كـ **Mizan — Local (localhost)**.

### المتغيرات المحفوظة تلقائياً في البيئة (Auto-captured Variables):
| المتغير | الوصف | كيفية التعبئة |
| :--- | :--- | :--- |
| `base_url` | رابط الخادم المحلي | `http://localhost:5210` (افتراضي) |
| `otp_code` | كود التحقق من 6 أرقام | يتم استلامه على الإيميل وإدخاله أو جلبه |
| `access_token` | توكن الـ JWT لصاحب المحل الرئيسي | يُحفظ تلقائياً عند نجاح Verify OTP |
| `refresh_token` | توكن التجديد لصاحب المحل | يُحفظ تلقائياً عند نجاح Verify OTP |
| `customer_access_token` | توكن الـ JWT لمستخدم تجريبي كعميل (لاختبار العزل) | يُحفظ تلقائياً عند فحص العميل |
| `contact_id` | معرف الطرف (عميل/مورد) | يُحفظ تلقائياً عند إضافة طرف جديد |
| `contact_supplier_id` | معرف طرف كمورد | يُحفظ تلقائياً عند إضافة مورد |
| `transaction_id` | معرف آخر عملية منشأة | يُحفظ تلقائياً عند إنشاء أي عملية |
| `installment_id` | معرف قسط مستحق | يُحفظ تلقائياً مع جداول الأقساط |
| `report_id` | معرف التقرير الدوري الناتج | يُحفظ تلقائياً عند إنشاء التقرير |

---

## 2. القسم 1: المصادقة وإنشاء الحسابات

### السيناريو 1.1: تسجيل حساب جديد كصاحب محل (Shop Owner)
- **Method**: `POST`
- **URL**: `{{base_url}}/api/auth/register`
- **Body (JSON)**:
```json
{
  "email": "owner@mizan.app",
  "firstName": "نادر",
  "lastName": "عوني"
}
```
- **Expected Status**: `200 OK`
- **Response**: `success: true`, `data.otpSent: true`.

---

### السيناريو 1.2: طلب كود تسجيل الدخول (Send OTP)
- **Method**: `POST`
- **URL**: `{{base_url}}/api/auth/send-otp`
- **Body (JSON)**:
```json
{
  "email": "owner@mizan.app"
}
```
- **Expected Status**: `200 OK`

---

### السيناريو 1.3: تأكيد الكود والحصول على التوكن (Verify OTP - Shop Owner)
- **Method**: `POST`
- **URL**: `{{base_url}}/api/auth/verify-otp`
- **Body (JSON)**:
```json
{
  "email": "owner@mizan.app",
  "code": "{{otp_code}}"
}
```
- **Expected Status**: `200 OK`
- **Auto Action**: يقوم الاسكريبت بحفظ `access_token` و `refresh_token` في متغيرات البيئة.

---

### السيناريو 1.4: تحديد نوع الحساب كـ صاحب محل (Shop Owner Setup)
- **Method**: `POST`
- **URL**: `{{base_url}}/api/auth/select-user-type`
- **Headers**: `Authorization: Bearer {{access_token}}`
- **Body (JSON)**:
```json
{
  "userType": "shop_owner",
  "shopName": "محل ميزان للمواد الغذائية والكماليات",
  "address": "القاهرة - مدينة نصر"
}
```
- **Expected Status**: `200 OK`
- **Response**: `data.userType: "shop_owner"`, `data.shopName: "محل ميزان للمواد الغذائية والكماليات"`.

---

### السيناريو 1.5 & 1.6: إنشاء حساب ثانٍ كـ عميل عادي (Customer) لاختبار العزل والأمان
- **Step 1.5**: `POST /api/auth/register` بإيميل `customer@mizan.app` والاسم `أحمد محمود`.
- **Step 1.6**: `POST /api/auth/verify-otp` ويحفظ الاسكريبت التوكن في `customer_access_token`.
- **Step 1.7**: `POST /api/auth/select-user-type` بـ:
```json
{
  "userType": "customer"
}
```
- **Expected Status**: `200 OK`.

---

### السيناريو 1.8: تجديد التوكن (Refresh Token)
- **Method**: `POST`
- **URL**: `{{base_url}}/api/auth/refresh-token`
- **Body (JSON)**:
```json
{
  "refreshToken": "{{refresh_token}}"
}
```
- **Expected Status**: `200 OK` (يتم تحديث التوكن تلقائياً).

---

### السيناريو 1.9: تسجيل الخروج (Logout)
- **Method**: `POST`
- **URL**: `{{base_url}}/api/auth/logout`
- **Headers**: `Authorization: Bearer {{access_token}}`
- **Body (JSON)**: `{"refreshToken": "{{refresh_token}}"}`
- **Expected Status**: `200 OK`.

---

## 3. القسم 2: الملف الشخصي (User Profile)

### السيناريو 2.1: عرض ملف صاحب المحل
- **Method**: `GET`
- **URL**: `{{base_url}}/api/users/me`
- **Headers**: `Authorization: Bearer {{access_token}}`
- **Expected Status**: `200 OK`
- **Response Validation**: يحتوي على `userType = "shop_owner"`, `shopName`, `address`.

### السيناريو 2.2: عرض ملف العميل
- **Method**: `GET`
- **URL**: `{{base_url}}/api/users/me`
- **Headers**: `Authorization: Bearer {{customer_access_token}}`
- **Expected Status**: `200 OK`
- **Response Validation**: يحتوي على `userType = "customer"`, و `shopName = null`.

---

## 4. القسم 3: إدارة الأطراف (Contacts)

### السيناريو 3.1: إضافة طرف جديد كـ عميل (Create Customer Contact)
- **Method**: `POST`
- **URL**: `{{base_url}}/api/contacts`
- **Headers**: `Authorization: Bearer {{access_token}}`
- **Body (JSON)**:
```json
{
  "name": "محمد إبراهيم حسنين",
  "phoneNumber": "01012345678",
  "notes": "عميل منتظم في السداد"
}
```
- **Expected Status**: `201 Created`
- **Auto Action**: حفظ المعرف في `contact_id`.

---

### السيناريو 3.2: إضافة طرف جديد كـ مورد (Create Supplier Contact)
- **Method**: `POST`
- **URL**: `{{base_url}}/api/contacts`
- **Headers**: `Authorization: Bearer {{access_token}}`
- **Body (JSON)**:
```json
{
  "name": "شركة النور للمشروبات والتوريدات",
  "phoneNumber": "01123456789",
  "notes": "مورد الألبان والعصائر الأسبوعي"
}
```
- **Expected Status**: `201 Created`
- **Auto Action**: حفظ المعرف في `contact_supplier_id`.

---

### السيناريو 3.3: استعراض وبحث الأطراف (List & Search Contacts)
- **Method**: `GET`
- **URL**: `{{base_url}}/api/contacts?page=1&pageSize=20&search=محمد`
- **Headers**: `Authorization: Bearer {{access_token}}`
- **Expected Status**: `200 OK`
- **Validation**: يرجع فقط الأطراف المطابقة للبحث التابعة لنفس المستخدم المسجل.

---

### السيناريو 3.4: تعديل طرف (Update Contact)
- **Method**: `PUT`
- **URL**: `{{base_url}}/api/contacts/{{contact_id}}`
- **Headers**: `Authorization: Bearer {{access_token}}`
- **Body (JSON)**:
```json
{
  "name": "محمد إبراهيم حسنين المعدل",
  "phoneNumber": "01099998888",
  "notes": "تم تعديل رقم الهاتف"
}
```
- **Expected Status**: `200 OK`.

---

### السيناريو 3.5: الحذف الناعم للطرف (Soft Delete Contact)
- **Method**: `DELETE`
- **URL**: `{{base_url}}/api/contacts/{{contact_id}}`
- **Headers**: `Authorization: Bearer {{access_token}}`
- **Expected Status**: `204 No Content`.

---

## 5. القسم 4: العمليات — إدخال يدوي (Manual Transactions)

### السيناريو 4.1: مبيعات كاش فوري (Manual Cash Sale)
- **Method**: `POST`
- **URL**: `{{base_url}}/api/transactions`
- **Headers**: `Authorization: Bearer {{access_token}}`
- **Body (JSON)**:
```json
{
  "contactId": "{{contact_id}}",
  "type": 0,
  "amount": 1500.00,
  "transactionDate": "2026-08-17T10:00:00Z",
  "noteType": 0,
  "isInstallment": false
}
```
- **Expected Status**: `201 Created`
- **Validation**: `type = 0 (Sale)`, `isInstallment = false`, `installments = []`.

---

### السيناريو 4.2: مشتريات كاش فوري (Manual Cash Purchase)
- **Method**: `POST`
- **URL**: `{{base_url}}/api/transactions`
- **Headers**: `Authorization: Bearer {{access_token}}`
- **Body (JSON)**:
```json
{
  "contactId": "{{contact_supplier_id}}",
  "type": 1,
  "amount": 800.00,
  "transactionDate": "2026-08-17T11:00:00Z",
  "noteType": 1,
  "noteText": "شراء بضاعة ألبان كاش",
  "isInstallment": false
}
```
- **Expected Status**: `201 Created`
- **Validation**: `type = 1 (Purchase)`, `amount = 800.00`.

---

### السيناريو 4.3: مبيعات آجل بأقساط آلية متساوية (Sale - Automatic Equal Installments)
- **Method**: `POST`
- **URL**: `{{base_url}}/api/transactions`
- **Headers**: `Authorization: Bearer {{access_token}}`
- **Body (JSON)**:
```json
{
  "contactId": "{{contact_id}}",
  "type": 0,
  "amount": 3000.00,
  "transactionDate": "2026-08-17T12:00:00Z",
  "noteType": 1,
  "noteText": "بيع شاشة تلفزيون بالأقساط",
  "isInstallment": true,
  "installmentPlanMode": 0,
  "installmentCount": 3,
  "firstInstallmentDate": "2026-08-24T00:00:00Z",
  "frequency": 1
}
```
- **Expected Status**: `201 Created`
- **Validation**:
  - يتولد جدول أقساط يحتوي على 3 أقساط بقيمة 1,000.00 ج.م لكل قسط.
  - حفظ `installment_id` تلقائياً في متغيرات البيئة.

---

### السيناريو 4.4: مشتريات آجل بأقساط مخصصة المواعيد والمبالغ (Purchase - Custom Installments)
- **Method**: `POST`
- **URL**: `{{base_url}}/api/transactions`
- **Headers**: `Authorization: Bearer {{access_token}}`
- **Body (JSON)**:
```json
{
  "contactId": "{{contact_supplier_id}}",
  "type": 1,
  "amount": 5000.00,
  "transactionDate": "2026-08-17T13:00:00Z",
  "noteType": 1,
  "noteText": "توريد أجهزة كهربائية للمحل على دفعتين",
  "isInstallment": true,
  "installmentPlanMode": 1,
  "customInstallments": [
    {
      "amount": 2000.00,
      "dueDate": "2026-08-25T00:00:00Z"
    },
    {
      "amount": 3000.00,
      "dueDate": "2026-09-10T00:00:00Z"
    }
  ]
}
```
- **Expected Status**: `201 Created`
- **Validation**: الأقساط تطابق مجموع العملية بالضبط (2000 + 3000 = 5000).

---

## 6. القسم 5: العمليات — إدخال صوتي وملاحظات صوتية (Voice Notes)

### السيناريو 5.1: إنشاء عملية بناءً على تفريغ صوتي (Create Transaction with Voice Transcript)
- **Method**: `POST`
- **URL**: `{{base_url}}/api/transactions`
- **Headers**: `Authorization: Bearer {{access_token}}`
- **Body (JSON)**:
```json
{
  "contactId": {{contact_id}},
  "type": 0,
  "amount": 1200.00,
  "transactionDate": "2026-08-17T14:00:00Z",
  "noteType": 1,
  "noteText": "تم بيع كرتونة زيت وسكر للحاج محمد على الحساب",
  "isInstallment": false
}
```
- **Expected Status**: `201 Created`
- **Validation**: `noteType = 1 (Text)`, وحفظ نص الملاحظة.

---

### السيناريو 5.2: رفع ملف صوتي للعملية (Upload Voice Note Audio File)
- **Method**: `POST`
- **URL**: `{{base_url}}/api/transactions/{{transaction_id}}/voice-note`
- **Headers**:
  - `Authorization: Bearer {{access_token}}`
  - `Content-Type: multipart/form-data`
- **Body (Form-Data)**:
  - Key: `file` (Type: `File`), اختر أي ملف صوتي `.mp3` أو `.m4a` أو `.wav`.
- **Expected Status**: `200 OK`
- **Response**: `data.voiceNoteAudioPath` تم حفظه بأمان في `App_Data/voice_notes/`.

---

### السيناريو 5.3: الاستماع / تحميل التسجيل الصوتي للعملية (Stream Voice Note)
- **Method**: `GET`
- **URL**: `{{base_url}}/api/transactions/{{transaction_id}}/voice-note`
- **Headers**: `Authorization: Bearer {{access_token}}`
- **Expected Status**: `200 OK`
- **Headers Returned**: `Content-Type: audio/mpeg` (أو نوع الملف المرفوع)، مع بث الملف الصوتي.

---

## 7. القسم 6: إدارة وتسديد الأقساط (Installments Management)

### السيناريو 6.1: استعراض أقساط العملية
- **Method**: `GET`
- **URL**: `{{base_url}}/api/transactions/{{transaction_id}}/installments`
- **Headers**: `Authorization: Bearer {{access_token}}`
- **Expected Status**: `200 OK`
- **Validation**: عرض جميع الأقساط وحالة الدفع `isPaid: false`.

---

### السيناريو 6.2: تسديد قسط محدد (Pay Installment)
- **Method**: `POST`
- **URL**: `{{base_url}}/api/transactions/{{transaction_id}}/installments/{{installment_id}}/pay`
- **Headers**: `Authorization: Bearer {{access_token}}`
- **Expected Status**: `200 OK`
- **Response**: `data.isPaid: true`, `data.paidAt: "2026-08-17T..."`.

---

## 8. القسم 7: التقارير الدورية (Periodic Reports)

### السيناريو 7.1: التوليد التلقائي للتقرير عند العملية رقم 7 (Batch 1 Generation)
عند إنشاء 7 عمليات متتالية للمستخدم:
1. يتم حساب إجمالي المبيعات والمشتريات لتلك العمليات السبع.
2. يتم توليد ملف PDF بجداول منسقة باللغة العربية عبر **QuestPDF**.
3. يتم إرسال إشعار فوري داخل التطبيق (`NotificationType = 1: PeriodicReportReady`).
4. يتم إرسال بريد إلكتروني تلقائياً في الخلفية (`Task.Run` مع `IServiceScopeFactory`) بالتقرير المرفق.

---

### السيناريو 7.2: قائمة التقارير الدورية (List Periodic Reports)
- **Method**: `GET`
- **URL**: `{{base_url}}/api/reports?page=1&pageSize=20`
- **Headers**: `Authorization: Bearer {{access_token}}`
- **Expected Status**: `200 OK`
- **Auto Action**: حفظ أول `id` في متغير البيئة `report_id`.

---

### السيناريو 7.3: تفاصيل التقرير بالمعرف (Get Periodic Report Details)
- **Method**: `GET`
- **URL**: `{{base_url}}/api/reports/{{report_id}}`
- **Headers**: `Authorization: Bearer {{access_token}}`
- **Expected Status**: `200 OK`
- **Validation**: يرجع `batchNumber: 1`, `transactionCount: 7`, `totalSalesAmount`, `totalPurchasesAmount`.

---

### السيناريو 7.4: تحميل ومشاهدة ملف الـ PDF للتقرير (Download PDF Report)
- **Method**: `GET`
- **URL**: `{{base_url}}/api/reports/{{report_id}}/download`
- **Headers**: `Authorization: Bearer {{access_token}}`
- **Expected Status**: `200 OK`
- **Response Header**: `Content-Type: application/pdf`
- **فحص المحتوى**: في Postman اضغط على **"Preview"** أو احفظ الملف لفتحه:
  - العنوان: "تطبيق ميزان — Mizan".
  - كروت ملخصة: إجمالي المبيعات، إجمالي المشتريات، الصافي، عدد العمليات.
  - جدول تفصيلي بالـ 7 عمليات مع اسم الطرف والمبلغ والتاريخ والنوع ونظام الدفع.
  - اتجاه النص العربي من اليمين لليسار (RTL) والحروف متصلة وسليمة 100%.

---

## 9. القسم 8: الإشعارات والتنبيهات (Notifications)

### السيناريو 8.1: استعراض الإشعارات (Get Notifications)
- **Method**: `GET`
- **URL**: `{{base_url}}/api/notifications?page=1&pageSize=20&unreadOnly=false`
- **Headers**: `Authorization: Bearer {{access_token}}`
- **Expected Status**: `200 OK`
- **Validation**: يظهر إشعار `نوع 1 (تقرير دوري جاهز)` ورابط `periodicReportId`.

---

### السيناريو 8.2: عدد الإشعارات غير المقروءة (Unread Count)
- **Method**: `GET`
- **URL**: `{{base_url}}/api/notifications/unread-count`
- **Headers**: `Authorization: Bearer {{access_token}}`
- **Expected Status**: `200 OK`
- **Response**: `data.unreadCount >= 1`.

---

### السيناريو 8.3: تمييز إشعار كمقروء (Mark Notification Read)
- **Method**: `POST`
- **URL**: `{{base_url}}/api/notifications/1/read`
- **Headers**: `Authorization: Bearer {{access_token}}`
- **Expected Status**: `204 No Content`.

---

### السيناريو 8.4: تمييز الكل كمقروء (Mark All Read)
- **Method**: `POST`
- **URL**: `{{base_url}}/api/notifications/read-all`
- **Headers**: `Authorization: Bearer {{access_token}}`
- **Expected Status**: `204 No Content`.

---

## 10. القسم 9: اختبارات الأمان والعزل والتحقق السلبي (Security & Negative Tests)

يحتوي هذا القسم على الاختبارات الصارمة للتأكد من استحالة اختراق أو تجاوز الصلاحيات:

| # | اسم الاختبار | Method & URL | Headers & Body | الكود المتوقع والسبب |
|---|---|---|---|---|
| **9.1** | طلب محمي بدون توكن | `GET /api/transactions` | بدون Header | `401 Unauthorized` |
| **9.2** | توكن غير صالح | `GET /api/transactions` | `Bearer invalid_token_123` | `401 Unauthorized` |
| **9.3** | كود OTP خاطئ | `POST /api/auth/verify-otp` | `{"email": "...", "code": "000000"}` | `400 Bad Request` ("كود التحقق غير صحيح") |
| **9.4** | اسم طرف بأرقام | `POST /api/contacts` | `{"name": "محمد 123"}` | `400 Bad Request` ("يجب أن يحتوي الاسم على أحرف فقط") |
| **9.5** | عدم تطابق مجموع الأقساط | `POST /api/transactions` | مبلغ 1000 وأقساط 300+400 | `400 Bad Request` ("مجموع مبالغ الأقساط يجب أن يساوي إجمالي العملية") |
| **9.6** | ملف صوتي بصيغة ممنوعة | `POST /api/transactions/.../voice-note` | رفع ملف `.exe` أو `.pdf` | `400 Bad Request` ("صيغة الملف غير مدعومة") |
| **9.7** | **عزل الأطراف (404)** | `GET /api/contacts/{{contact_id}}` | `Bearer {{customer_access_token}}` | `404 Not Found` (يمنع معرفة وجود الطرف لمستخدم آخر) |
| **9.8** | **عزل العمليات (404)** | `GET /api/transactions/{{transaction_id}}` | `Bearer {{customer_access_token}}` | `404 Not Found` (لا يمكن للعميل رؤية عملية صاحب المحل) |
| **9.9** | **عزل الصوتيات (404)** | `GET /api/transactions/{{transaction_id}}/voice-note` | `Bearer {{customer_access_token}}` | `404 Not Found` (الملفات الصوتية غير مكشوفة) |
| **9.10**| **عزل التقارير (404)** | `GET /api/reports/{{report_id}}/download` | `Bearer {{customer_access_token}}` | `404 Not Found` (التقارير وسجلاتها معزولة بالكامل) |

---

## 🎯 ملخص طريقة الاستخدام السريع

1. افتح تطبيق **Postman**.
2. استورد ملف الـ Collection: `Mizan_API.postman_collection.json`.
3. استورد ملف الـ Environment: `Mizan_Local.postman_environment.json`.
4. شغّل الـ Backend: `dotnet run --project src/Mizan.API`.
5. نفّذ الطلبات بالترتيب من المجلد 1 حتى المجلد 9؛ ستجد أن كل التوكنات والمعرفات تُحفظ وتُمرر تلقائياً وبكل سلاسة!
