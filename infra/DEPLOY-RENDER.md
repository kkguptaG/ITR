# Deploying TallyG Tax to Render (staging) + pointing itrhelp.com via Hostinger hPanel

> **You run every step under your own logins.** Do **not** share passwords with anyone/any tool.
> This is a **private staging preview**, not a public launch — the app still has stub integrations
> (e-filing/payments/OCR) and **provisional, pending-CA tax figures**. Lock it down and complete the
> compliance gates (ERI registration, CA sign-off, security/DPDP audit) before going public.

## Why Render (and not hPanel)
Hostinger shared hosting (hPanel) runs PHP/MySQL/WordPress/static sites — it **cannot** run this
stack (ASP.NET Core 9 + Next.js SSR + PostgreSQL). Render runs all three from one blueprint
(`render.yaml`), with managed Postgres, free TLS, and custom domains. hPanel keeps the **domain +
DNS**; Render runs the **app**. (For real production later, move to an **India region** — Azure
Central India or AWS Mumbai — for DPDP data residency.)

---

## Step 1 — Put the code in a Git repo (Render deploys from Git)
The project isn't a git repo yet. From `D:\TallyGTax`:
```powershell
git init
git add .
git commit -m "TallyG Tax — initial"
# create an EMPTY private repo on github.com first (e.g. yourname/tallyg-tax), then:
git branch -M main
git remote add origin https://github.com/<you>/tallyg-tax.git
git push -u origin main
```
(`.gitignore` already excludes node_modules, bin/obj, .next, *.db, .env.)

## Step 2 — Create the services on Render
1. Sign up at render.com (free), connect your GitHub.
2. **New + → Blueprint** → pick the `tallyg-tax` repo → Render reads **`render.yaml`** and shows:
   a Postgres `tallyg-db`, a web service `tallyg-api`, a web service `tallyg-web`.
3. **Apply** → Render builds both Docker images and provisions Postgres. First build ≈ 5–10 min.
   - The API auto-seeds on boot (admin@tallyg.test / demo@tallyg.test; OTP returned as `devOtp`).
4. When live, each service has a `*.onrender.com` URL — open the API one + `/health` to confirm,
   then the web one to confirm the UI loads.

## Step 3 — Add your custom domains (in Render)
- **tallyg-web** → Settings → Custom Domains → add **`itrhelp.com`** and **`www.itrhelp.com`**.
- **tallyg-api** → Settings → Custom Domains → add **`api.itrhelp.com`**.

Render then shows the **exact DNS targets** to create (an apex **A** record IP for `itrhelp.com`,
and **CNAME** targets for `www` and `api`). Copy those values for Step 4.

## Step 4 — Point DNS in Hostinger hPanel
hPanel → **Domains → itrhelp.com → DNS / Nameservers → DNS Zone**. Add the records Render gave you
(values below are the *shape* — use the exact ones Render shows):

| Type  | Name (host) | Value (from Render)                | TTL  |
|-------|-------------|------------------------------------|------|
| A     | `@`         | `216.24.57.x` (Render apex IP)     | 3600 |
| CNAME | `www`       | `tallyg-web-xxxx.onrender.com`     | 3600 |
| CNAME | `api`       | `tallyg-api-xxxx.onrender.com`     | 3600 |

- Remove or replace any existing `@`/`www` records that point at Hostinger's parking page.
- Keep Hostinger's nameservers (don't switch to Render NS) — just edit the DNS zone records.
- DNS propagates in minutes–hours. Render auto-issues **Let's Encrypt TLS** once it resolves; the
  domains flip to "Verified / Certificate issued".

## Step 5 — Lock the preview down (important)
Right now anyone who finds the URL can self-register (and `devOtp` is returned in Development). For a
truly private preview, do **one** of:
- **Recommended:** set `ASPNETCORE_ENVIRONMENT=Production` on `tallyg-api` (stops `devOtp`), seed/use
  one known account, and add a real SMS/email OTP provider; **or**
- Keep it open but unlisted, and add a holding/`noindex` page; **or**
- Put Cloudflare Access / an IP allowlist in front.
Don't index it (no public marketing) until the compliance gates are done.

## Cost
Free tier works for staging (web services sleep when idle; free Postgres is time-limited). For an
always-on preview, the API + web + Postgres on Render's Starter plans are roughly **$7/mo each**.

## Switching to Railway / Fly / Azure instead
- **Railway:** New Project → Deploy from repo; add a Postgres plugin; set the same env vars; it
  auto-builds the Dockerfiles. Custom domains + DNS the same idea.
- **Azure Container Apps (Central India)** — best for production DPDP residency: push both images to
  ACR, create two Container Apps + Azure Database for PostgreSQL, same env vars.
The app needs **zero code changes** for any of these — only the env vars + DNS differ.
