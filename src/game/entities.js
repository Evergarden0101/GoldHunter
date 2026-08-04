/**
 * Entities: Player, CoinPopper, BaseCamp, Shop, Rock and GoldPickup.
 *
 * Entities own their own state and presentation timers. Anything that needs to
 * see more than one entity at a time (punch resolution, deposits, purchases)
 * lives in world.js.
 */

import {
  PLAYER, COMBAT, POPPERS, CAMP, SHOP, PICKUP, UPGRADE, COLORS, ITEMS,
} from '../config.js';
import { clamp, damp, rotateToward, TAU, makeRng } from '../core/math.js';

const rng = makeRng(0x5eed);

/* ------------------------------------------------------------------ Player */

export class Player {
  constructor(index, opts = {}) {
    this.index = index;
    this.name = opts.name || `P${index + 1}`;
    this.isHuman = !!opts.isHuman;
    this.color = COLORS.players[index];
    this.colorDark = COLORS.playersDark[index];
    this.controller = opts.controller || null;
    this.brain = null;              // set for AI players
    this.home = opts.home;          // BaseCamp
    this.alive = true;

    this.x = opts.x || 0;
    this.y = opts.y || 0;
    this.vx = 0;
    this.vy = 0;
    this.facing = opts.facing ?? 0;

    this.bag = 0;
    this.scaleLevel = 0;
    this.upgrades = Object.fromEntries(ITEMS.map((i) => [i.id, 0]));
    this.canSteal = false;
    this.purchases = [];
    this.spent = 0;

    // Combat state machine
    this.phase = 'idle';            // idle | windup | active | recover
    this.phaseTimer = 0;
    this.cooldown = 0;
    this.charging = false;
    this.chargeTime = 0;
    this.punchPower = 0;            // 0 = light, 1 = full charge
    this.hitSet = new Set();
    this.stun = 0;
    this.invuln = 0;

    this.dashTimer = 0;
    this.dashCooldown = 0;
    this.dashDirX = 0;
    this.dashDirY = 0;

    // Shop browsing
    this.shop = null;
    this.shopIndex = 0;
    this.buyHold = 0;
    this.cycleLock = 0;

    // Presentation
    this.squash = 1;
    this.hitFlash = 0;
    this.walkPhase = rng.angle();
    this.glow = 0;
    this.harvesting = 0;
    this.depositing = 0;
    this.lastStolenFrom = new Map();  // campIndex -> cooldown remaining

    // Stats snapshot for HUD / results
    this.stats = {
      robbed: 0, lost: 0, mined: 0, banked: 0, punchesLanded: 0,
      punchesTaken: 0, campRaids: 0, raidedFor: 0,
    };
  }

  /* ---- derived stats ---- */
  get scale() { return 1 + this.scaleLevel * UPGRADE.scaleStep; }
  get radius() { return PLAYER.radius * this.scale; }
  get bagCap() { return PLAYER.bagCapacity + this.upgrades.goldBagUp * UPGRADE.bagPerLevel; }
  get bagSpace() { return Math.max(0, this.bagCap - this.bag); }
  get bagFill() { return clamp(this.bag / this.bagCap, 0, 1); }
  get attackMul() {
    return (1 + this.upgrades.attackUp * UPGRADE.attackPerLevel)
      * (1 + this.scaleLevel * UPGRADE.scalePowerPerLevel);
  }
  get defenseMul() {
    return Math.pow(1 - UPGRADE.defensePerLevel, this.upgrades.defenseUp);
  }
  get reach() {
    return COMBAT.punchReach * (1 + this.scaleLevel * UPGRADE.scaleReachPerLevel);
  }
  get campArmor() {
    return Math.pow(1 - UPGRADE.campArmorPerLevel, this.upgrades.baseCampUp);
  }
  get depositRate() {
    return CAMP.depositRate * (1 + this.upgrades.baseCampUp * UPGRADE.campDepositPerLevel);
  }
  get endBonusRate() {
    return this.upgrades.baseCampUp * UPGRADE.campEndBonusPerLevel;
  }
  /** Heavy bags slow you down — carrying a fortune is a real risk. */
  get speed() {
    const load = 1 - 0.12 * this.bagFill;
    return PLAYER.speed * (1 + this.scaleLevel * UPGRADE.scaleSpeedPerLevel) * load;
  }
  get busy() { return this.phase !== 'idle'; }
  get canAct() { return this.stun <= 0; }
  get chargeRatio() {
    if (!this.charging) return 0;
    const t = (this.chargeTime - COMBAT.chargeMinHold) / (COMBAT.chargeFull - COMBAT.chargeMinHold);
    return clamp(t, 0, 1);
  }

