# CLAUDE.md

Guidance for Claude Code when working in this repository.

## What this is

GoldHunter is a 4-player, 150-second arena game. Browser only, vanilla ES
modules, canvas 2D, **zero runtime dependencies**. Players mine gold from coin
poppers, punch it out of each other, buy upgrades at shops, and bank it at their
base camp. Highest vault at the whistle wins.

Node is used only for tooling (bundler, tests, screenshots). Playwright drives a
real Chromium for the tests.

## Commands

```bash
npm run build      # bundle src/ -> dist/goldhunter.html + dist/artifact.html
npm test           # build + 6 NPC matches + 12 real-keyboard input checks
npm run balance    # 12-match sweep with per-player detail (tuning aid)
npm run shots      # regenerate docs/*.png
npm run dev        # serve source at :8080 (ES modules need http, not file://)
```

`npm test` takes about a minute and is the gate for any gameplay change.
Playwright resolves from the global install; override with `PW=` and `CHROME=`
if the environment differs.

## Architecture

Data flows one way each frame:

```
InputHub ──> Controller (keyboard | gamepad | virtual)
                  │                       ▲
                  │                       │ NpcBrain writes here
                  ▼                       │
              Player.update ──> World.update ──> Fx ──> Renderer + Hud
```

The important consequence: **AI and humans are the same code path.** `NpcBrain`
does not move a player or throw a punch; it writes `want.ax/ay/attack/action`
into a `VirtualController`, and `Player.update` consumes it exactly as it
consumes a keyboard. Never add a shortcut that lets the AI bypass `Player`.

| File | Owns |
| --- | --- |
| `src/config.js` | Every gameplay number. Nothing else hard-codes one. |
| `src/core/input.js` | Key/pad polling, tap-vs-hold buttons, virtual controllers |
| `src/core/fx.js` | Particles, rings, floating text, screen shake, **hit stop** |
| `src/core/audio.js` | WebAudio synth SFX (no asset files) |
| `src/game/world.js` | Arena build, sim step, and every multi-entity interaction |
| `src/game/entities.js` | Per-entity state and presentation timers |
| `src/game/npc.js` | Utility scoring over seven goals + combat behaviour |
| `src/game/nav.js` | Nav grid, A\*, string pulling, path following |
| `src/game/items.js` | Shop catalogue, pricing, purchase application |
| `src/game/render.js` / `hud.js` | All drawing |
| `src/ui.js` | Lobby + results DOM screens |

Rule of thumb for where code goes: if it needs to see **more than one entity**
(punch resolution, deposits, purchases, scoring) it belongs in `world.js`. If it
only touches one entity's own state, it belongs in `entities.js`.

## Invariants — do not break these

1. **`src/config.js` is the only place gameplay numbers live.** If you find
   yourself typing a literal like `0.35` or `150` into a system file, add it to
   config instead.
2. **Hit stop scales the simulation clock, not the presentation clock.**
   `World.update(realDt)` calls `fx.simDt(realDt)` for the simulation and feeds
   `fx.update()` the *real* dt. Feeding both the same value makes impacts look
   like frame drops.
3. **The AI writes to a controller, never to the player.** See above.
4. **Purchases bill bag first, then vault** (`items.js: funds/buy`). This was a
   deliberate reversal — see "History" below before changing it back.
5. **Only vault gold scores.** Bag gold is worth nothing at the whistle; the
   endgame branch in the AI's `bank` scoring depends on this.
6. **Gold is conserved except at shops.** Poppers are the only source; shop
   spending is the only sink. If a change makes gold appear or vanish anywhere
   else, that is a bug.
7. **Camps are not solid; poppers, shops and rocks are.** Camps must be walkable
   or nobody can deposit.
8. **`dist/` is committed and must be rebuilt** whenever `src/` or `styles.css`
   changes — the standalone build and the artifact are generated from it.

## Testing approach

Two harnesses, and they catch different things:

