# CLAUDE.md

Guidance for Claude Code when working in this repository.

## What this is

GoldHunter is a 4-player, 150-second Unity arena game in C#. Players mine gold
from coin poppers, punch it out of each other, buy upgrades at shops, and bank
it at their base camp. Highest vault at the whistle wins.

The repository root **is** the Unity project (`Assets/`, `ProjectSettings/`).
Built against Unity 2022.3 LTS, C# 9.

`reference/web-prototype/` is the original browser implementation the C# port
came from. It still runs and still has its own harness, but it is not part of
the Unity build — treat it as provenance for the balance numbers, not as code
to keep in sync.

## Commands

```bash
tools/run-tests.sh                        # the gate for any gameplay change
tools/run-tests.sh --matches 12 --verbose # balance sweep with per-player detail
tools/run-tests.sh --difficulty hard

dotnet build tools/UnityStubCheck         # just the compile check
dotnet run --project tools/CoreTests -c Release -- --matches 6
```

Everything runs without a Unity licence. The .NET SDK is the only requirement.

## The split, and why it is load-bearing

```
Assets/Scripts/Core/     plain C#, NO UnityEngine   (asmdef: noEngineReferences: true)
Assets/Scripts/Unity/    MonoBehaviours and views   (references GoldHunter.Core)
```

`GoldHunter.Core.asmdef` sets `"noEngineReferences": true`. **Do not remove
that flag.** It is what stops the simulation from drifting into the engine; the
moment a `using UnityEngine` appears in `Core/`, compilation fails. That
constraint is what makes the whole game testable headlessly in milliseconds.

Data flows one way each frame:

```
IController (keyboard | gamepad | virtual)
        │                          ▲
        │                          │ NpcBrain writes here
        ▼                          │
   PlayerState.Tick ──> MatchSimulation.Tick ──> ISimulationListener
                                                        │
                                                 FxDirector, views, HUD
```

## Invariants — do not break these

1. **A settings class holds every gameplay number, and lives beside its
   consumer.** `CoinPopperSettings` is at the top of `CoinPopper.cs`,
   `CombatSettings` at the top of `CombatResolver.cs`, and so on; `Core/Config`
   holds only the `GameConfig` aggregate root. If you are typing a literal like
   `0.35` or `150` into a simulation file, it belongs in that file's settings
   class instead, surfaced through `GameConfigAsset`.
   **Tunables are public fields, deliberately.** Unity serialises public fields
   of a `[Serializable]` class and never properties, and the core cannot use
   `[field: SerializeField]` because it has no UnityEngine reference. Convert
   one to an auto-property and it compiles, then quietly disappears from the
   Inspector. Anything *derived* from a tunable should be a property
   (`GoldPerSecond`, `ChargeSpan`, `DiagonalLimit`).
2. **The AI writes to a controller, never to a player.** `NpcBrain` fills a
   `VirtualController`; `PlayerState` consumes it exactly like a keyboard. Never
   add a path that lets a brain move a player or throw a punch directly.
3. **The core raises events; it never draws, plays audio or shakes anything.**
   Add to `ISimulationListener` and handle it in `FxDirector`.
4. **Hit stop scales the simulation clock, not the presentation clock.**
   `MatchSimulation.Tick(realDt)` derives the sim delta via `HitStopClock`.
   Unity ticks it with `Time.unscaledDeltaTime` and views animate on unscaled
   time. Feeding both the same scaled value makes impacts look like frame drops.
5. **Only vault gold scores.** Bag gold is worth nothing at the whistle; the
   AI's endgame banking branch depends on it.
6. **The gold ledger.** Poppers are the only source. There are exactly two
   sinks: shop spending, and pickups expiring uncollected
   (`MatchSimulation.GoldExpired`). Anything else that changes the total is a
   bug, and `CoreTests` will catch it — it asserts `world + spent + expired`
   never decreases.
7. **Purchases bill bag first, then vault** (`ShoppingService`). This was a
   deliberate reversal — see "History" below before changing it back.
