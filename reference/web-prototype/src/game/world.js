/**
 * World: arena construction, the fixed simulation step, and every interaction
 * that involves more than one entity (punches, robbery, deposits, purchases).
 *
 * Layout (metres, +y is down / south):
 *
 *              small popper (0,-20)
 *   camp NW ....................... camp NE      camps sit 25m from centre
 *              [ pillar rocks ]
 *   shop W  ....  BIG POPPER  ....  shop E
 *              [ pillar rocks ]
 *   camp SW ....................... camp SE
 *              small popper (0, 20)
 *
 * The four camps sit on the diagonals so every player is exactly the same
 * distance from the big popper, from one small popper and from one shop.
 */

import {
  ARENA, MATCH, PLAYER, COMBAT, CAMP, SHOP, PICKUP, COLORS, ITEMS, DIFFICULTY, NPC_PROFILES,
} from '../config.js';
import {
  clamp, lerp, dist, angleDelta, sign, makeRng, circlePush,
} from '../core/math.js';
import { Fx } from '../core/fx.js';
import { sfx } from '../core/audio.js';
import { NavGrid } from './nav.js';
import {
  Player, CoinPopper, BaseCamp, Shop, Rock, scatterGold,
} from './entities.js';
import { NpcBrain } from './npc.js';
import { buy, canBuy, priceOf, isMaxed, funds } from './items.js';

const DIAG = Math.SQRT1_2;

export class World {
  /**
   * @param {object} setup { slots: [{type:'human'|'cpu', controller, profile}], difficulty, seed }
   */
  constructor(setup) {
    this.setup = setup;
    this.rng = makeRng(setup.seed ?? (Date.now() & 0xffff));
    this.fx = new Fx();
    this.sfx = sfx;
    this.diff = DIFFICULTY[setup.difficulty] || DIFFICULTY.normal;

    this.time = 0;
    this.state = 'countdown';       // countdown | playing | ended
    this.countdown = MATCH.countdown + 0.999;
    this.rushStarted = false;
    this.banner = null;
    this.results = null;
    this.events = [];               // ticker feed for the HUD

    this._buildArena();
    this._buildPlayers();

    this.announce('GET READY', COLORS.text, 1.2);
  }

  /* ------------------------------------------------------------- creation */

  _buildArena() {
    this.camps = [];
    // NW, NE, SW, SE — evenly spaced on the diagonals, 25m from the centre.
    const angles = [-135, -45, 135, 45].map((d) => (d * Math.PI) / 180);
    for (let i = 0; i < 4; i++) {
      const a = angles[i];
      this.camps.push(new BaseCamp(i, Math.cos(a) * ARENA.campRadius, Math.sin(a) * ARENA.campRadius));
    }
    this.campAngles = angles;

    this.poppers = [
      new CoinPopper('big', 0, 0, 'MOTHERLODE'),
      new CoinPopper('small', 0, -20, 'NORTH'),
      new CoinPopper('small', 0, 20, 'SOUTH'),
    ];
    this.bigPopper = this.poppers[0];

    this.shops = [new Shop('west', -20, 0), new Shop('east', 20, 0)];

    this.rocks = [];
    // Four pillars framing the centre chamber; entrances face the camps.
    for (let i = 0; i < 4; i++) {
      const a = (i * Math.PI) / 2;
      this.rocks.push(new Rock(Math.cos(a) * 6.6, Math.sin(a) * 6.6, 1.5, 100 + i));
    }
    // Gate pairs on each camp->centre lane, so the approach is a choke point.
    for (let i = 0; i < 4; i++) {
      const a = this.campAngles[i];
      const cx = Math.cos(a) * 13.5;
      const cy = Math.sin(a) * 13.5;
      const px = -Math.sin(a);
      const py = Math.cos(a);
      this.rocks.push(new Rock(cx + px * 4.4, cy + py * 4.4, 1.6, 200 + i * 2));
      this.rocks.push(new Rock(cx - px * 4.4, cy - py * 4.4, 1.6, 201 + i * 2));
    }
    // Outer cover behind the shops and small poppers.
    for (let i = 0; i < 4; i++) {
      const a = (i * Math.PI) / 2;
      this.rocks.push(new Rock(Math.cos(a) * 28, Math.sin(a) * 28, 2.3, 300 + i));
    }

    // Static blockers for both physics and navigation.
    this.blockers = [
      ...this.rocks.map((r) => ({ x: r.x, y: r.y, r: r.radius, kind: 'rock', ref: r })),
      ...this.poppers.map((p) => ({ x: p.x, y: p.y, r: p.radius, kind: 'popper', ref: p })),
      ...this.shops.map((s) => ({ x: s.x, y: s.y, r: s.radius, kind: 'shop', ref: s })),
    ];
    this.nav = new NavGrid({ half: ARENA.half, cornerCut: ARENA.cornerCut }, this.blockers);

    this.pickups = [];
  }

