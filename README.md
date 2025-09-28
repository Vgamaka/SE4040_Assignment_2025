# SE4040_Assignment_2025

Monorepo for Enterprise Application Development assignment.

## Structure
- apps/web      → Web UI (Backoffice + Operator)
- apps/android  → Native Android app (Owner + Operator)
- apps/backend  → ASP.NET Core Web API (IIS-hosted) + MongoDB
- docs/report   → Evidence, logs, and final report
- scripts       → Utilities for packaging/deployment

## Quick Start
Web:     cd apps/web && npm i && npm run dev
Android: cd apps/android && ./gradlew :app:assembleDebug
Backend: cd apps/backend && dotnet run
