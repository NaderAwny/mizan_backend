# Mizan — Production Refactor Violations Report

> Generated: 2026-10-02 | Scan: `lib/` (presentation, data, app, domain layers)
> Status: **SCAN ONLY — no code changed yet**

---

## Rule 1 — `setState` Forbidden

| File | Lines (sample) |
|------|---------------|
| `presentation/voice_note/get_voice_note_cubit/get_voice_note_view.dart` | 86, 92, 102, 134, 143, 156, 168, 434, 465, 496 |
| `presentation/voice_note/get_voice_note_by_id_cubit/get_voice_note_by_id_view.dart` | 66, 70, 75, 83 |
| `presentation/voice_note/create_voice_note_cubit/create_voice_note_view.dart` | 101, 110, 118, 127, 162, 175, 203, 209, 216, 233, 248, 254, 273, 303, 936, 1156 |
| `presentation/transactions/transactions_view.dart` | 90, 833, 864 |
| `presentation/statistics/statistics_view.dart` | 113, 144, 160, 164, 1372, 1392, 1421 |
| `presentation/send_otp/send_otp_view.dart` | 227 |
| `presentation/select_user_type/select_user_type_view.dart` | 96, 342, 357, 411, 531 |
| `presentation/register/register_view.dart` | 181, 201, 221 |
| `presentation/operations/widgets/contact_picker_bottom_sheet.dart` | 175 + more |
| `presentation/customers/customers_view.dart` | (detected via StatefulWidget scan) |
| `presentation/customers/contact_form_view.dart` | (detected via StatefulWidget scan) |
| `presentation/auth_verification/auth_verification.dart` | (detected via StatefulWidget scan) |

**Summary:** 11+ files, 50+ `setState` calls. The states managed by `setState` (selected tab index, filter index, playing state, timer countdown, form touch flags, recording state, audio playback position, etc.) must all move into Cubit states with `@freezed`.

---

## Rule 1b — `ValueNotifier` / `ValueListenableBuilder` used instead of Cubit state

| File | Lines |
|------|-------|
| `presentation/send_otp/send_otp_view.dart` | 74, 84, 764, 770, 821, 835 — `ValueNotifier<int>` for countdown timer |

**Fix:** Timer countdown state must go into `SendOtpCubit` as a freezed state field.

---

## Rule 1c — `StatefulWidget` used where only reason is `setState` (must convert to `StatelessWidget`)

Stateful widgets that hold `setState`-only state (i.e., the widget has no controllers/animations that justify being stateful):

| File | Violating state field |
|------|----------------------|
| `presentation/transactions/transactions_view.dart` | `_selectedFilterIndex` |
| `presentation/statistics/statistics_view.dart` | `_selectedTabIndex` |
| `presentation/send_otp/send_otp_view.dart` | `_emailTouched`, timer |
| `presentation/register/register_view.dart` | `_firstNameTouched`, `_lastNameTouched`, `_emailTouched` |
| `presentation/select_user_type/select_user_type_view.dart` | `_shopNameTouched` + type selection |

> Note: `StatefulWidget` is **allowed** where it owns controllers/AnimationControllers (splash, auth_verification, voice_note views, etc.) — those are NOT violations for the widget type itself, but the `setState` calls inside them are still violations.

---

## Rule 2 — Logic / Side Effects in Views

| File | Violation |
|------|-----------|
| `presentation/onboarding/onboarding_view.dart` L200–201 | Calls `getIt<OnboardingLocalDataSource>().markOnboardingAsSeen()` directly in the view — this is a data/business operation. Must move to `OnboardingCubit`. |
| `presentation/splash/splash_view.dart` L178–185 | `_progressController` status listener sets `_animationFinished` and calls `_navigate()` with `_destination` — navigation decision logic (`switch` on destination) lives inside `_navigate()` in the view; navigation must happen from the `listener` of `BlocConsumer` reacting to a Cubit state. |
| `presentation/transactions/transaction_by_id_view.dart` L1069–1089 | Helper methods `_getTypeColor()` / `_getTypeTextColor()` return `Color` based on business `type` — this is a formatting/business decision inside a view. |
| `presentation/send_otp/send_otp_view.dart` L109–141 | `_startTimer()` method with its own `Timer` lives in the view; timer countdown is business logic and must live in `SendOtpCubit`. |
| `presentation/customers/widgets/contacts_search_bar.dart` L54 | Debounce `Timer` created directly in a widget — debounce logic belongs in `ContactsCubit`. |

