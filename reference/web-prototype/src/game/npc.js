/**
 * NPC brain.
 *
 * Bots are *not* scripted: every tick they score a handful of candidate goals
 * (mine, bank, mug a carrier, shop, raid a vault, grab loose coins, run away)
 * and commit to the winner with a bit of hysteresis. Personality weights from
 * NPC_PROFILES bias those scores, so "Bruno" hunts players while "Coinsworth"
 * quietly farms and banks.
 *
 * Movement goes through PathFollower (A* + string pulling) so bots route around
 * rocks and shops instead of grinding into them.
 */

import { CAMP, SHOP, ITEMS, COMBAT, MATCH } from '../config.js';
import { clamp, dist, makeRng } from '../core/math.js';
import { PathFollower, separation } from './nav.js';
import { levelOf, priceOf, isMaxed, funds } from './items.js';

const GOALS = ['mine', 'bank', 'hunt', 'shop', 'raid', 'loot', 'flee'];

export class NpcBrain {
  constructor(player, profile, difficulty, grid, seed = 1) {
    this.p = player;
    this.profile = profile;
    this.diff = difficulty;
    this.rng = makeRng(seed);
    this.follow = new PathFollower(grid);

    this.goal = 'mine';
    this.target = null;
    this.goalScore = 0;
    this.think = 0;
    this.reaction = 0;
    this.attackHold = 0;       // >0 => hold the punch button
    this.wantCharge = false;
    this.actionPulse = 0;
    this.shopWant = null;
    this.shopCooldown = 0;
    this.shopPurchaseMark = 0;
    this.lastShop = null;
    this.stuckTimer = 0;
    this.lastPos = { x: player.x, y: player.y };
    this.wanderAngle = this.rng.angle();
    this.dashUrge = 0;
  }

  /* ------------------------------------------------------------ utilities */

  travelTime(x, y) {
    const d = dist(this.p.x, this.p.y, x, y);
    return d / Math.max(1.5, this.p.speed * this.diff.speed);
  }

  /** Taste ranking over the shelf, optionally capped by a price ceiling. */
  _rankItems(world, maxPrice = Infinity) {
    const bias = this.profile.shopBias;
    let best = null, bestScore = 0;
    for (const def of ITEMS) {
      if (isMaxed(this.p, def.id)) continue;
      if (priceOf(this.p, def.id) > maxPrice) continue;
      const w = (bias[def.id] ?? 0.5);
      if (w <= 0.05) continue;
      let score = w * (1 - levelOf(this.p, def.id) * 0.12);
      if (def.id === 'steal') score *= 0.6 + this.profile.stealWill;
      if (def.id === 'baseCampUp' && world.leaderIndex === this.p.index) score *= 1.4;
      if (score > bestScore) { bestScore = score; best = def; }
    }
    return best;
  }

  /**
   * What this bot should buy on its next shop run, or null.
   *
   * Shops bill the bag first and the vault for the rest, so "can I afford it"
   * is simply funds vs price. Bots keep a late-match reserve: spending vault
   * gold in the closing seconds would just donate the win away.
   *
   * @param {boolean} affordableOnly restrict to what it can pay for right now
   */
  desiredItem(world, affordableOnly = false) {
    const p = this.p;
    this.dreamId = null;

    const dream = this._rankItems(world);
    if (!dream) return null;
    this.dreamId = dream.id;
    if (!affordableOnly) return dream;

    // Spending discipline: the vault is the score, so a bot only dips into it
    // while there is still time to earn the gold back. The reserve it refuses
    // to touch ramps up as the clock runs down.
    const reserve = (1 - world.timeLeft / MATCH.duration) * 150;
    const budget = Math.max(0, funds(p) - reserve);
    if (budget >= priceOf(p, dream.id)) return dream;

    // Otherwise take the best thing the budget does cover.
    let best = null, bestPrice = 0;
    for (const def of ITEMS) {
      if (isMaxed(p, def.id)) continue;
      const price = priceOf(p, def.id);
      if (price > budget) continue;
      const w = (this.profile.shopBias[def.id] ?? 0.5);
      if (w <= 0.05) continue;
      if (!best || w > bestPrice) { best = def; bestPrice = w; }
    }
    return best;
  }

  nearestThreat(world) {
    let best = null, bestD = Infinity;
    for (const o of world.players) {
      if (o === this.p || !o.alive) continue;
      const d = dist(this.p.x, this.p.y, o.x, o.y);
      if (d < bestD) { bestD = d; best = o; }
    }
    return { who: best, d: bestD };
  }

  /* --------------------------------------------------------- goal scoring */

