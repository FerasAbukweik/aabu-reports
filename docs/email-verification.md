# Email verification

Login and registration send a six-digit email code. They do not issue authentication cookies.
`POST /api/auth/verify-email` accepts `{ "studentId": "1234567890", "code": "123456" }`
and completes sign-in. `POST /api/auth/resend-code` accepts `{ "studentId": "1234567890" }`.
Verification uses the unique student id in the request and Redis key; no verification cookie is used.

Codes are stored directly in Redis. `Email:VerificationCodeLifetimeMinutes` in `appsettings.json`
controls both cache expiry and the lifetime stated in the verification email (default: five minutes).
Resending overwrites the previous code for that student. Verification reads and compares the code;
Redis removes it automatically when it expires. The frontend waits 45 seconds before allowing resend.
Incorrect guesses count toward Identity account lockout, and verification endpoints limit requests per IP.

## SMTP configuration

For Docker Compose, set these values in your local `.env` (do not commit credentials):

```dotenv
SMTP_HOST=smtp.example.com
SMTP_PORT=587
SMTP_ENABLE_SSL=true
SMTP_FROM=reports@example.com
SMTP_USERNAME=reports@example.com
SMTP_PASSWORD=your-smtp-password
```

For a directly running API, use the corresponding `Email__Host`, `Email__Port`,
`Email__EnableSsl`, `Email__From`, `Email__Username`, and `Email__Password` environment variables.
With SSL enabled, MailKit uses implicit SSL/TLS on port 465 and requires STARTTLS
on other ports, including 587.
For a local mail catcher, disable SSL and use its SMTP port. Codes are never logged.

Registration remains transactional: if email delivery fails, account creation rolls back.
If verification expires or the dialog is closed after a successful registration, use login
with the new credentials to request a fresh verification code.