  addGold(amount) {
    const take = Math.min(amount, this.bagSpace);
    this.bag += take;
    return take;
  }

  /* ---- update ---- */
  update(dt, world) {
    const c = this.controller;
    this.stun = Math.max(0, this.stun - dt);
    this.invuln = Math.max(0, this.invuln - dt);
    this.cooldown = Math.max(0, this.cooldown - dt);
    this.dashCooldown = Math.max(0, this.dashCooldown - dt);
    this.cycleLock = Math.max(0, this.cycleLock - dt);
    this.hitFlash = Math.max(0, this.hitFlash - dt * 4);
    this.harvesting = Math.max(0, this.harvesting - dt * 3);
    this.depositing = Math.max(0, this.depositing - dt * 3);
    for (const [k, v] of this.lastStolenFrom) {
      const n = v - dt;
      if (n <= 0) this.lastStolenFrom.delete(k); else this.lastStolenFrom.set(k, n);
    }

    const inShop = !!this.shop;
    let ax = c ? c.ax : 0;
    let ay = c ? c.ay : 0;
    const mag = Math.hypot(ax, ay);
    if (mag > 1) { ax /= mag; ay /= mag; }

    if (this.canAct) {
      this._updateAttack(dt, c, world);
      if (inShop) this._updateShop(dt, c, world);
      else if (c && c.action.pressed && this.dashCooldown <= 0 && this.phase === 'idle') {
        this._startDash(ax, ay, world);
      }
    } else {
      this.charging = false;
      this.chargeTime = 0;
      this.buyHold = 0;
    }

    // ---- movement ----
    let speed = this.speed;
    if (this.charging) speed *= COMBAT.chargeMoveSlow;
    if (this.phase === 'windup' || this.phase === 'active') speed *= 0.35;
    if (this.phase === 'recover') speed *= 0.6;

    if (this.dashTimer > 0) {
      this.dashTimer -= dt;
      this.vx = this.dashDirX * PLAYER.dashSpeed;
      this.vy = this.dashDirY * PLAYER.dashSpeed;
      if (rng.chance(0.6)) {
        world.fx.burst(this.x, this.y, {
          count: 1, color: this.color, speed: [0.5, 2], life: [0.15, 0.3],
          size: [0.12, 0.26], drag: 6,
        });
      }
    } else if (this.canAct) {
      const tvx = ax * speed;
      const tvy = ay * speed;
      const rate = (mag > 0.02 ? PLAYER.accel : PLAYER.friction) / Math.max(1, speed) * 3.4;
      this.vx = damp(this.vx, tvx, rate, dt);
      this.vy = damp(this.vy, tvy, rate, dt);
    } else {
      // Stunned: slide with the knockback.
      this.vx = damp(this.vx, 0, PLAYER.knockbackDecay, dt);
      this.vy = damp(this.vy, 0, PLAYER.knockbackDecay, dt);
    }

    this.x += this.vx * dt;
    this.y += this.vy * dt;

    const moving = Math.hypot(this.vx, this.vy);
    if (moving > 0.4 && this.canAct) {
      this.walkPhase += dt * (6 + moving * 1.5);
      const target = Math.atan2(this.vy, this.vx);
      if (!this.charging && this.phase === 'idle') {
        this.facing = rotateToward(this.facing, target, PLAYER.turnRate * dt);
      } else {
        this.facing = rotateToward(this.facing, target, PLAYER.turnRate * 0.35 * dt);
      }
    }
    // Aim overrides facing while charging so heavies can be steered.
    if (this.charging && mag > 0.2) {
      this.facing = rotateToward(this.facing, Math.atan2(ay, ax), PLAYER.turnRate * 0.9 * dt);
    }

    // Squash & stretch
    const targetSquash = this.phase === 'windup' ? 0.86
      : this.phase === 'active' ? 1.18
        : this.dashTimer > 0 ? 1.14
          : 1 + Math.sin(this.walkPhase) * 0.05 * clamp(moving / 5, 0, 1);
    this.squash = damp(this.squash, targetSquash, 18, dt);
    this.glow = damp(this.glow, this.charging ? this.chargeRatio : 0, 12, dt);
  }