  _buildPlayers() {
    this.players = [];
    const slots = this.setup.slots;
    for (let i = 0; i < 4; i++) {
      const slot = slots[i];
      const camp = this.camps[i];
      const a = this.campAngles[i];
      const p = new Player(i, {
        name: slot.name,
        isHuman: slot.type === 'human',
        controller: slot.controller,
        home: camp,
        x: camp.x - Math.cos(a) * 1.6,
        y: camp.y - Math.sin(a) * 1.6,
        facing: a + Math.PI,
      });
      if (slot.type === 'cpu') {
        const profile = slot.profile || NPC_PROFILES[i % NPC_PROFILES.length];
        p.profile = profile;
        p.brain = new NpcBrain(p, profile, this.diff, this.nav, 1234 + i * 77);
      }
      this.players.push(p);
    }
  }

  /* -------------------------------------------------------------- helpers */

  get timeLeft() { return Math.max(0, MATCH.duration - this.time); }
  get rushing() { return this.state === 'playing' && this.timeLeft <= MATCH.rushAt; }

  get leaderIndex() {
    let best = 0;
    for (let i = 1; i < 4; i++) if (this.camps[i].vault > this.camps[best].vault) best = i;
    return best;
  }

  playersNear(x, y, r, exclude = null) {
    return this.players.filter((p) => p !== exclude && p.alive && dist(p.x, p.y, x, y) <= r);
  }

  announce(text, color = COLORS.text, life = 1.6) {
    this.banner = { text, color, life, maxLife: life };
  }

  logEvent(text, color) {
    this.events.push({ text, color, life: 3.4 });
    if (this.events.length > 5) this.events.shift();
  }

  /* --------------------------------------------------------------- update */

  /** @param {number} realDt seconds of wall-clock time since the last frame */
  update(realDt) {
    const dt = this.fx.simDt(realDt);
    this.fx.update(realDt);

    if (this.banner) {
      this.banner.life -= realDt;
      if (this.banner.life <= 0) this.banner = null;
    }
    for (let i = this.events.length - 1; i >= 0; i--) {
      this.events[i].life -= realDt;
      if (this.events[i].life <= 0) this.events.splice(i, 1);
    }

    if (this.state === 'countdown') {
      const prev = Math.ceil(this.countdown);
      this.countdown -= realDt;
      const now = Math.ceil(this.countdown);
      if (now !== prev) {
        if (now > 0) { this.announce(String(now), COLORS.text, 0.9); this.sfx.beep(false); }
      }
      if (this.countdown <= 0) {
        this.state = 'playing';
        this.announce('GO!', COLORS.gold, 1.0);
        this.sfx.beep(true);
      }
      // Poppers still tick during the countdown so the opening rush matters.
      for (const p of this.poppers) p.update(dt, this, 0);
      for (const c of this.camps) c.update(dt);
      return;
    }

    if (this.state === 'ended') {
      for (const p of this.poppers) p.update(dt, this, 0);
      for (const c of this.camps) c.update(dt);
      for (const g of this.pickups) g.update(dt, []);
      return;
    }

    this.time += dt;
    if (!this.rushStarted && this.rushing) {
      this.rushStarted = true;
      this.bigPopper.gold = Math.min(this.bigPopper.cap, this.bigPopper.gold + MATCH.rushBurst);
      this.bigPopper.addShake(1.2);
      this.fx.shake(0.6);
      this.announce('GOLD RUSH!', COLORS.gold, 2.0);
      this.logEvent('Poppers overflowing — final 25 seconds!', COLORS.gold);
      this.sfx.rush();
    }
    if (this.time >= MATCH.duration) { this._finish(); return; }

    const rateMul = this.rushing ? MATCH.rushPopperMultiplier : 1;
    for (const pop of this.poppers) pop.update(dt, this, rateMul);
    for (const camp of this.camps) camp.update(dt);
    for (const shop of this.shops) { shop.update(dt); shop.customers.clear(); }

    // --- brains write into their virtual controllers before the sim step ---
    for (const p of this.players) if (p.brain) p.brain.update(dt, this);

    for (const p of this.players) p.update(dt, this);

    this._resolveCollisions(dt);
    this._resolveInteractions(dt);
    this._resolvePunches(dt);
    this._updatePickups(dt);
  }

