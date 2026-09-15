# MafiaOnline — Project Overview for AI Agents

## What This Project Is

MafiaOnline is a real-time, multiplayer implementation of the classic **Mafia / Werewolf** party game, playable in a browser. Players join lobbies, are assigned secret roles, and take turns playing through **Night** phases (role abilities, kills) and **Day** phases (public voting to eliminate a suspect) until one faction wins.

---

## Solution Structure

```
src/MafiaOnline/
├── MafiaOnline.sln
├── Api/                        ← ASP.NET Core 8 Web API + frontend host
│   ├── Controllers/            ← REST endpoints (Auth, Lobby, Day, Night, Me)
│   ├── Data/                   ← EF Core DbContext + entity models
│   ├── Dtos/                   ← Request / response shapes
│   ├── Hubs/                   ← SignalR hub (LobbyHub)
│   ├── Identity/               ← CurrentUser / ICurrentUser
│   ├── Middleware/             ← SessionMiddleware (token → user)
│   ├── Migrations/             ← EF Core PostgreSQL migrations
│   ├── Models/                 ← Domain models: Lobby, LobbyPlayer, NightSession, DaySession, Role
│   ├── Repositories/           ← ILobbyRepository (Postgres impl)
│   ├── Services/               ← LobbyService, NightService, DayService, StatisticsService, …
│   ├── Utils/                  ← LobbyCodeGenerator
│   ├── Program.cs              ← App bootstrap, DI, auth setup
│   └── mafiaonline.frontend/   ← React 18 + TypeScript + Vite SPA (served as static files)
│       └── src/                ← Components, api.ts, i18n (en + pl)
├── MafiaOnline.Engine/         ← Pure C# game engine (no ASP.NET deps)
│   ├── Core/                   ← Game, RoleFactory, commands
│   ├── Config/                 ← GameConfig + per-role config classes
│   ├── Roles/                  ← IRole, RoleType enum, all role implementations
│   ├── Night/                  ← NightContext, NightResolver, NightEvent, NightAbility
│   └── Day/                    ← DayResult, DaySession
└── MafiaOnline.Tests/          ← xUnit unit tests (NightResolver, roles, serialization)
```

---

## Technology Stack

