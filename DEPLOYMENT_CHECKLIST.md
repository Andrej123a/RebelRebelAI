# Rebel Rebel Deployment Checklist

Before hosting publicly:

- Set `ASPNETCORE_ENVIRONMENT=Production`.
- Provide `ConnectionStrings__DefaultConnection` through host secrets/environment variables.
- Provide `AdminUser__Email` and `AdminUser__Password` through host secrets/environment variables.
- Provide SMTP settings through environment variables, especially `EmailSettings__Username` and `EmailSettings__Password`.
- Replace `AllowedHosts="*"` with the production domain when the domain is known.
- Use a managed PostgreSQL database with backups enabled.
- Confirm migrations run successfully against a staging/production copy before launch.
- Confirm HTTPS is forced by the host/reverse proxy.
- Confirm `/sitemap.xml`, `/robots.txt`, menu, events, contact, and reservation pages load without admin login.
- Confirm production users see the branded error page, not developer exception details.
- Rotate the seeded admin password after first production login.