  _finish() {
    this.state = 'ended';
    this.time = MATCH.duration;
    this.sfx.chargeStop();
    this.sfx.fanfare();
    this.fx.shake(0.5);

    const rows = this.players.map((p, i) => {
      const vault = this.camps[i].vault;
      const bonus = vault * p.endBonusRate;
      return {
        index: i,
        name: p.name,
        isHuman: p.isHuman,
        profile: p.profile || null,
        color: p.color,
        vault,
        bonus,
        total: vault + bonus,
        carried: p.bag,
        stats: p.stats,
        upgrades: { ...p.upgrades },
        scaleLevel: p.scaleLevel,
      };
    });
    rows.sort((a, b) => b.total - a.total);
    rows.forEach((r, i) => { r.place = i + 1; });
    this.results = rows;
    this.announce(`${rows[0].name} WINS`, rows[0].color, 3);

    for (const c of this.camps) { c.shake = 0.7; c.flash = 1; }
    for (let i = 0; i < 40; i++) {
      const a = this.rng.angle();
      const r = this.rng.range(0, 12);
      this.fx.burst(Math.cos(a) * r, Math.sin(a) * r, {
        count: 2, color: COLORS.gold, speed: [3, 10], life: [0.6, 1.4],
        size: [0.12, 0.26], gravity: 9, drag: 1.1, shape: 'coin',
      });
    }
  }

  /* ---------------------------------------------------------- collisions */

  _resolveCollisions(dt) {
    // Players vs static blockers.
    for (const p of this.players) {
      for (const b of this.blockers) {
        const push = circlePush(p.x, p.y, p.radius, b.x, b.y, b.r);
        if (push) {
          p.x += push[0];
          p.y += push[1];
          const nl = Math.hypot(push[0], push[1]) || 1;
          const nx = push[0] / nl, ny = push[1] / nl;
          const vn = p.vx * nx + p.vy * ny;
          if (vn < 0) {
            p.vx -= vn * nx * (1 + ARENA.wallBounce);
            p.vy -= vn * ny * (1 + ARENA.wallBounce);
          }
          if (b.kind === 'popper' && p.dashTimer > 0) b.ref.addShake(0.25);
        }
      }
      this._clampToArena(p);
    }

    // Players vs players (soft, mass scales with size).
    for (let i = 0; i < this.players.length; i++) {
      for (let j = i + 1; j < this.players.length; j++) {
        const a = this.players[i], b = this.players[j];
        const push = circlePush(a.x, a.y, a.radius, b.x, b.y, b.radius);
        if (!push) continue;
        const ma = a.scale, mb = b.scale;
        const ta = mb / (ma + mb), tb = ma / (ma + mb);
        a.x += push[0] * ta; a.y += push[1] * ta;
        b.x -= push[0] * tb; b.y -= push[1] * tb;
      }
    }
    for (const p of this.players) this._clampToArena(p);
  }

