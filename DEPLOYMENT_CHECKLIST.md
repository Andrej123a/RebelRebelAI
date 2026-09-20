# Rebel Rebel Deployment Checklist

Before hosting publicly:

- Use `.env.production.example` as a key list and enter the values in the hosting provider's secret/environment dashboard; the application does not load this file directly.
- Set `ASPNETCORE_ENVIRONMENT=Production`.
- Provide `ConnectionStrings__DefaultConnection` through host secrets/environment variables.
- For the first deployment only, provide `AdminUser__Email` and `AdminUser__Password` through host secrets/environment variables and set `AdminUser__BootstrapEnabled=true`.
- After the first successful admin login, set `AdminUser__BootstrapEnabled=false` and remove `AdminUser__Password` from the host configuration.
- Provide SMTP settings through environment variables, especially `EmailSettings__Username` and `EmailSettings__Password`.
- Set `AllowedHosts` to the production domain; production startup intentionally rejects an empty value or `*`.
- Use a managed PostgreSQL database with backups enabled.
- Apply migrations as a controlled deployment step with `dotnet ef database update`; production startup does not modify the schema automatically.
- Confirm migrations run successfully against a staging/production copy before launch.
- Confirm HTTPS is forced by the host/reverse proxy.
- Confirm `/sitemap.xml`, `/robots.txt`, menu, events, contact, and reservation pages load without admin login.
- Confirm production users see the branded error page, not developer exception details.
- Rotate the bootstrapped admin password after first production login.