- `tools/smoke.js` steps the world directly at a fixed dt with four bots. Fast,
  deterministic per seed, and it asserts behaviour coverage, not just "no
  crash": bots must move, mine, bank, fight, shop, and land at least one vault
  raid across the sample, and nobody may finish with an empty vault. It prints a
  balance summary; use `--matches N --verbose --difficulty hard`.
- `tools/input-test.js` presses **real keys in a real browser**. The bot harness
  drives virtual controllers, so it passes even when keyboard handling is
  entirely broken — which is exactly the bug it found (a jab tapped and released
  within one frame was dropped, since polling never saw the key down; fixed by
  latching presses in `InputHub.keyboardController`).

When tuning balance, run `npm run balance` before and after and compare the
winner spread and goal-share lines. Healthy targets:

- No archetype wins more than ~half of matches.
- `mine` 30-45 %, `bank` 20-30 %, `hunt` 15-25 %, `flee` under 10 %.
- Nobody finishes on 0 g; median score roughly 120-220 g.
- At least one vault raid per few matches (proves the Steal chain works).

## The AI, concretely

`NpcBrain.evaluate()` scores seven goals and keeps the best:

`mine`, `bank`, `hunt`, `shop`, `raid`, `loot`, `flee`

Every score has the shape `value / (travelTime × k + 1)` multiplied by
personality weights from `NPC_PROFILES`. Small multipliers do a lot of work
here; change one at a time and re-measure. Hysteresis terms (`this.goal === x &&
this.target === y`) exist to stop goal thrashing — keep them.

Known couplings that bit during tuning, so watch for them:

- `flee` targets home, and stepping into your own camp **auto-deposits**. Any
  "run away" behaviour is therefore also a banking behaviour.
- Making a goal more attractive to every profile can make those bots *worse*
  (they chase an expensive plan, get robbed, never finish it) while the passive
  profile quietly wins. Always check the winner spread, not just the goal share.
- Bots must keep a spending reserve that ramps up as the clock runs down, or
  they will convert their whole vault into upgrades and finish near zero.

## Rendering notes

- The camera fits the whole octagonal arena; `Renderer.fit()` is
  height-constrained at typical aspect ratios, which is what leaves room for the
  four corner HUD cards.
- World-space text is drawn by scaling the context by `0.1` and using ~10-13 px
  fonts, so labels stay legible at any zoom. Keep that convention.
- `ctx.roundRect` is used throughout; `main.js` polyfills it for older Safari.
- Player HUD cards sit in the screen corner matching their camp (NW/NE/SW/SE).
- Shop panels open *outward* into the side margin so they never cover the fight.

## Build

`tools/build.js` is a dependency-free bundler: it walks imports from
`src/main.js`, rewrites `import`/`export` into a tiny module registry (each
module gets its own scope, so identically-named module-level constants like
`rng` in `fx.js` and `entities.js` cannot collide), and inlines `styles.css`.

It emits two files: `dist/goldhunter.html` (full standalone page) and
`dist/artifact.html` (the same page as a body fragment, for publishing as a
Claude Artifact, which supplies its own `<head>`/`<body>`).

Only relative imports are supported. There is no dead-code elimination and no
minifier — keep it that way unless there is a reason.

## History worth knowing

Purchases originally billed the **bag only**, which seemed like nice risk/reward
(walk your money to the shop). It failed in measurement, twice:

1. The cheapest item cost more than the 40 g starting bag, so no first purchase
   was ever possible.
2. After repricing, the bag size became a hard price ceiling. Steal could only
   be afforded by standing around with a full unbanked bag, which is precisely
   what rivals punch out of you. Bots hoarded, got robbed, and never bought it —
   the passive Banker archetype won 10 of 12 matches.

Billing bag-then-vault fixed both and simplified the AI (an entire hoarding
subsystem was deleted). If you are tempted to restore bag-only pricing, re-read
this and run `npm run balance` first.