  _clampToArena(p) {
    const lim = ARENA.half - p.radius;
    if (p.x < -lim) { p.x = -lim; p.vx = Math.abs(p.vx) * ARENA.wallBounce; }
    if (p.x > lim) { p.x = lim; p.vx = -Math.abs(p.vx) * ARENA.wallBounce; }
    if (p.y < -lim) { p.y = -lim; p.vy = Math.abs(p.vy) * ARENA.wallBounce; }
    if (p.y > lim) { p.y = lim; p.vy = -Math.abs(p.vy) * ARENA.wallBounce; }

    // Chamfered corners: |x| + |y| <= D.
    const D = ARENA.half * 2 - ARENA.cornerCut - p.radius * Math.SQRT2;
    const s = Math.abs(p.x) + Math.abs(p.y);
    if (s > D) {
      const over = (s - D) / 2;
      const sx = sign(p.x) || 1;
      const sy = sign(p.y) || 1;
      p.x -= sx * over;
      p.y -= sy * over;
      const vn = p.vx * sx * DIAG + p.vy * sy * DIAG;
      if (vn > 0) {
        p.vx -= vn * sx * DIAG * (1 + ARENA.wallBounce);
        p.vy -= vn * sy * DIAG * (1 + ARENA.wallBounce);
      }
    }
  }

  /* ------------------------------------------- harvest / bank / shop zones */

  _resolveInteractions(dt) {
    for (const p of this.players) {
      if (!p.alive) continue;

      // --- coin poppers ---
      for (const pop of this.poppers) {
        if (dist(p.x, p.y, pop.x, pop.y) <= pop.reach + p.radius) {
          pop.harvest(p, dt, this);
        }
      }

      // --- own base camp: deposit ---
      const home = p.home;
      if (p.bag > 0 && dist(p.x, p.y, home.x, home.y) <= home.radius + p.radius * 0.5) {
        const moved = Math.min(p.bag, p.depositRate * dt);
        p.bag -= moved;
        home.vault += moved;
        p.stats.banked += moved;
        p.depositing = 1;
        home.depositGlow = 1;
        home.shake = Math.min(0.5, home.shake + dt * 1.4);
        p._depositTick = (p._depositTick || 0) + moved;
        if (p._depositTick > 14) {
          p._depositTick = 0;
          if (p.isHuman) this.sfx.deposit();
          this.fx.burst(p.x, p.y, {
            count: 3, color: COLORS.gold, speed: [2, 5], life: [0.25, 0.5],
            size: [0.1, 0.2], shape: 'coin', drag: 3,
          });
          this.fx.ring(home.x, home.y, { r0: home.radius * 0.4, r1: home.radius, life: 0.3, color: home.color, width: 0.16 });
        }
        if (p.bag <= 0.01) {
          p.bag = 0;
          this.fx.ring(home.x, home.y, { r0: 0.5, r1: home.radius * 1.5, life: 0.4, color: COLORS.gold, width: 0.22 });
        }
      }

      // --- shops ---
      let inShop = null;
      for (const s of this.shops) {
        if (dist(p.x, p.y, s.x, s.y) <= SHOP.browseRange + p.radius) { inShop = s; break; }
      }
      if (inShop !== p.shop) {
        p.buyHold = 0;
        p.shop = inShop;
        if (inShop) { inShop.flash = 0.6; if (p.isHuman) this.sfx.pop(); }
      }
      if (inShop) inShop.customers.add(p.index);
    }
  }

  /* ------------------------------------------------------------- punching */

  /** Called by Player when a swing reaches its active frames. */
  registerPunch(p) {
    const o = p.punchOrigin();
    const power = p.punchPower;
    this.fx.ring(o.x, o.y, {
      r0: 0.2, r1: o.range * (0.8 + power * 0.6), life: 0.18 + power * 0.1,
      color: power > 0 ? '#fff0b0' : '#ffffff', width: 0.12 + power * 0.16,
    });
    this.fx.burst(o.x, o.y, {
      count: 4 + Math.round(power * 8), color: power > 0 ? '#ffe08a' : '#cfd8ff',
      speed: [3, 8 + power * 8], life: [0.12, 0.3], size: [0.06, 0.16],
      dir: p.facing, spread: COMBAT.punchArc, drag: 7,
    });
    this.sfx.punch(power);
  }