  _startDash(ax, ay, world) {
    let dx = ax, dy = ay;
    if (Math.hypot(dx, dy) < 0.1) { dx = Math.cos(this.facing); dy = Math.sin(this.facing); }
    const l = Math.hypot(dx, dy) || 1;
    this.dashDirX = dx / l;
    this.dashDirY = dy / l;
    this.dashTimer = PLAYER.dashTime;
    this.dashCooldown = PLAYER.dashCooldown;
    this.facing = Math.atan2(this.dashDirY, this.dashDirX);
    world.fx.ring(this.x, this.y, { r0: 0.3, r1: 1.9, life: 0.24, color: this.color, width: 0.14 });
    world.sfx.dash();
  }

  _updateAttack(dt, c, world) {
    // Phase machine first so timings stay frame-exact.
    if (this.phase !== 'idle') {
      this.phaseTimer -= dt;
      if (this.phaseTimer <= 0) {
        if (this.phase === 'windup') {
          this.phase = 'active';
          this.phaseTimer = COMBAT.punchActive;
          this.hitSet.clear();
          // Lunge.
          const p = 1 + this.punchPower * 1.6;
          this.vx += Math.cos(this.facing) * 5.5 * p;
          this.vy += Math.sin(this.facing) * 5.5 * p;
          world.registerPunch(this);
        } else if (this.phase === 'active') {
          this.phase = 'recover';
          this.phaseTimer = COMBAT.punchRecover * (1 + this.punchPower * 0.5);
          if (this.hitSet.size === 0) world.sfx.whiff();
        } else {
          this.phase = 'idle';
        }
      }
      return;
    }

    if (!c) return;

    if (c.attack.down && this.cooldown <= 0 && !this.shop) {
      this.charging = true;
      this.chargeTime += dt;
      // Only humans drive the charge whine — three bots would fight over it.
      if (this.isHuman && this.chargeRatio > 0) world.sfx.charge(this.chargeRatio);
      if (this.chargeRatio >= 1 && rng.chance(dt * 22)) {
        const a = rng.angle();
        world.fx.burst(this.x + Math.cos(a) * 1.2, this.y + Math.sin(a) * 1.2, {
          count: 1, color: '#fff3c4', speed: [1, 3], life: [0.2, 0.4], size: [0.08, 0.16],
          dir: a + Math.PI, spread: 1.2,
        });
      }
    }

    if (c.attack.released && this.cooldown <= 0 && !this.shop) {
      const held = c.attack.releaseHold;
      this.charging = false;
      this.chargeTime = 0;
      if (this.isHuman) world.sfx.chargeStop();
      const ratio = clamp((held - COMBAT.chargeMinHold) / (COMBAT.chargeFull - COMBAT.chargeMinHold), 0, 1);
      this.punchPower = held < COMBAT.chargeMinHold ? 0 : Math.max(0.001, ratio);
      this.phase = 'windup';
      this.phaseTimer = COMBAT.punchWindup * (1 + this.punchPower * 1.7);
      this.cooldown = (this.punchPower > 0 ? COMBAT.chargeCooldown : COMBAT.punchCooldown);
    }

    if (!c.attack.down) {
      this.charging = false;
      this.chargeTime = 0;
    }
  }

  _updateShop(dt, c, world) {
    if (!c) return;
    if (c.action.pressed && this.cycleLock <= 0) {
      this.shopIndex = (this.shopIndex + 1) % ITEMS.length;
      this.cycleLock = SHOP.cycleCooldown;
      this.buyHold = 0;
      world.sfx.pop();
    }
    if (c.attack.down) {
      this.buyHold += dt;
      if (this.buyHold >= SHOP.buyHold) {
        this.buyHold = 0;
        world.tryBuy(this, ITEMS[this.shopIndex].id);
      }
    } else {
      this.buyHold = 0;
    }
  }

  /** Punch hitbox origin & angle for the current active frame. */
  punchOrigin() {
    const r = this.radius + this.reach * (1 + this.punchPower * (COMBAT.chargeReachBonus / COMBAT.punchReach));
    return {
      x: this.x + Math.cos(this.facing) * r * 0.55,
      y: this.y + Math.sin(this.facing) * r * 0.55,
      range: r,
    };
  }