---

## Rule 3 — Dispose / Resource Leaks

Most views **do** call `dispose()` on controllers — ✅ generally compliant.  
However:

| File | Issue |
|------|-------|
| `presentation/splash/splash_view.dart` L214 | `_splashCubit.close()` called manually — Cubits provided via `BlocProvider` are auto-closed; calling `.close()` manually here may cause double-close issues. |
| `presentation/customers/contacts_cubit/contacts_cubit.dart` | Owns a `_debounceTimer` — need to verify it is cancelled in `close()`. |
| `presentation/transactions/get_list_transaction/get_list_transaction_cubit.dart` | Owns a `_debounceTimer` — need to verify it is cancelled in `close()`. |
| All cubits with `isClosed` check | ✅ Good — `isClosed` is already used before `emit` in async code (multiple cubits). |

---

## Rule 4 — `Image.network` / `NetworkImage`

- **`Image.network`**: No occurrences found ✅
- **`NetworkImage`**: No occurrences found ✅
- **`CachedNetworkImage`**: **Zero usages found anywhere in `lib/`** ❌

**Violation:** Network images may be loaded elsewhere (e.g., profile avatars, transaction contacts). Need to audit any place a URL string from a model is passed to an image widget. Once found, they must use `CachedNetworkImage` via a shared `AppCachedImage` widget.  
Also: **no shared `AppCachedImage` widget exists** — it must be created.

---

## Rule 5 — Flat Widget Tree / No `_buildXxx()` Helper Methods

| File | Issue |
|------|-------|
| `presentation/voice_note/get_voice_note_cubit/get_voice_note_view.dart` | Uses `_build...()` helper methods; file is 800+ lines |
| `presentation/voice_note/get_voice_note_by_id_cubit/get_voice_note_by_id_view.dart` | `_build...()` helpers |
| `presentation/voice_note/create_voice_note_cubit/create_voice_note_view.dart` | `_build...()` helpers; file is 1400+ lines |
| `presentation/transactions/transactions_view.dart` | `_build...()` helpers |
| `presentation/transactions/pay_installment/pay_installment_sheet.dart` | `_build...()` helpers |
| `presentation/transactions/get_transaction_by_id/transaction_by_id_view.dart` | `_build...()` helpers |
| `presentation/statistics/statistics_view.dart` | `_build...()` helpers; file is 1400+ lines |
| `presentation/send_otp/send_otp_view.dart` | `_build...()` helpers |
| `presentation/select_user_type/select_user_type_view.dart` | `_build...()` helpers |
| `presentation/register/register_view.dart` | `_build...()` helpers |
| `presentation/operations/quick_sale_view.dart` | `_build...()` helpers |
| `presentation/operations/quick_purchase_view.dart` | `_build...()` helpers |
| `presentation/onboarding/onboarding_view.dart` | `_getFooterWidget()` method |
| `presentation/installments/installments_view.dart` | `_build...()` helpers |
| `presentation/installments/installments_history/installments_history_view.dart` | `_build...()` helpers |
| `presentation/home.dart` | 10793-byte file — `_build...()` helpers |
| `presentation/home/widgets/recent_transactions_section.dart` | `_build...()` helpers |
| `presentation/customers/customers_view.dart` | `_build...()` helpers |
| `presentation/customers/contact_profile_view.dart` | `_build...()` helpers |
| `presentation/customers/contact_form_view.dart` | `_build...()` helpers |
| `presentation/auth_verification/auth_verification.dart` | `_build...()` helpers |

**Rule:** All `_buildXxx()` helper methods returning widgets must be extracted to `const StatelessWidget` classes.

---

## Rule 6 — Performance (`const`, `ListView.builder`)

| File | Issue |
|------|-------|
| `presentation/home/widgets/mizan_drawer.dart` L34 | `ListView(children: [...])` — must use `ListView.builder` |