  _resolvePunches(dt) {
    for (const p of this.players) {
      if (p.phase !== 'active') continue;
      const o = p.punchOrigin();
      const power = p.punchPower;

      // --- players ---
      for (const v of this.players) {
        if (v === p || !v.alive || p.hitSet.has(v)) continue;
        if (v.invuln > 0) continue;
        const d = dist(p.x, p.y, v.x, v.y);
        if (d > p.radius + v.radius + o.range * 0.85) continue;
        const ang = Math.atan2(v.y - p.y, v.x - p.x);
        if (Math.abs(angleDelta(p.facing, ang)) > COMBAT.punchArc * 0.5 + 0.25) continue;
        p.hitSet.add(v);
        this._landHit(p, v, power);
      }

      // --- enemy base camps (requires the Steal upgrade) ---
      if (p.canSteal) {
        for (const camp of this.camps) {
          if (camp.owner === p.index || p.hitSet.has(camp)) continue;
          const d = dist(p.x, p.y, camp.x, camp.y);
          if (d > p.radius + camp.radius + o.range * 0.8) continue;
          p.hitSet.add(camp);
          this._raidCamp(p, camp, power);
        }
      }

      // --- coin poppers: a punch shakes coins loose ---
      for (const pop of this.poppers) {
        if (p.hitSet.has(pop)) continue;
        const d = dist(p.x, p.y, pop.x, pop.y);
        if (d > p.radius + pop.radius + o.range * 0.8) continue;
        p.hitSet.add(pop);
        this._punchPopper(p, pop, power);
      }
    }
  }

  _landHit(attacker, victim, power) {
    const isCharged = power > 0;
    // How much of the victim's bag comes loose.
    let frac = isCharged
      ? lerp(COMBAT.chargedStealMin, COMBAT.chargedStealMax, power)
      : COMBAT.lightSteal;
    frac *= attacker.attackMul * victim.defenseMul;
    frac = clamp(frac, 0.05, 0.95);

    let amount = victim.bag * frac;
    if (victim.bag > 0) amount = Math.max(amount, Math.min(COMBAT.minSteal, victim.bag));
    amount = Math.min(amount, victim.bag);
    amount = Math.round(amount);

    victim.bag -= amount;
    victim.stats.lost += amount;
    victim.stats.punchesTaken++;
    attacker.stats.punchesLanded++;

    const taken = attacker.addGold(Math.round(amount * COMBAT.attackerShare));
    attacker.stats.robbed += taken;
    const dropped = amount - taken;

    const ang = Math.atan2(victim.y - attacker.y, victim.x - attacker.x);
    if (dropped > 0.5) scatterGold(this, victim.x, victim.y, dropped, victim.index, ang);

    const force = (isCharged
      ? lerp(COMBAT.knockbackChargedMin, COMBAT.knockbackChargedMax, power)
      : COMBAT.knockbackLight) * attacker.attackMul;
    victim.takeKnockback(Math.cos(ang), Math.sin(ang), force);
    victim.stun = PLAYER.stunTime + (isCharged ? COMBAT.stunChargedBonus * power : 0);
    victim.invuln = PLAYER.invulnAfterHit;
    victim.hitFlash = 1;
    victim.squash = 1.35;
    victim.charging = false;
    victim.chargeTime = 0;
    victim.phase = 'idle';
    victim.phaseTimer = 0;

    // --- juice ---
    const hs = isCharged
      ? lerp(COMBAT.hitstopChargedMin, COMBAT.hitstopChargedMax, power)
      : COMBAT.hitstopLight;
    this.fx.hitstop(hs);
    this.fx.shake(isCharged ? lerp(COMBAT.shakeLight, COMBAT.shakeCharged, power) : COMBAT.shakeLight);
    this.fx.kick(isCharged ? 0.5 + power * 0.5 : 0.22);
    if (isCharged && power > 0.6) this.fx.whiteFlash(0.18 + power * 0.22);

    const hx = (attacker.x + victim.x) / 2;
    const hy = (attacker.y + victim.y) / 2;
    this.fx.ring(hx, hy, {
      r0: 0.3, r1: 2.4 + power * 3.4, life: 0.26 + power * 0.14,
      color: '#ffffff', width: 0.2 + power * 0.3,
    });
    this.fx.burst(hx, hy, {
      count: 10 + Math.round(power * 18), color: '#fff6d0',
      speed: [5, 12 + power * 16], life: [0.16, 0.42], size: [0.07, 0.2],
      dir: ang, spread: 2.1, drag: 6,
    });
    if (amount > 0) {
      this.fx.burst(victim.x, victim.y, {
        count: Math.min(16, 3 + Math.round(amount / 5)), color: COLORS.gold,
        speed: [3, 9], life: [0.3, 0.7], size: [0.1, 0.2],
        dir: ang, spread: 2.6, gravity: 10, drag: 2, shape: 'coin',
      });
      this.fx.text(victim.x, victim.y - victim.radius - 0.6, `-${amount}`, {
        color: '#ff8b8b', size: 1 + Math.min(0.7, amount / 60), life: 0.9,
      });
      if (taken > 0) {
        this.fx.text(attacker.x, attacker.y - attacker.radius - 0.6, `+${taken}`, {
          color: COLORS.gold, size: 1 + Math.min(0.6, taken / 60), life: 0.9,
        });
      }
    }
    if (isCharged && power > 0.75) {
      this.logEvent(`${attacker.name} SMASHED ${victim.name} for ${amount}g`, attacker.color);
    }
  }

