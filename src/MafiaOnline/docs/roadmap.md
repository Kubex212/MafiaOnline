# Roadmap

Items to complete before considering the project done.

---

## Tier 0 — Credibility fixes (do first)

- [x] Delete `WeatherForecastController.cs` and `WeatherForecast.cs` (default scaffold leftover)
- [x] Fix `Console.WriteLine` in `LobbyHub.cs` → use injected `ILogger`
- [x] Remove redundant `ExecuteSqlRaw` calls from `Program.cs` startup (column + index already created by the `AddLastActivityAtUtc` migration)
- [ ] Write the README (currently just a title + blank line)

---

## Tier 1 — High impact, low effort

- [ ] **`docker-compose.yml`** — Dockerfile exists; compose file lets any reviewer run the project in one command (Postgres + app)
- [ ] **GitHub Actions CI** — no `.github/workflows/` at all; a build + test pipeline is the first thing many teams check
- [ ] **README with screenshots** — what the game is, live demo link, tech stack badges, architecture flow, how to run locally

---

## Tier 2 — Hot topics that show backend depth

- [ ] **Redis** — `_timers` in `NightService` is an in-memory `Dictionary` that breaks on restart and horizontal scaling; swap for Redis-backed distributed timers + cache session lookups (currently hits Postgres on every request via `SessionService`)
- [ ] **Health checks** — `/health` endpoint checking EF Core + Redis; ASP.NET Core built-in, ~1 hour, shows production awareness
- [ ] **Rate limiting** — ASP.NET Core built-in `AddRateLimiter`; apply to auth + lobby creation endpoints; ~1 hour

---

## Tier 3 — Advanced (if time allows)

- [ ] **OpenTelemetry traces** — `opentelemetry-dotnet` with ASP.NET Core instrumentation, export to Jaeger or OTLP console
- [ ] **Integration tests** — Testcontainers for .NET spinning up a real Postgres; exercise the full lobby lifecycle end-to-end