8. **Camps are not solid; poppers, shops and rocks are.** `StageService` builds
   the blocker list and deliberately omits camps. `BaseCampManager` destroys any
   collider on a camp view. A solid camp means nobody can ever deposit.

## Where code goes

| Need | Home |
| --- | --- |
| A gameplay number | the settings class atop the file that reads it, surfaced in `GameConfigAsset` |
| Logic touching one entity's own state | that entity in `Core/Simulation` |
| Logic touching **two or more** entities | `MatchSimulation` or a service |
| "Where is the map / what is at this point" | `Core/Services/StageService` |
| Pricing, funding, affordability | `Core/Services/ShoppingService` |
| Banking, raiding, standings, final score | `Core/Services/BaseCampService` |
| Bot decision-making | `Core/Ai/NpcBrain`, `ShopPlanner` |
| Anything visible or audible | `Unity/Fx/FxDirector`, `Unity/Actors`, `Unity/UI` |

## Testing approach

`tools/CoreTests` is the real test suite. It plays complete matches at a fixed
timestep and asserts behaviour coverage, not just "no crash": bots must move,
mine, bank, fight, shop, and land at least one vault raid across the sample, and
nobody may finish with an empty vault. `UnitChecks.cs` adds focused checks on
arena symmetry, the octagon clamp, tap-vs-hold detection, pathing around
blockers, hit-stop semantics and the shop's funding order.

`tools/UnityStubCheck` compiles the Unity layer against a hand-written stub
`UnityEngine` (`tools/UnityStubCheck/UnityEngineStub.cs`). It proves the code
compiles and the API shapes are right; it proves nothing about runtime behaviour
in the editor. **If you use a Unity API the stub lacks, add it to the stub** —
keep the stub faithful to the real signatures, or it will report false errors
(it already caught two, both missing `Mathf` int overloads).

Healthy balance targets when tuning:

- No archetype wins much more than half of matches.
- `Mine` 30-40 %, `Bank` 20-30 %, `Hunt` 10-20 %, `Flee` under 10 %.
- Nobody finishes on 0 g; median score roughly 120-220 g.
- At least ~1 vault raid per match (proves the Steal chain works end to end).

## Things that bit during the port

- **Partial pickups.** Collecting a floor blob into a nearly-full bag used to
  delete the whole blob. Always use the return value of `AddGold` and put back
  or keep what did not fit.
- **Rounding after moving gold.** Rounding a value *after* taking it out of a
  bag, vault or popper creates or destroys the difference. Round the request,
  then move exactly what came back.
- **`Math.Round` is banker's rounding in C#** (`Round(0.5) == 0`), unlike
  JavaScript's `Math.round`. Ported numbers can drift slightly because of this.
- **Unity initialisation order.** `GoldHunterBootstrap` adds `MatchManager`
  last, so any component resolving it in `Awake` finds nothing. `HudController`
  resolves lazily for exactly this reason.
- **A one-frame tap must still register.** `KeyboardController` latches with
  `GetKey(k) || GetKeyDown(k)`; polling alone drops a jab that starts and ends
  inside one frame. The browser prototype shipped with that bug.

## Known balance characteristic

Coinsworth (Banker) still wins more than its share of bot-only matches —
turtling is strong when no human is applying pressure. Vault raids are the
designed counterweight and now fire ~2 per match. If you push on this, change
one multiplier at a time and re-measure with `--matches 12`; making a goal more
attractive to *every* profile tends to make those bots worse while the passive
one quietly wins.

## History worth knowing

Purchases originally billed the **bag only**, which seemed like nice risk/reward
(walk your money to the shop). It failed in measurement, twice:

1. The cheapest item cost more than the 40 g starting bag, so no first purchase
   was ever possible.
2. After repricing, bag size became a hard price ceiling. Steal could only be
   afforded by standing around with a full unbanked bag — precisely what rivals
   punch out of you. Bots hoarded, got robbed, never bought it, and the passive
   Banker archetype won 10 of 12 matches.

Billing bag-then-vault fixed both and deleted an entire hoarding subsystem from
the AI. If you are tempted to restore bag-only pricing, re-read this and run a
12-match sweep first.