  _raidCamp(thief, camp, power) {
    const cool = thief.lastStolenFrom.get(camp.owner) || 0;
    if (cool > 0) {
      this.fx.text(camp.x, camp.y - camp.radius, `${cool.toFixed(1)}s`, { color: '#9aa6c4', size: 0.85, life: 0.6 });
      this.sfx.deny();
      return;
    }
    const owner = this.players[camp.owner];
    let amount = Math.min(
      Math.max(CAMP.stealMin, camp.vault * CAMP.stealFraction * (1 + power * 0.5) * owner.campArmor),
      CAMP.stealCap,
      camp.vault,
      thief.bagSpace,
    );
    amount = Math.round(amount);
    if (amount <= 0) {
      this.sfx.deny();
      this.fx.text(camp.x, camp.y - camp.radius, camp.vault < 1 ? 'EMPTY' : 'BAG FULL', {
        color: '#9aa6c4', size: 0.9, life: 0.8,
      });
      return;
    }

    camp.vault -= amount;
    thief.bag += amount;
    thief.stats.campRaids++;
    thief.stats.raidedFor += amount;
    owner.stats.lost += amount;
    thief.lastStolenFrom.set(camp.owner, CAMP.stealCooldown);

    camp.shake = 1;
    camp.flash = 1;
    camp.alarm = 1.6;
    this.fx.hitstop(0.07);
    this.fx.shake(0.45);
    this.fx.ring(camp.x, camp.y, { r0: 0.4, r1: camp.radius * 1.8, life: 0.45, color: '#ff5d5d', width: 0.26 });
    this.fx.burst(camp.x, camp.y, {
      count: 18, color: COLORS.gold, speed: [4, 11], life: [0.3, 0.7],
      size: [0.1, 0.22], gravity: 10, drag: 2, shape: 'coin',
    });
    this.fx.text(camp.x, camp.y - camp.radius - 0.4, `STOLEN ${amount}`, { color: '#ff9d5d', size: 1.25, life: 1.1 });
    this.sfx.steal();
    this.sfx.alarm();
    this.logEvent(`${thief.name} raided ${owner.name}'s vault for ${amount}g`, thief.color);
  }