| Layer | Technology |
|---|---|
| Backend API | ASP.NET Core 8 (C#) |
| Real-time | SignalR (`/lobbyHub`) |
| Database | PostgreSQL via EF Core (Npgsql) |
| Authentication | Cookie auth + OAuth2 (Discord, Google) → custom session tokens |
| Frontend | React 18 + TypeScript + Vite |
| i18n | i18next (English + Polish) |
| Logging | Serilog (console + rolling file logs under `Api/logs/`) |
| Tests | xUnit |

---

## Authentication & Sessions

- Users sign in via **Discord OAuth** or **Google OAuth** (`/api/auth/discord/callback`, `/api/auth/google/callback`).
- On successful OAuth, `ExternalAuthService.FindOrCreateUserAsync` creates or retrieves the user record; `SessionService.CreateForUserAsync` issues a **session token**.
- The token is returned as a query parameter (`?token=…`) and stored client-side.
- `SessionMiddleware` resolves the token on every request and populates `ICurrentUser`.
- `CurrentUser` / `ICurrentUser` carry the authenticated user's ID into services and controllers.

---

## Game Flow

### Lobby Lifecycle
1. A player creates a lobby (receives a short code, e.g. `ABC123`).
2. Other players join with the code.
3. The host configures roles via `GameConfig`, then starts the game.
4. After a game ends, players can **rematch** (players mark ready → new game in same lobby).

### Game Phases (alternating)
```
Night → Day → Night → Day → … → Game Over
```

**Night phase**
- `LobbyService.BeginNightAsync` → starts `NightSession`, broadcasts via SignalR.
- Each living player with an interactive ability submits a `NightAction` (target selection) within a turn timer.
- `NightService.SubmitActionAsync` / `SkipNightTurnAsync` collect actions.
- Once all actions are submitted, `LobbyService.ResolveNightAsync` calls `NightResolver` in the engine to compute kills, clues, and special effects.
- Results are broadcast as `NightResolved` + per-player clue events over SignalR.

**Day phase**
- `DayService.BeginDayAsync` broadcasts who died (and day-visible effects like `enshrouded`, `asleep`).
- Each living player casts one vote (`DayController.Vote`).
- When all votes are in (or timeout), `DayService.ResolveDayAsync` eliminates the plurality candidate and checks win conditions.

---

## Game Engine (`MafiaOnline.Engine`)

The engine is a **pure domain library** (no HTTP, no EF Core). Key concepts:

- **`Game`** — aggregate root; holds players, turns, phase, `LastNightResult`, `LastDayResult`.
- **Commands** — `AddPlayerCommand`, `StartGameCommand`, `StartNightCommand`, `ResolveNightCommand`, `ResolveDayCommand` — mutate `Game` state.
- **`RoleFactory`** — creates role instances from `RoleType` + `GameConfig`.
- **`NightAbility`** — base class for interactive/automatic night abilities; has priority, cooldown, min/max targets, `IsAutomatic`.
- **`NightContext`** — mutable bag of state collected during ability resolution (attack targets, protection sets, clue dictionaries, traps, flags, etc.).
- **`NightResolver`** — processes all submitted actions in priority order, resolves interactions (Guardian protection, Poacher traps, Deceiver reversal, Drunk reversal, etc.), produces `NightEvent` list.
- **`NightEvent`** — serializable event with a `DisplayedClue` and `GetAdditionalInfoDictionary()` for frontend display.

---

## Roles

Roles are split into three **teams**:

| Team | Roles |
|---|---|
| **Villagers** | Villager, Guardian, Detective, DogTamer, Poacher, Trailer, Seer, Peeper, Spy, Ranger, Alchemist, Sorcerer, Innkeeper |
| **Werewolves** | Werewolf, Dracula, Deceiver, Ghost, Charmer |
| **Neutral** | Fool |

Roles that give **night clues**: Detective, Seer, DogTamer, Trailer, Peeper, Spy, Ranger, Alchemist, Sorcerer, Innkeeper.

See **`docs/add-role.md`** for the complete checklist on how to add a new role (engine + API + frontend + tests).

---

## Configuration (`GameConfig`)

Key settings (all configurable per-lobby before game start):

| Property | Default | Description |
|---|---|---|
| `WerewolfCount` | 1 | How many werewolves in the game |
| `TurnDurationSeconds` | 30 | Night-action timer |
| `EnabledRoles` | All roles except Villager & Werewolf | Which special roles can appear |
| `Guardian.*` | — | Guardian ability config |
| `Detective.*` | — | Detective ability config |
| `DogTamer.*` | — | Dog Tamer ability config |
| `Poacher.*` | — | Poacher trap config |
| `Ghost.*` | — | Ghost ability config |
| `Innkeeper.*` | — | Innkeeper ability config |
| `Werewolf.*` | — | Werewolf team config |
| `Day.*` | — | Day voting config |

---

## API Surface

### REST Controllers

| Controller | Purpose |
|---|---|
| `AuthController` | Discord/Google OAuth initiation + callback |
| `LobbyController` | Create/join/leave lobby, update config, move/shuffle players, start game, reconnect |
| `NightController` | Submit night action / skip turn |
| `DayController` | Cast vote, resolve day |
| `MeController` | Current user info + stats |

### SignalR Hub: `LobbyHub` (`/lobbyHub`)

Broadcasts real-time game events to lobby members (player joined/left, game started, night/day phase transitions, vote updates, game over, etc.).

---

## Frontend (`mafiaonline.frontend`)

- **React 18 + TypeScript**, built with Vite, served as static files from the API host.
- Key source files:
  - `src/api.ts` — all HTTP + SignalR client calls, TypeScript types for all DTOs, role lists, clue types.
  - `NightPhase.tsx` — night UI, clue display, role targeting.
  - `DayPhase.tsx` — day UI, voting, day-visible effects.
  - `GameConfigPanel.tsx` / `GameConfigView.tsx` — pre-game role/config editing.
  - `RoundTable.tsx` — seat display with player state flags.
  - `i18n/locales/en/` + `pl/` — translation strings (role names, clues, log messages).

---

## Data Persistence

- **PostgreSQL** stores users, external logins (Discord/Google provider IDs), sessions (tokens), and game history.
- **Lobby state is in-memory** (`PostgresLobbyRepository` stores serialized `Lobby` JSON in the DB, but a running lobby lives in memory during the session).
- EF Core migrations are auto-applied on startup (`context.Database.Migrate()`).

---

## Tests (`MafiaOnline.Tests`)

- `NightTests.cs` — core night resolver scenarios.
- `NightInnkeeperTests.cs`, `NightPoacherAndTrailerTests.cs`, `SorcererTests.cs` — role-specific ability tests.
- `RoleTests.cs` — verifies `RoleFactory` constructs every role correctly.
- `LobbySerializationTests.cs` — round-trip JSON serialization of `Lobby`.

---

## Key Conventions & Gotchas

- **Ability priority order** DOES NOT MATTER: 
- **Deceiver (role) / Drunk(modifier)** can flip clue events; new clue-producing roles must be checked against these interactions in `NightResolver`.
- **`NightEvent` subclasses** must be JSON-serializable (follow patterns in `NightEvent.cs`; include `GetAdditionalInfoDictionary`).
- **Frontend and server must stay in sync**: `RoleType` enum, clue types (`ClueType` union in `api.ts`), and DTO shapes must match on both sides.
- **Translations required**: every role needs English (`en`) and Polish (`pl`) entries for name, short/long description, clue strings, and log strings.
- **Minimum 4 players** required to start a game.
- **Reconnect support**: the API has a `/reconnect` endpoint that restores full lobby + game state for a rejoining player.