  takeKnockback(dx, dy, force) {
    const resist = 1 / (1 + this.upgrades.defenseUp * 0.18 + Math.max(0, this.scaleLevel) * 0.15);
    this.vx += dx * force * resist;
    this.vy += dy * force * resist;
  }
}

/* -------------------------------------------------------------- CoinPopper */

export class CoinPopper {
  constructor(kind, x, y, label) {
    const def = POPPERS[kind];
    this.kind = kind;
    this.def = def;
    this.x = x;
    this.y = y;
    this.label = label;
    this.gold = def.start;
    this.rate = def.ratePerMin / 60;
    this.cap = def.cap;
    this.radius = def.radius;
    this.reach = def.reach;

    this.shake = 0;             // current shake energy 0..1
    this.shakePhase = rng.angle();
    this.spinPhase = rng.angle();
    this.popTimer = 0;
    this.accum = 0;
    this.harvestedThisFrame = 0;
    this.bob = rng.angle();
  }

  get shakeX() { return Math.sin(this.shakePhase * 3.1) * this.shake * 0.42; }
  get shakeY() { return Math.cos(this.shakePhase * 2.3) * this.shake * 0.42; }

  addShake(v) { this.shake = clamp(this.shake + v, 0, 1.4); }

  update(dt, world, rateMul = 1) {
    this.shakePhase += dt * (22 + this.shake * 40);
    this.spinPhase += dt * 1.4;
    this.bob += dt * 2.1;
    this.shake = Math.max(0, this.shake - dt * (2.4 + this.shake * 1.8));

    // Generation ticks: pop a coin every time a whole gold piece accumulates
    // in batches, which gives the machine a rhythmic shudder.
    const before = this.gold;
    this.accum += this.rate * rateMul * dt;
    const whole = Math.floor(this.accum);
    if (whole > 0) {
      this.accum -= whole;
      this.gold = Math.min(this.cap, this.gold + whole);
    }
    this.popTimer -= dt;
    if (this.gold > before && this.popTimer <= 0 && this.gold < this.cap) {
      this.popTimer = this.kind === 'big' ? 0.34 : 0.6;
      this.addShake(0.2);
      world.fx.burst(this.x, this.y - this.radius * 0.4, {
        count: 2, color: COLORS.gold, speed: [2, 5.5], life: [0.3, 0.55],
        size: [0.11, 0.2], dir: -Math.PI / 2, spread: 2.0, gravity: 12, drag: 1.4, shape: 'coin',
      });
      world.sfx.pop();
    }

    this.harvestedThisFrame = 0;
  }

  /** Pull gold into a player's bag. Returns the amount transferred. */
  harvest(player, dt, world) {
    if (this.gold <= 0 || player.bagSpace <= 0) return 0;
    const want = this.def.harvestRate * dt;
    const take = Math.min(want, this.gold, player.bagSpace);
    if (take <= 0) return 0;
    this.gold -= take;
    player.bag += take;
    player.stats.mined += take;
    this.harvestedThisFrame += take;
    this.addShake(dt * 3.4);
    player.harvesting = 1;
    if (rng.chance(dt * 26)) {
      const a = Math.atan2(player.y - this.y, player.x - this.x) + rng.range(-0.5, 0.5);
      world.fx.burst(this.x + Math.cos(a) * this.radius, this.y + Math.sin(a) * this.radius, {
        count: 1, color: COLORS.gold, speed: [3, 6], life: [0.16, 0.3], size: [0.1, 0.18],
        dir: a, spread: 0.6, shape: 'coin', drag: 2,
      });
      world.sfx.coin(rng.range(-1, 2));
    }
    return take;
  }
}

/* ---------------------------------------------------------------- BaseCamp */

export class BaseCamp {
  constructor(ownerIndex, x, y) {
    this.owner = ownerIndex;
    this.x = x;
    this.y = y;
    this.radius = CAMP.radius;
    this.vault = 0;
    this.color = COLORS.players[ownerIndex];
    this.colorDark = COLORS.playersDark[ownerIndex];
    this.shake = 0;
    this.shakePhase = rng.angle();
    this.flash = 0;
    this.alarm = 0;
    this.pulse = rng.angle();
    this.depositGlow = 0;
  }

