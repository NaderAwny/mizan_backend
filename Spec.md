# مواصفات تطبيق ميزان — Mizan Technical Specification

## 1. نظرة عامة
تطبيق ميزان لإدارة الديون والمبيعات والأقساط.
المصادقة: البريد الإلكتروني ورمز التحقق (Email OTP) + JWT Bearer Tokens.

## 2. النماذج الأساسية
- **المستخدم (User)**: `id`, `email`, `first_name`, `last_name`, `user_type`, `password_hash`, `is_active`, `created_at`.
- **كود التحقق (OtpCode)**: `id`, `email`, `code`, `expires_at`, `attempts_count`, `is_used`, `created_at`.
- **المحل (Shop)**: `id`, `owner_id`, `shop_name`, `address`, `created_at`.
- **رمز التحديث (RefreshToken)**: `id`, `user_id`, `token`, `expires_at`, `created_at`, `revoked_at`, `replaced_by_token`.

## 3. مسارات المصادقة (Auth Endpoints)
- `POST /api/auth/register`: تسجيل مستخدم جديد بالبريد والاسم.
- `POST /api/auth/send-otp`: طلب كود تسجيل الدخول.
- `POST /api/auth/verify-otp`: التحقق من الكود وإصدار الـ Access & Refresh Tokens.
- `POST /api/auth/select-user-type`: تحديد نوع الحساب (`customer` أو `shop_owner`).
- `POST /api/auth/refresh-token`: تجديد رمز الوصول.
- `POST /api/auth/logout`: تسجيل الخروج وإلغاء رمز التحديث.
