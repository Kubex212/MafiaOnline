# Adding a new role

This document describes the typical steps required to add a new role to the project. It covers backend (engine + API), frontend (React/TypeScript), configuration, logging and tests. Follow the checklist at the end before opening a PR.

---

## Quick overview

- Add the role to the RoleType enum and the RoleFactory.
- Implement the role class and, if applicable, its night ability.
- Wire ability data into `NightContext` and `NightResolver`.
- Add any new `NightEvent` types for clues/effects.
- If the ability has day-visible effects, include them in the DayStarted broadcast.
- Add or extend configuration where needed.
- Update frontend API typings and components to display clues/config and allow user actions.
- Add translations and tests.

---

## Backend (engine & API)

1. Add an enum entry
   - File: `MafiaOnline.Engine.Roles.RoleType` (`MafiaOnline.Engine\Roles\IRole.cs`)
   - Add the new `RoleType` value. Follow existing naming and ordering conventions.

2. Create the role class and/or ability
   - Implement the role in `MafiaOnline.Engine.Roles` (new file or alongside similar roles).
   - If the role has a night ability: implement a `NightAbility` derivative (put it in the same file as the role for discoverability).
   - Override ability metadata (priority, cooldown, min/max targets, automatic/interactive, etc.).
   - Implement `Apply(...)` to mutate the `NightContext` (see step 4).

3. Register the role in the factory
   - File: `MafiaOnline.Engine.Core.RoleFactory` (`MafiaOnline.Engine\Core\RoleFactory.cs`)
   - Add a case that returns your role instance. If the role uses per-role config, pass the config object.

4. Update `NightContext` when needed
   - File: `MafiaOnline.Engine.Night.NightContext` (`MafiaOnline.Engine\Night\NightContext.cs`)
   - If your ability needs to record shared data during resolution (e.g. a map of clues, traps, boolean flags or sets), add appropriately typed properties (Dictionary/HashSet/List) and initialize them.

5. Add NightEvent(s)
   - File: `MafiaOnline.Engine.Night.NightEvent` (`MafiaOnline.Engine\Night\NightEvent.cs`)
   - If the ability produces a clue or event that should be serialized and shown to clients, add an event class (e.g. `MyRoleClueEvent`).
   - Implement `DisplayedClue` / `GetAdditionalInfoDictionary` when helpful for API serialization.

6. Resolve effects in `NightResolver`
   - File: `MafiaOnline.Engine.Night.NightResolver` (`MafiaOnline.Engine\Night\NightResolver.cs`)
   - Use data stored in `NightContext` to:
     - Create `NightEvent`s (clues, blocked attacks, etc.).
     - Add killed player IDs to the killed list.
     - Trigger secondary effects (poacher, deceiver reversal, drunk reversal, etc.).
   - Follow existing ordering and interaction patterns (guardian protections, poacher traps, deceiver reversal, drunk reversing clues, removing clues for sleeping players).

7. Day-visible effects & DayService
   - File: `Api.Services.DayService.BeginDayAsync` (`Api\Services\DayService.cs`)
   - If your ability produces an effect visible on Day start (like `enshrouded` or `asleep`), add it to the payload sent in `SendAsync("DayStarted", ...)`.
   - Keep payload shapes consistent with existing entries.

8. Configuration
   - File: `MafiaOnline.Engine.Config.GameConfig` (`MafiaOnline.Engine\Config\GameConfig.cs`)
   - Add role-specific configuration properties if required and provide sensible defaults in `GetDefault()`.

9. API / Reconnect / Serialization
   - If you add new fields/events sent over the hub or via API (reconnect/night clue endpoints), ensure DTO shapes and JSON serialization are consistent.
   - Update reconnect response shapes if night/day state needs the new data (see `Reconnect` DTO in `api.ts`).

---

## Frontend (TypeScript / React)