  evaluate(world) {
    const p = this.p;
    const timeLeft = world.timeLeft;
    const scores = {};
    const targets = {};

    // ---- shopping intent (drives both the shop goal and how eager we are to bank)
    const homeD = this.travelTime(p.home.x, p.home.y);
    const endgame = timeLeft < homeD + 4;
    const shopWant = (this.shopCooldown <= 0 && !endgame) ? this.desiredItem(world, true) : null;

    // ---- bank ------------------------------------------------------------
    if (p.bag > 0) {
      const fill = p.bag / p.bagCap;
      let s = (p.bag / 35) * (0.6 + this.profile.saveGoldWill * 1.3) / (homeD * 0.5 + 1);
      s *= 0.6 + fill * 1.3;
      if (endgame) s += 100;                       // bagged gold scores nothing
      if (p.bagSpace <= 1) s *= 1.8;               // full bag: nothing else to do
      if (p.home.vault < 50) s *= 1.5;             // get something on the board first
      scores.bank = s;
      targets.bank = p.home;
    }

    // ---- mine ------------------------------------------------------------
    if (p.bagSpace > 3 && !endgame) {
      for (const pop of world.poppers) {
        const gettable = Math.min(pop.gold, p.bagSpace);
        if (gettable < 3) continue;
        const t = this.travelTime(pop.x, pop.y);
        const contest = world.playersNear(pop.x, pop.y, pop.reach + 2.5, p).length;
        let s = (gettable / 40) * (0.9 + this.profile.greed * 0.5) / (t * 0.5 + 1);
        s *= 1 - contest * 0.18 * (1 - this.profile.attackWill);
        if (pop.kind === 'big') s *= 1.12;
        if (this.goal === 'mine' && this.target === pop) s *= 1.15;   // hysteresis
        if (!scores.mine || s > scores.mine) { scores.mine = s; targets.mine = pop; }
      }
    }

    // ---- hunt ------------------------------------------------------------
    for (const o of world.players) {
      if (o === p || !o.alive) continue;
      const loot = Math.min(o.bag, p.bagSpace);
      if (loot < 5) continue;
      const t = this.travelTime(o.x, o.y);
      if (t > 3.2 && this.profile.attackWill < 0.7) continue;
      // Chasing is expensive: it only wins when the mark is close and loaded.
      let s = (loot / 45) * (0.3 + this.profile.attackWill * 1.5) / (t * 1.25 + 1);
      if (o.stun > 0) s *= 1.5;
      if (o.upgrades.defenseUp > p.upgrades.attackUp + 1) s *= 0.7;
      if (o.index === world.leaderIndex) s *= 1.3;   // whoever is winning gets the attention
      if (this.goal === 'hunt' && this.target === o) s *= 1.2;
      if (!scores.hunt || s > scores.hunt) { scores.hunt = s; targets.hunt = o; }
    }

    // ---- shop ------------------------------------------------------------
    if (shopWant) {
      let bestShop = null, bestT = Infinity;
      for (const sh of world.shops) {
        const t = this.travelTime(sh.x, sh.y);
        if (t < bestT) { bestT = t; bestShop = sh; }
      }
      if (bestShop) {
        // Finally affording the thing you actually wanted beats another ore run.
        const eager = shopWant.id === this.dreamId ? 1.6 : 1.0;
        scores.shop = (0.85 + this.profile.shopWill * 1.4) / (bestT * 0.45 + 1) * 1.25 * eager;
        targets.shop = bestShop;
        this.shopWant = shopWant.id;
      }
    }

    // ---- raid ------------------------------------------------------------
    if (p.canSteal && p.bagSpace > 4 && !endgame) {
      for (const camp of world.camps) {
        if (camp.owner === p.index) continue;
        if (p.lastStolenFrom.get(camp.owner) > 0) continue;
        const owner = world.players[camp.owner];
        const loot = Math.min(camp.vault * CAMP.stealFraction * owner.campArmor, CAMP.stealCap, p.bagSpace);
        if (loot < 6) continue;
        const t = this.travelTime(camp.x, camp.y);
        const guarded = dist(owner.x, owner.y, camp.x, camp.y) < 8 ? 0.55 : 1;
        // A stocked vault is worth a long walk — one punch can out-earn a whole
        // ore run, which is the entire point of paying for Steal.
        let s = (loot / 26) * (0.55 + this.profile.stealWill * 2.5) / (t * 0.35 + 1) * guarded;
        if (camp.owner === world.leaderIndex) s *= 1.3;
        if (!scores.raid || s > scores.raid) { scores.raid = s; targets.raid = camp; }
      }
    }

    // ---- loot ------------------------------------------------------------
    if (p.bagSpace > 3) {
      for (const g of world.pickups) {
        if (g.dead) continue;
        const t = this.travelTime(g.x, g.y);
        if (t > 2.2) continue;
        const s = (g.amount / 25) * (0.9 + this.profile.greed) / (t * 1.4 + 0.6);
        if (!scores.loot || s > scores.loot) { scores.loot = s; targets.loot = g; }
      }
    }

    // ---- flee ------------------------------------------------------------
    const threat = this.nearestThreat(world);
    const FLEE_RANGE = 6;
    if (threat.who && p.bag > p.bagCap * 0.5 && threat.d < FLEE_RANGE && threat.who.stun <= 0) {
      const danger = (1 - threat.d / FLEE_RANGE) * threat.who.attackMul;
      let s = danger * (0.3 + this.profile.caution * 1.5) * (p.bag / p.bagCap) * 0.9;
      if (threat.who.charging) s *= 1.6;      // a wound-up smash is worth dodging
      scores.flee = s;
      targets.flee = p.home;
    }

    let bestGoal = 'mine', best = -Infinity;
    for (const g of GOALS) {
      const s = scores[g] ?? -Infinity;
      if (s > best) { best = s; bestGoal = g; }
    }
    if (best === -Infinity) { bestGoal = 'mine'; targets.mine = world.poppers[0]; }

    this.goal = bestGoal;
    this.target = targets[bestGoal] || null;
    this.goalScore = best;
    if (bestGoal !== 'shop') this.shopWant = null;   // never act on a stale plan
  }