`const` coverage not fully audited yet (requires `flutter analyze` output) — will be verified post-analysis.

---

## Rule 7 — Dependency Injection ✅ (mostly compliant)

All Cubits are obtained via `getIt<...>()` in the view layer. ✅  
One notable issue:

| File | Issue |
|------|-------|
| `presentation/onboarding/onboarding_view.dart` L200–201 | `getIt<OnboardingLocalDataSource>()` called directly in view — data source must only be used by Cubits/repositories, not views. |

---

## Rule 8 — Strict Typing / `dynamic` / `print()`

### `print()` violations (3 occurrences):
| File | Line |
|------|------|
| `presentation/send_otp/cubit/send_otp_cubit/send_otp_cubit.dart` | 46 |
| `presentation/resources/media_type_enum.dart` | 11 |
| `presentation/register/cubit/register_cubit.dart` | 48 |

### `dynamic` violations (outside `fromJson`/`toJson`):
| File | Issue |
|------|-------|
| `presentation/resources/routes_manager.dart` L84, L193 | `Route<dynamic>` return type — should be `Route<Object?>` |
| `data/network/error_handler.dart` L11, L69, L116 | `dynamic error`, `dynamic data`, `dynamic errors` params — must be typed (e.g., `Object`, `Map<String, Object?>`) |
| `data/network/auth_interceptor.dart` L87 | `Map<String, dynamic>.from(o.headers)` — acceptable at network boundary but could be `Map<String, Object?>` |
| `data/data_source/auth_remote_data_source.dart` L7–26 | `Map<String, dynamic>` params for `verifyOtp`, `selectUserType`, `logout` — these are request bodies; should use typed request classes |

> Note: All `dynamic` in generated `.g.dart` files (`app_api.g.dart`, `*.g.dart` response files) are **auto-generated** and are allowed per Rule 8's exception for `fromJson`/`toJson` boundaries.

---

## Rule 9 — Hard-Coded Strings / Colors / Sizes

### Hard-coded `Color(0xFF...)` / `Colors.xxx` in views (outside `resources/`):

| File | Sample violations |
|------|-------------------|
| `presentation/voice_note/get_voice_note_cubit/get_voice_note_view.dart` | `Color(0xFF5E48B8)`, `Color(0xFFF0EDFC)`, `Color(0x06000000)`, `Colors.transparent` |
| `presentation/voice_note/get_voice_note_by_id_cubit/get_voice_note_by_id_view.dart` | `Color(0x0A000000)`, `Color(0xFF5E48B8)`, `Color(0xFFF0EDFC)`, `Colors.transparent` |
| `presentation/voice_note/create_voice_note_cubit/create_voice_note_view.dart` | `Color(0x0A000000)`, `Color(0xFF5E48B8)`, `Color(0xFFF0EDFC)`, `Colors.white`, `Colors.transparent` |
| `presentation/transactions/transactions_view.dart` | `Color(0x061C1816)`, `Colors.transparent` |
| `presentation/transactions/get_transaction_by_id/transaction_by_id_view.dart` | `Color(0xFFEEF2FF)`, `Color(0xFF4F46E5)` |
| `presentation/send_otp/send_otp_view.dart` | `Color(0x1A000000)`, `Color(0xFFEBF3EC)`, `Color(0xFFFFF9EE)`, `Color(0xFFE8D8B7)`, `Color(0xFFE53935)` |
| `presentation/operations/quick_sale_view.dart` | `Colors.black`, `Colors.transparent` |
| `presentation/operations/quick_purchase_view.dart` | `Colors.black`, `Colors.transparent` |
| `presentation/transactions/pay_installment/pay_installment_sheet.dart` | `Colors.black.withAlpha(90)`, `Colors.transparent` |
| `presentation/statistics/statistics_view.dart` | `Colors.transparent` |
| `presentation/splash/splash_view.dart` | `Colors.transparent` |
| (many more files with `Colors.transparent`) | All `Colors.transparent` should be `ColorManager.transparent` |

