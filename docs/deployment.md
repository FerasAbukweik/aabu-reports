# Docker deployment

The frontend is served at `http://localhost:4000` and calls the API directly at
`http://localhost:8080`. Nginx serves the frontend files and handles Angular routes.
Compose uses its normal default network with automatically assigned container addresses.
No fixed frontend IP or trusted-proxy configuration is required.

To rebuild and restart the app:

```sh
docker compose up -d --build
```

When switching from the previous custom network, recreate every service together:

```sh
docker compose up -d --build --force-recreate
```

Existing named data volumes are preserved.
Refresh cookies are scoped to `/api/auth`; logout revokes the refresh token and clears the cookies.
Codes retain the simple student-ID Redis lookup and expire automatically as configured.

Pagination accepts 1–100 rows per request and a nonnegative offset. Report attachments are limited
to five images of at most 5 MB each. Chat messages are limited to 500 characters.
