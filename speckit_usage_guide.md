# SpecKit Usage Guide — Mizan Backend

## Architecture Overview
- Core entity: `User` with `Email`, `FirstName`, `LastName`, `UserType`.
- Identity mechanism: Email OTP (6-digit numeric codes with 120s expiry and constant-time verification).
- JWT Authentication: Bearer tokens with claims `NameIdentifier`, `Email`, `Role`, `GivenName`, `Surname`.