  get shakeX() { return Math.sin(this.shakePhase * 3.3) * this.shake * 0.4; }
  get shakeY() { return Math.cos(this.shakePhase * 2.1) * this.shake * 0.4; }

  update(dt) {
    this.shakePhase += dt * 26;
    this.pulse += dt * 2.4;
    this.shake = Math.max(0, this.shake - dt * 3.2);
    this.flash = Math.max(0, this.flash - dt * 3);
    this.alarm = Math.max(0, this.alarm - dt);
    this.depositGlow = Math.max(0, this.depositGlow - dt * 2.5);
  }
}

/* -------------------------------------------------------------------- Shop */

export class Shop {
  constructor(id, x, y) {
    this.id = id;
    this.x = x;
    this.y = y;
    this.radius = SHOP.radius;
    this.bob = rng.angle();
    this.flash = 0;
    this.customers = new Set();
  }

  update(dt) {
    this.bob += dt * 1.8;
    this.flash = Math.max(0, this.flash - dt * 2.6);
  }
}

/* -------------------------------------------------------------------- Rock */

export class Rock {
  constructor(x, y, r, seed) {
    this.x = x;
    this.y = y;
    this.radius = r;
    this.seed = seed;
    this.points = [];
    const n = 8;
    const rr = makeRng(seed);
    for (let i = 0; i < n; i++) {
      const a = (i / n) * TAU;
      const rad = r * rr.range(0.82, 1.12);
      this.points.push([Math.cos(a) * rad, Math.sin(a) * rad]);
    }
  }
}

/* -------------------------------------------------------------- GoldPickup */

export class GoldPickup {
  constructor(x, y, amount, vx = 0, vy = 0, ownerIndex = -1) {
    this.x = x;
    this.y = y;
    this.vx = vx;
    this.vy = vy;
    this.amount = amount;
    this.life = PICKUP.life;
    this.owner = ownerIndex;      // brief no-pickup window for the victim
    this.lock = PICKUP.autoPickupDelay;
    this.spin = rng.range(-8, 8);
    this.rot = rng.angle();
    this.bob = rng.angle();
    this.dead = false;
  }

  update(dt, players) {
    this.life -= dt;
    this.lock = Math.max(0, this.lock - dt);
    this.bob += dt * 5;
    this.rot += this.spin * dt;
    this.spin *= Math.exp(-2 * dt);
    if (this.life <= 0) { this.dead = true; return; }

    const drag = Math.exp(-PICKUP.drag * dt);
    this.vx *= drag;
    this.vy *= drag;

    // Magnet toward the closest eligible player.
    let best = null, bestD = PICKUP.magnetRange;
    for (const p of players) {
      if (!p.alive || p.bagSpace <= 0) continue;
      if (this.lock > 0 && p.index === this.owner) continue;
      const d = Math.hypot(p.x - this.x, p.y - this.y);
      if (d < bestD) { bestD = d; best = p; }
    }
    if (best) {
      const k = 1 - bestD / PICKUP.magnetRange;
      const dx = best.x - this.x, dy = best.y - this.y;
      const l = Math.hypot(dx, dy) || 1;
      this.vx += (dx / l) * PICKUP.magnetSpeed * k * dt * 6;
      this.vy += (dy / l) * PICKUP.magnetSpeed * k * dt * 6;
    }

    this.x += this.vx * dt;
    this.y += this.vy * dt;
  }

  get fading() { return this.life < 3; }
}

/** Explodes `total` gold into physical coin blobs on the floor. */
export function scatterGold(world, x, y, total, ownerIndex, dir = null) {
  let left = total;
  const guard = 40;
  let n = 0;
  while (left > 0.5 && n++ < guard) {
    const amt = Math.min(left, PICKUP.clumpSize * rng.range(0.7, 1.3));
    left -= amt;
    const a = dir === null ? rng.angle() : dir + rng.range(-1.1, 1.1);
    const s = rng.range(PICKUP.scatterSpeed[0], PICKUP.scatterSpeed[1]);
    world.pickups.push(new GoldPickup(
      x + Math.cos(a) * 0.3, y + Math.sin(a) * 0.3,
      Math.round(amt), Math.cos(a) * s, Math.sin(a) * s, ownerIndex,
    ));
  }
}
