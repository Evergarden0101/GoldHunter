# GoldHunter

A 2½-minute, four-player gold rush brawler for **Unity**, written in C#.

Four prospectors spawn at base camps 25 m from a central coin popper. Mine it,
punch the gold out of each other, spend at the shops, and bank more than anyone
else before the whistle. **Only gold sitting in your base camp at the end
counts** — whatever is still in your bag is worth nothing.

Empty seats are filled by NPCs with distinct, Inspector-tunable personalities
that navigate, fight, shop and raid on their own.

![The arena mid-match](docs/match.png)

*(Screenshot from the [browser prototype](reference/web-prototype) the C# port
was built from — same rules, same balance, same arena.)*

---

## Running it

1. Open the repository root as a Unity project (made with **2022.3 LTS**; any
   version with C# 9 support will do).
2. Create an empty scene, add an empty GameObject, and put
   **`GoldHunterBootstrap`** on it.
3. Press Play.

The bootstrap builds the camera, lighting, managers, spawners and HUD at
runtime, so there is nothing to wire by hand. Once you want prefabs and art,
delete it and lay the same components out in the scene yourself — they are
ordinary MonoBehaviours.

By default seat 1 is human and the other three are NPCs.

### Controls

| Seat | Move | Punch | Dash |
| --- | --- | --- | --- |
| P1 | `W A S D` | `Space` | `Left Shift` |
| P2 | `I J K L` | `O` | `U` |
| P3 | Arrow keys | `/` | `.` |
| P4 | Numpad `8 4 5 6` | Numpad `0` | Numpad `.` |

Every binding is editable per seat on `MatchManager`. Gamepads use the legacy
Input Manager (left stick, button 0 punch, button 1 dash).

- **Tap punch** → fast jab. **Hold punch** → the fist charges (up to 1.15 s) and
  the release rips far more gold, knocks further, and freezes the frame harder.
- Inside a shop the buttons change: **dash cycles the selection**, **hold punch
  to buy**.
- `P` pauses, `R` restarts.

---

## Tuning it from the editor

Everything is data. Create **Assets ▸ Create ▸ GoldHunter ▸ Game Config** and
assign it to `MatchManager`:

| Where | What |
| --- | --- |
| Motherlode ▸ **Gold Per Minute** | Centre popper's **popping speed** (200/min) |
| Motherlode ▸ Starting Gold / Capacity / Harvest Rate | 50 g, 320 g cap, 34 g/s siphon |
| Small Popper ▸ … | The two flanking machines (20 g, 80/min) |
| Match | Duration (150 s), countdown, Gold Rush timing and multiplier |
| Player | Speed, **Bag Capacity** (40 g), dash, stun |
| Combat | Punch timings, steal fractions, knockback, **hit-stop** durations |
| Camp | Deposit rate, steal fraction/cap/cooldown |
| Shop Catalogue | Every item's price curve and max level |
| Upgrade | What one level of each upgrade actually does |

**Assets ▸ Create ▸ GoldHunter ▸ NPC Profile** exposes an archetype's drives —
attack will, save-gold will, steal will, shop will, greed, caution, and per-item
shopping taste. **GoldHunter ▸ Difficulty** scales how *well* bots execute
(reaction time, aim jitter, charge discipline) without changing what they want.

No gameplay value is hard-coded anywhere outside `GameConfig`.

---

## Architecture

The project is split in two, and the split is enforced by the compiler:

```
Assets/Scripts/
  Core/    plain C#, NO UnityEngine reference  (asmdef: noEngineReferences)
  Unity/   MonoBehaviours, ScriptableObjects, views
```

`GoldHunter.Core.asmdef` sets `"noEngineReferences": true`, so the simulation
*cannot* accidentally take a dependency on the engine — it stops compiling if
someone adds `using UnityEngine`. That is what makes the whole game testable
headlessly (see below).

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

**AI and humans share one code path.** `NpcBrain` never moves a player or throws
a punch; it writes into a `VirtualController` and `PlayerState` consumes it
exactly as it consumes a keyboard. A bot can only ever do something a human
could also do.

**The core never draws or plays anything.** It reports what happened through
`ISimulationListener` — punch landed, vault raided, gold banked — and the Unity
layer decides what that looks like.

### Where things live

| Folder | Owns |
| --- | --- |
| `Core/Config` | Every gameplay number, as serializable plain classes |
| `Core/Math` | `Vec2`, easing, seeded RNG, collision helpers |
| `Core/Input` | `IController`, edge-detected buttons, `VirtualController` |
| `Core/Navigation` | Nav grid, A\*, string pulling, path following, separation |
| `Core/Simulation` | The match: entities, combat resolution, arena build |
| `Core/Services` | **StageService**, **ShoppingService**, **BaseCampService** |
| `Core/Ai` | Utility scoring over seven goals, shop planning |
| `Core/Events` | The listener contract and its event structs |
| `Unity/Managers` | `MatchManager`, **StageManager**, **BaseCampManager**, **ShopManager** |
| `Unity/Config` | ScriptableObject wrappers for the config |
| `Unity/Actors` | Player and coin popper views + spawners |
| `Unity/Input` | Keyboard and gamepad adapters |
| `Unity/Fx` | `FxDirector` — the sole `ISimulationListener` |
| `Unity/UI` | IMGUI HUD (no uGUI/TextMeshPro dependency) |

Rule of thumb: if it needs to see **more than one entity** (punch resolution,
deposits, purchases, scoring) it belongs in `MatchSimulation` or a service. If
it only touches one entity's own state, it belongs on that entity.

### The three managers

- **`StageManager`** — the scene's authority on the map. Wraps `StageService`
  and answers *"where is the arena, can a body stand here, and what is
  interactable at this position?"*. `QueryInteractable` returns
  `CoinPopper` / `Shop` / `OwnBaseCamp` / `EnemyBaseCamp` / `None`, and physics,
  the AI and the UI all route through it so there is exactly one definition of
  the playfield. Draws arena, blocker and nav-grid gizmos.
- **`BaseCampManager`** — owns the four vaults in the scene: spawns the camp
  views, keeps them in step with vault totals, flashes the alarm on a raid, and
  answers standings queries. Camps are never given colliders, because a camp has
  to stay walkable or its owner could never step in to deposit.
- **`ShopManager`** — the shopping front end: stalls, who is browsing, and each
  customer's panel. Pricing and funding rules live in `ShoppingService`.

---

## How a match plays

| | |
| --- | --- |
| Match length | 150 s after a 3-2-1 countdown |
| Base camps | 4, evenly spaced on the diagonals, 25 m from the centre |
| Motherlode | starts 50 g, generates **200 g/min**, holds 320 |
| Small poppers (×2) | start 20 g, generate **80 g/min**, hold 160 |
| Bag | 40 g to start, upgradeable to 140 g |
| Gold Rush | last 25 s — poppers pump **2.5×** plus a 60 g dump |

The layout is rotationally fair: every player is the same distance from the
Motherlode, from one small popper and from one shop — asserted by a test.

```
                small popper
    camp NW  ...................  camp NE
                [pillars]
    shop W   ...  MOTHERLODE  ...  shop E
                [pillars]
    camp SW  ...................  camp SE
                small popper
```

- Stand in a popper's ring to siphon it. Punching one shakes coins loose onto
  the floor — fast, but wasteful.
- Walk into your own camp to bank. Banked gold is safe from punches.
- A jab rips **35 %** of the victim's bag; a full charge up to **80 %**. 75 % goes
  to the attacker, the rest sprays across the floor for anyone to scoop.
- With **Steal**, punch an enemy *base camp* to take 25 % of their vault (max
  70 g, 4.5 s cooldown per camp).

### The shop

Shops bill your **bag first, then your vault** — so an upgrade always costs
final score. A price shown in amber means the vault has to chip in.

| Item | Cost | Max | Effect |
| --- | --- | --- | --- |
| Attack Up | 28 / 52 / 76 / 100 | 4 | +22 % gold ripped and knockback per level |
| Defense Up | 28 / 52 / 76 / 100 | 4 | −18 % gold lost per hit, resist knockback |
| Gold Bag Up | 30 / 56 / 82 / 108 | 4 | +25 carry capacity |
| Base Camp Up | 36 / 70 / 104 | 3 | −30 % vault theft, +35 % deposit speed, +4 % end bonus |
| Scale Up | 34 | 3 | Bigger: +22 % reach, +18 % power, −10 % speed |
| Scale Down | 34 | 3 | Smaller: +10 % speed, smaller target, weaker punch |
| Steal | 52 | 1 | Punch enemy base camps to rob their vaults |

Scale Up and Scale Down share one axis (−3 … +3), so buying one walks back the
other.

### The NPCs

Every tick each bot scores seven goals — mine, bank, hunt a carrier, shop, raid
a vault, grab loose coins, flee — and commits to the best, with hysteresis so
they don't dither.

| Bot | Attack | Bank | Steal | Plays like |
| --- | --- | --- | --- | --- |
| **Bruno** (Bruiser) | 90 % | 42 % | 45 % | Hunts carriers, buys Attack and Scale Up |
| **Coinsworth** (Banker) | 20 % | 92 % | 15 % | Farms and banks constantly, armours the vault |
| **Sly** (Thief) | 55 % | 45 % | 95 % | Saves for Steal, then raids vaults |
| **Pip** (All-round) | 55 % | 60 % | 50 % | Balanced |

Movement is real navigation: blockers are rasterised into a grid, routes come
from A\* with line-of-sight string pulling, and local separation stops bots
clumping. Enable *Draw Nav Grid* on `StageManager` to see it.

### Feel

- **Hit stop** freezes the *simulation* clock on every connect — 55 ms for a
  jab, up to 190 ms for a full charge — while presentation keeps running on
  `Time.unscaledDeltaTime`, so impacts read as a snap rather than a dropped
  frame.
- **Coin poppers shake** continuously while being drained, jolt on every
  generation tick, and convulse when punched. Shake is simulation state, so
  every renderer agrees on it.
- Camera shake on a trauma model, squash-and-stretch driven by the punch state
  machine, charge tint, camp alarm flashes on a raid.

---

## Testing

Unity is not needed to verify the game:

```bash
tools/run-tests.sh                       # Unity layer compiles + core tests
tools/run-tests.sh --matches 12 --verbose
tools/run-tests.sh --difficulty hard
```

Two things run:

1. **`tools/CoreTests`** plays full 150-second NPC-vs-NPC matches at a fixed
   timestep, in milliseconds. It asserts gold conservation, that matches reach a
   result, that bots navigate/mine/bank/fight/shop, that at least one vault raid
   happens, and that nobody finishes with an empty vault — plus focused checks
   on arena symmetry, the octagon clamp, tap-vs-hold detection, pathing around
   blockers, hit-stop, and the shop's funding order.
2. **`tools/UnityStubCheck`** compiles the whole Unity layer against a stub
   `UnityEngine`, so a typo in a MonoBehaviour fails here instead of when
   someone opens the editor.

Sample output:

```
GoldHunter core balance — 12 matches · difficulty=normal
  winners            Banker:8  Bruiser:2  All-round:2
  scores             top 227g · median 128g · low 67g
  fighting           21 punches landed/match · 2.5 vault raids/match
  economy            9 upgrades bought/match · 371g spent
  AI goal share      Mine 34%  Bank 23%  Raid 21%  Hunt 11%  Shop 10%
```

**The gold ledger.** Poppers are the only source. There are exactly two sinks:
gold spent at a shop, and gold that evaporates on the floor uncollected
(`MatchSimulation.GoldExpired`). The test asserts `world + spent + expired`
never decreases, which is how the port's three separate rounding leaks were
found and fixed.

---

## Notable design decisions

- **Shops bill bag then vault.** Charging the bag alone was tried first: the bag
  is small, so it becomes a hard price ceiling, and expensive items can only be
  bought by loitering with a fat unbanked bag — exactly what rivals punch out of
  you. That made Steal effectively unbuyable and let the passive Banker win
  10 of 12 matches. Vault funding keeps every upgrade reachable and makes the
  price honest, since it comes off your score.
- **Bots save for what they actually want.** A bot within half the price of its
  preferred item waits rather than spending on the cheap shelf. Saving is free
  here because the funds sit safely in the vault — without this rule the Thief
  never accumulates enough for Steal and the vault-raiding game never happens.
- **Bots keep a spending reserve** that ramps up as the clock runs down;
  otherwise they convert their whole vault into upgrades and finish near zero.
- **The leader gets hunted.** Bots weight attacks and raids 1.3× against
  whoever is ahead, which stops a 2½-minute match being decided at 0:40.

**Known balance characteristic:** Coinsworth (Banker) still wins more than its
share of bot-only matches — turtling is genuinely strong when nobody at the
table is a human applying pressure. `tools/run-tests.sh --matches 12` prints
the winner spread if you want to push on it.

---

## The web prototype

`reference/web-prototype/` holds the original browser implementation this port
was built from — vanilla ES modules and canvas, playable by opening
`reference/web-prototype/dist/goldhunter.html`. It is kept because it is the
provenance of the balance numbers and its own harness (`npm test` in that
folder) still runs. It is not part of the Unity build.

## Licence

MIT.