1. Update role list & types
   - File: `Api\mafiaonline.frontend\src\api.ts`
     - If you added a new clue type, extend the `ClueType` union and add the new response interface to `AnyClueResponse`.
     - If you added a new role, add it to `roleTypes` so UI controls know about it.

2. Night UI
   - File: `NightPhase.tsx`
     - If the role emits a clue, display it in both the per-turn log and the resolution UI.
     - If the role requires user targeting/actions, ensure `RoundTable` selection and `NightPhase` respect the ability `minTargets`/`maxTargets` and forbidden-target logic.
     - Use `submitNightAction`/`skipNightTurn` from `api.ts` for interactive actions.

3. Day UI
   - File: `DayPhase.tsx`
     - If your ability produces day-visible effects, add handling similar to `enshrouded` and `asleep`.
     - Update seat flags in `RoundTable` so the effect is visible in the table (e.g. a new `isEnshrouded` flag).

4. Configuration UI
   - Files: `GameConfigPanel.tsx` and `GameConfigView.tsx`
     - If the role has config options, add controls to the panel and read-only displays to the view.
     - Make sure the JSON shape matches server `GameConfig`.

5. API types & client code
   - File: `api.ts`
     - Update TypeScript types for any new clues/events or modified payloads so the frontend compiles and runtime shapes match.

6. Translations & role text
   - Add English and Polish entries in the i18n locale files (e.g. `src/.../i18n/locales/en/common.json` and `pl/common.json`).
   - Provide:
     - role name
     - short & long descriptions
     - clue strings (short/long)
     - any new log strings

7. UX polish
   - If the role needs special UI hints, update `RoleHint.tsx`, `RoleInfo.tsx`, and `RoundTable.tsx` as needed.

---

## Tests

- Backend tests
  - Add unit tests in `MafiaOnline.Tests` that exercise the role's ability and its interactions (NightResolver behaviors, Poacher/Guardian interplay, deceiver/drunk flips).
  - Ensure `RoleFactory` test (RoleTests.cs) passes: RoleFactory must construct your role and `role.Type` equals the enum value.

- Integration / API tests
  - Add tests for hub messages and day/night flows if practical.

- Frontend tests (if present)
  - Exercise UI change handling for clues/config and state restoration.

---

## Additional tips & gotchas

- Ability ordering: abilities are executed in priority order and by seat index. Pick priority carefully to ensure intended interactions.
- Deceiver / Drunk interactions: the engine supports reversing clue events; consider this when designing clues.
- Serialization: `NightEvent` derivatives must be JSON-serializable. Follow `NightEvent.cs` patterns and include `GetAdditionalInfoDictionary` for friendly payloads.
- Default role set: `GameConfig.GetDefault()` removes `Villager` and `Werewolf` from defaults — ensure whether your role should be enabled by default.
- Naming: keep event/class names consistent with existing suffixes (`Event` for night events, `Ability` for abilities).
- Frontend/server parity: server and TS typings must agree on field names and shapes. Update both sides together.
- Run full build and tests after changes:
  - Backend: build solution and run unit tests.
  - Frontend: run TypeScript build and sanity check in the browser.

---

## Minimum PR checklist

- [ ] Added role enum entry
- [ ] Implemented role and ability (server)
- [ ] Registered role in RoleFactory
- [ ] Added/updated NightContext fields if needed
- [ ] Created NightEvent(s) and used them in NightResolver
- [ ] DayService updated for day-visible effects (if applicable)
- [ ] GameConfig updated (if applicable)
- [ ] API DTOs / typings updated (`api.ts`)
- [ ] Frontend UI updated (NightPhase/DayPhase/GameConfigPanel/GameConfigView)
- [ ] Translations added/updated (en + pl)
- [ ] Unit tests added and passing
- [ ] End-to-end check: play a game exercising the new role and verify logs/clues/config behavior


If you discover additional interactions or places to update (hub messages, reconnect payloads, role hint content, etc.), add them to the checklist and include tests verifying the behavior.