**Colors that must be added to `ColorManager`:**
- `Color(0xFF5E48B8)` — voice note type (purple)
- `Color(0xFFF0EDFC)` — voice note type container
- `Color(0xFFEEF2FF)` — transaction type bg
- `Color(0xFF4F46E5)` — transaction type color
- `Color(0xFFE53935)` — error red variant
- `Color(0xFFFFF9EE)`, `Color(0xFFE8D8B7)` — warning tones
- `Colors.transparent` → `ColorManager.transparent`

---

## Rule 9b — `analysis_options.yaml` Missing Required Lints

Current `analysis_options.yaml` only has `include: package:flutter_lints/flutter.yaml` with no custom rules.

**Missing analyzer settings:**
```yaml
analyzer:
  language:
    strict-casts: true
    strict-inference: true
    strict-raw-types: true
linter:
  rules:
    - prefer_const_constructors
    - prefer_const_literals_to_create_immutables
    - prefer_const_declarations
    - avoid_unnecessary_containers
    - sized_box_for_whitespace
    - use_key_in_widget_constructors
    - always_specify_types
    - avoid_dynamic_calls
    - avoid_print
    - always_declare_return_types
    - type_annotate_public_apis
```

---

## Rule 10 — Splash Preload (LAST — do not fix yet)

Current splash state does NOT implement full preload:
- `SplashCubit.decide()` only checks session/onboarding — it does **not** prefetch startup data in parallel with `Future.wait`
- No `SplashState` variants: `initial` / `loading` / `success(destination)` / `failure(message)` — current state is a generic wrapper
- No minimum display time constant in `AppDurations`
- No global timeout for preload
- No prefetching of critical images with `precacheImage`
- No caching of startup data to local storage for instant next-launch

---

## Summary Table

| # | Rule | Severity | Files Affected |
|---|------|----------|---------------|
| 1 | `setState` forbidden | 🔴 Critical | 11+ files, 50+ occurrences |
| 1b | `ValueNotifier` instead of Cubit | 🔴 Critical | 1 file |
| 2 | Logic in views | 🔴 Critical | 5+ files |
| 3 | Dispose leaks | 🟡 Medium | 2–3 cubits |
| 4 | No `CachedNetworkImage` / no `AppCachedImage` | 🔴 Critical | App-wide |
| 5 | Helper `_buildXxx()` methods, deep nesting | 🟡 Medium | 20+ files |
| 6 | `ListView(children:[])` | 🟠 Minor | 1 file |
| 7 | Data source called in view | 🔴 Critical | 1 file |
| 8 | `print()`, `dynamic` outside JSON boundary | 🟠 Minor | 4 files |
| 9 | Hard-coded `Color`/`Colors.xxx` in views | 🟡 Medium | 10+ files |
| 9b | `analysis_options.yaml` incomplete | 🟡 Medium | 1 file |
| 10 | Splash preload not implemented | 🔴 Critical (last) | splash feature |

---

## Proposed Refactor Order (feature-by-feature)

1. **`analysis_options.yaml`** — add all required lints first
2. **Resources** — add missing `ColorManager` entries (transparent, purple tones, etc.)
3. **`send_otp`** — setState → Cubit state, timer → Cubit, ValueNotifier → Cubit
4. **`register`** — setState (touched flags) → Cubit state
5. **`select_user_type`** — setState → Cubit state
6. **`auth_verification`** — setState → Cubit state (if any)
7. **`onboarding`** — move `markOnboardingAsSeen` to Cubit; navigation from listener
8. **`transactions`** — setState (filter index, search) → Cubit state
9. **`statistics`** — setState (tab index, filter) → Cubit state
10. **`voice_note`** — setState (playback state, recording state) → Cubit state
11. **`operations`** — setState → Cubit state
12. **`customers`** — debounce timer → Cubit; setState → Cubit state
13. **`installments`** — setState → Cubit state
14. **`home`** — extract widgets, fix ListView
15. **Shared** — create `AppCachedImage` widget, replace all image usages
16. **`print()`** — replace with logger
17. **`dynamic`** — fix routes_manager, error_handler
18. **`_buildXxx()` helpers** — extract to `const StatelessWidget` classes across all features
19. **Rule 10 — Splash preload** — implement last