  /* --------------------------------------------------------------- update */

  update(dt, world) {
    const p = this.p;
    const c = p.controller;
    if (!c) return;
    this.shopCooldown = Math.max(0, this.shopCooldown - dt);
    this.attackHold = Math.max(0, this.attackHold - dt);
    this.actionPulse = Math.max(0, this.actionPulse - dt);
    this.dashUrge = Math.max(0, this.dashUrge - dt);

    // Reaction delay makes lower difficulties feel human.
    this.think -= dt;
    if (this.think <= 0) {
      this.think = this.diff.react + this.rng.range(0, 0.12);
      this.evaluate(world);
    }

    c.want.attack = false;
    c.want.action = false;

    if (p.shop !== this.lastShop) {
      this.lastShop = p.shop;
      if (p.shop) this.shopPurchaseMark = p.purchases.length;
    }
    if (p.shop) { this._shopBehaviour(dt, world); return; }

    const goalPos = this._goalPosition(world);
    if (goalPos) this.follow.setGoal(goalPos.x, goalPos.y);
    else this.follow.clear();

    let [dx, dy] = this.follow.steer(p.x, p.y, dt);

    // Local avoidance so bots don't pile up on each other.
    const [sx, sy] = separation(p, world.players, p.radius * 2.6);
    dx += sx * 0.85;
    dy += sy * 0.85;

    // Anti-stuck: if we've barely moved while wanting to move, jitter + repath.
    const moved = dist(p.x, p.y, this.lastPos.x, this.lastPos.y);
    this.lastPos.x = p.x; this.lastPos.y = p.y;
    if (moved < 0.02 && (dx || dy)) {
      this.stuckTimer += dt;
      if (this.stuckTimer > 0.5) {
        this.wanderAngle += this.rng.range(1.4, 2.6);
        dx += Math.cos(this.wanderAngle) * 1.4;
        dy += Math.sin(this.wanderAngle) * 1.4;
        this.follow.setGoal(goalPos ? goalPos.x : p.x, goalPos ? goalPos.y : p.y, true);
        this.stuckTimer = 0;
      }
    } else this.stuckTimer = 0;

    const l = Math.hypot(dx, dy);
    if (l > 1e-4) { dx /= l; dy /= l; }
    c.want.ax = dx;
    c.want.ay = dy;

    this._combatBehaviour(dt, world, dx, dy);
  }

  _goalPosition(world) {
    const p = this.p;
    const t = this.target;
    if (!t) return null;
    switch (this.goal) {
      case 'mine': {
        // Stand just inside the harvest ring, on our side of the machine.
        const a = Math.atan2(p.y - t.y, p.x - t.x);
        const r = t.radius + p.radius + 0.5;
        return { x: t.x + Math.cos(a) * r, y: t.y + Math.sin(a) * r };
      }
      case 'bank':
        return { x: p.home.x, y: p.home.y };
      case 'flee':
        return { x: p.home.x, y: p.home.y };
      case 'hunt': {
        // Lead the target a little so we don't chase their exact past position.
        const lead = this.diff.aim * 0.35;
        return { x: t.x + t.vx * lead, y: t.y + t.vy * lead };
      }
      case 'shop':
      case 'raid':
      case 'loot':
        return { x: t.x, y: t.y };
      default:
        return null;
    }
  }