  _punchPopper(p, pop, power) {
    if (pop.gold <= 0) {
      pop.addShake(0.5);
      return;
    }
    const amount = Math.round(Math.min(pop.gold, 5 + power * 11));
    pop.gold -= amount;
    pop.addShake(1.1 + power * 0.3);
    const ang = Math.atan2(pop.y - p.y, pop.x - p.x);
    scatterGold(this, pop.x, pop.y, amount, -1, ang + Math.PI);
    this.fx.hitstop(0.03 + power * 0.03);
    this.fx.shake(0.18 + power * 0.2);
    this.fx.ring(pop.x, pop.y, { r0: pop.radius * 0.5, r1: pop.radius * 2, life: 0.3, color: COLORS.gold, width: 0.2 });
    this.sfx.coin(2);
  }

  /* -------------------------------------------------------------- pickups */

  _updatePickups(dt) {
    for (let i = this.pickups.length - 1; i >= 0; i--) {
      const g = this.pickups[i];
      g.update(dt, this.players);
      if (g.dead) { this.pickups.splice(i, 1); continue; }

      for (const p of this.players) {
        if (!p.alive || p.bagSpace <= 0) continue;
        if (g.lock > 0 && p.index === g.owner) continue;
        if (dist(p.x, p.y, g.x, g.y) > p.radius + PICKUP.radius + 0.3) continue;
        const got = p.addGold(g.amount);
        this.fx.text(p.x, p.y - p.radius - 0.5, `+${Math.round(got)}`, {
          color: COLORS.gold, size: 0.85, life: 0.6, vy: -3,
        });
        this.fx.burst(g.x, g.y, {
          count: 4, color: COLORS.gold, speed: [2, 5], life: [0.2, 0.4],
          size: [0.08, 0.16], shape: 'coin', drag: 4,
        });
        if (p.isHuman) this.sfx.coin(1);
        this.pickups.splice(i, 1);
        break;
      }
    }
    // Keep the floor from filling with thousands of blobs.
    if (this.pickups.length > 160) this.pickups.splice(0, this.pickups.length - 160);
  }

  /* ------------------------------------------------------------ purchases */

  tryBuy(player, itemId) {
    const shop = player.shop;
    if (!shop) return false;
    if (isMaxed(player, itemId)) {
      this.sfx.deny();
      this.fx.text(player.x, player.y - player.radius - 0.7, 'MAXED', { color: '#9aa6c4', size: 0.9, life: 0.7 });
      return false;
    }
    if (!canBuy(player, itemId)) {
      this.sfx.deny();
      const need = priceOf(player, itemId) - Math.floor(funds(player));
      this.fx.text(player.x, player.y - player.radius - 0.7, `NEED ${Math.ceil(need)}g`, {
        color: '#ff8b8b', size: 0.9, life: 0.8,
      });
      return false;
    }
    const paid = buy(player, itemId);
    const def = ITEMS.find((i) => i.id === itemId);
    shop.flash = 1;
    this.sfx.buy();
    this.fx.hitstop(0.04);
    this.fx.ring(player.x, player.y, { r0: 0.3, r1: 2.6, life: 0.4, color: player.color, width: 0.2 });
    this.fx.burst(player.x, player.y, {
      count: 16, color: player.color, speed: [3, 9], life: [0.3, 0.6],
      size: [0.08, 0.2], drag: 4, glow: true,
    });
    this.fx.text(player.x, player.y - player.radius - 0.8, def.name.toUpperCase(), {
      color: player.color, size: 1.1, life: 1.0,
    });
    this.fx.text(player.x, player.y - player.radius - 1.7, `-${paid}g`, {
      color: '#ffd98a', size: 0.85, life: 0.9,
    });
    if (itemId === 'steal') {
      this.logEvent(`${player.name} bought STEAL — vaults are no longer safe`, player.color);
      this.announce(`${player.name} CAN RAID VAULTS`, player.color, 1.6);
      for (const c of this.camps) if (c.owner !== player.index) c.alarm = 1.2;
    }
    return true;
  }

  /* ---------------------------------------------------------------- debug */

  totalGoldInPlay() {
    let g = 0;
    for (const p of this.poppers) g += p.gold;
    for (const p of this.players) g += p.bag;
    for (const c of this.camps) g += c.vault;
    for (const k of this.pickups) g += k.amount;
    return g;
  }
}