  _shopBehaviour(dt, world) {
    const p = this.p;
    const c = p.controller;
    const wantId = this.shopWant || this.desiredItem(world, true)?.id;
    if (!wantId) { this._leaveShop(c); return; }
    const idx = ITEMS.findIndex((i) => i.id === wantId);
    if (idx < 0 || isMaxed(p, wantId) || funds(p) < priceOf(p, wantId)) { this._leaveShop(c); return; }

    if (p.shopIndex !== idx) {
      // Tap the action key to advance the highlight (needs rising edges).
      this.actionPulse -= dt;
      c.want.action = this.actionPulse <= 0;
      if (c.want.action) this.actionPulse = SHOP.cycleCooldown + 0.06;
    } else {
      c.want.attack = true;      // hold to confirm
      if (p.purchases.length > this.shopPurchaseMark) {
        this.shopPurchaseMark = p.purchases.length;
        this.shopWant = null;
        this.shopCooldown = 7;
        this._leaveShop(c);
      }
    }
    // Drift out of the shop zone once done.
    c.want.ax = 0;
    c.want.ay = 0;
  }

  _leaveShop(c) {
    const p = this.p;
    this.shopCooldown = Math.max(this.shopCooldown, 3);
    const away = Math.atan2(p.y - p.shop.y, p.x - p.shop.x);
    c.want.ax = Math.cos(away);
    c.want.ay = Math.sin(away);
    c.want.attack = false;
    c.want.action = false;
  }

  _combatBehaviour(dt, world, dx, dy) {
    const p = this.p;
    const c = p.controller;
    if (!p.canAct || p.busy) { this.attackHold = 0; return; }

    // Raiding a vault: punch the camp itself.
    if (this.goal === 'raid' && this.target) {
      const d = dist(p.x, p.y, this.target.x, this.target.y);
      if (d < this.target.radius + p.radius + p.reach * 0.85) {
        this._aimAt(c, this.target.x, this.target.y);
        this.attackHold = 0.02;                     // light taps are enough
        c.want.attack = true;
        return;
      }
    }

    // Punching a player.
    const victim = this.goal === 'hunt' ? this.target : this._opportunisticTarget(world);
    if (victim) {
      const d = dist(p.x, p.y, victim.x, victim.y);
      const reach = p.radius + victim.radius + p.reach;
      const chargeWorthy = victim.bag > 25 && this.rng() < this.diff.chargeSkill;

      if (d < reach * 2.4 && this.rng() < this.diff.aim) this._aimAt(c, victim.x, victim.y);

      if (d < reach * 3.0 && d > reach * 1.05 && p.dashCooldown <= 0
          && this.dashUrge <= 0 && this.rng() < this.diff.chargeSkill * 0.5) {
        c.want.action = true;                        // dash in
        this.dashUrge = 1.6;
      }

      if (this.wantCharge || chargeWorthy) {
        this.wantCharge = true;
        c.want.attack = true;                        // keep holding
        const full = p.chargeRatio >= 0.85 - (1 - this.diff.chargeSkill) * 0.5;
        if (d <= reach * 0.95 && (full || p.chargeRatio > 0.35)) {
          c.want.attack = false;                     // release into the hit
          this.wantCharge = false;
        } else if (p.chargeTime > COMBAT.chargeFull * 1.9) {
          c.want.attack = false;                     // don't hold forever
          this.wantCharge = false;
        }
      } else if (d <= reach * 0.95) {
        this.attackHold = 0.02;
        c.want.attack = true;
      }
    } else {
      this.wantCharge = false;
    }

    // Escape dash when fleeing with a fat bag.
    if (this.goal === 'flee' && p.dashCooldown <= 0 && this.dashUrge <= 0) {
      c.want.action = true;
      this.dashUrge = 1.2;
    }
  }

  /** Someone standing right next to us with loot worth taking. */
  _opportunisticTarget(world) {
    const p = this.p;
    if (this.profile.attackWill < 0.15) return null;
    let best = null, bestD = Infinity;
    for (const o of world.players) {
      if (o === p || !o.alive) continue;
      const d = dist(p.x, p.y, o.x, o.y);
      const reach = p.radius + o.radius + p.reach;
      if (d < reach * 1.25 && o.bag > 6 && p.bagSpace > 4 && d < bestD) { bestD = d; best = o; }
    }
    return best;
  }

  _aimAt(c, x, y) {
    const p = this.p;
    const a = Math.atan2(y - p.y, x - p.x);
    const jitter = (1 - this.diff.aim) * 0.5;
    const aa = a + this.rng.range(-jitter, jitter);
    // Feed the aim through the movement stick: Player derives facing from it.
    const blend = 0.65;
    c.want.ax = c.want.ax * (1 - blend) + Math.cos(aa) * blend;
    c.want.ay = c.want.ay * (1 - blend) + Math.sin(aa) * blend;
  }

  get debugLabel() {
    return `${this.profile.tag}:${this.goal}`;
  }
}
