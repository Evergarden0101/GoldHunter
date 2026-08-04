/**
 * Canvas renderer. Everything is drawn as vectors — no image assets — so the
 * whole game ships as one file.
 *
 * The camera fits the whole octagonal arena on screen and applies the shake /
 * zoom-kick coming out of the Fx layer.
 */

import { ARENA, COLORS, SHOP, MATCH } from '../config.js';
import { clamp, lerp, TAU, easeOutCubic } from '../core/math.js';
import { ringAlpha } from '../core/fx.js';
import { shopRows } from './items.js';

export class Renderer {
  constructor(canvas) {
    this.canvas = canvas;
    this.ctx = canvas.getContext('2d');
    this.dpr = 1;
    this.w = 0;
    this.h = 0;
    this.scale = 10;
    this.resize();
  }

  resize() {
    const dpr = Math.min(window.devicePixelRatio || 1, 2);
    const w = this.canvas.clientWidth || window.innerWidth;
    const h = this.canvas.clientHeight || window.innerHeight;
    this.canvas.width = Math.floor(w * dpr);
    this.canvas.height = Math.floor(h * dpr);
    this.dpr = dpr;
    this.w = w;
    this.h = h;
  }

  /** metres -> pixels for the current viewport. */
  fit(margin = 1.06) {
    const span = ARENA.half * 2 * margin;
    return Math.min(this.w / span, (this.h - 96) / span);
  }

  draw(world, ui) {
    const ctx = this.ctx;
    const fx = world.fx;
    ctx.setTransform(this.dpr, 0, 0, this.dpr, 0, 0);
    ctx.clearRect(0, 0, this.w, this.h);

    // Background
    const bg = ctx.createLinearGradient(0, 0, 0, this.h);
    bg.addColorStop(0, '#0d101a');
    bg.addColorStop(1, '#080a11');
    ctx.fillStyle = bg;
    ctx.fillRect(0, 0, this.w, this.h);

    const zoom = 1 + fx.zoomKick * 0.02;
    const s = this.fit() * zoom;
    this.scale = s;
    const cx = this.w / 2 + fx.shakeX * s * 0.16;
    const cy = this.h / 2 + 26 + fx.shakeY * s * 0.16;

    ctx.save();
    ctx.translate(cx, cy);
    ctx.rotate(fx.shakeRot);
    ctx.scale(s, s);

    this._drawFloor(ctx, world);
    this._drawCamps(ctx, world);
    this._drawShops(ctx, world);
    this._drawRocks(ctx, world);
    this._drawPoppers(ctx, world);
    this._drawPickups(ctx, world);
    this._drawRings(ctx, fx, false);
    this._drawPlayers(ctx, world, ui);
    this._drawParticles(ctx, fx);
    this._drawRings(ctx, fx, true);
    this._drawTexts(ctx, fx);

    ctx.restore();

    if (fx.flash > 0.001) {
      ctx.fillStyle = `rgba(255,255,255,${fx.flash * 0.75})`;
      ctx.fillRect(0, 0, this.w, this.h);
    }
    if (world.rushing && world.state === 'playing') {
      const pulse = 0.06 + Math.abs(Math.sin(fx.time * 4)) * 0.07;
      ctx.fillStyle = `rgba(255,120,40,${pulse})`;
      ctx.fillRect(0, 0, this.w, this.h);
    }
  }

  /* ------------------------------------------------------------ scenery */

  _arenaPath(ctx, inset = 0) {
    const h = ARENA.half - inset;
    const c = ARENA.cornerCut;
    ctx.beginPath();
    ctx.moveTo(-h + c, -h);
    ctx.lineTo(h - c, -h);
    ctx.lineTo(h, -h + c);
    ctx.lineTo(h, h - c);
    ctx.lineTo(h - c, h);
    ctx.lineTo(-h + c, h);
    ctx.lineTo(-h, h - c);
    ctx.lineTo(-h, -h + c);
    ctx.closePath();
  }

  _drawFloor(ctx, world) {
    ctx.save();
    this._arenaPath(ctx, 0);
    ctx.fillStyle = COLORS.floor;
    ctx.fill();
    ctx.clip();

    // Radial vignette toward the motherlode.
    const g = ctx.createRadialGradient(0, 0, 2, 0, 0, ARENA.half * 1.1);
    g.addColorStop(0, 'rgba(60,52,26,0.55)');
    g.addColorStop(0.35, 'rgba(30,34,50,0.25)');
    g.addColorStop(1, 'rgba(8,10,16,0.65)');
    ctx.fillStyle = g;
    ctx.fillRect(-ARENA.half, -ARENA.half, ARENA.half * 2, ARENA.half * 2);

    // Grid
    ctx.strokeStyle = COLORS.grid;
    ctx.lineWidth = 0.06;
    ctx.globalAlpha = 0.55;
    ctx.beginPath();
    for (let i = -ARENA.half; i <= ARENA.half; i += 5) {
      ctx.moveTo(i, -ARENA.half); ctx.lineTo(i, ARENA.half);
      ctx.moveTo(-ARENA.half, i); ctx.lineTo(ARENA.half, i);
    }
    ctx.stroke();
    ctx.globalAlpha = 1;

    // 25m ring the camps sit on.
    ctx.strokeStyle = 'rgba(255,201,57,0.10)';
    ctx.lineWidth = 0.14;
    ctx.setLineDash([1.1, 1.1]);
    ctx.beginPath();
    ctx.arc(0, 0, ARENA.campRadius, 0, TAU);
    ctx.stroke();
    ctx.setLineDash([]);
    ctx.restore();

    // Wall
    this._arenaPath(ctx, 0);
    ctx.strokeStyle = COLORS.wall;
    ctx.lineWidth = 0.7;
    ctx.stroke();
    ctx.strokeStyle = 'rgba(120,140,190,0.25)';
    ctx.lineWidth = 0.14;
    ctx.stroke();
  }

  _drawRocks(ctx, world) {
    for (const r of world.rocks) {
      ctx.save();
      ctx.translate(r.x, r.y);
      ctx.beginPath();
      ctx.moveTo(r.points[0][0], r.points[0][1] + 0.25);
      for (const [px, py] of r.points) ctx.lineTo(px, py + 0.25);
      ctx.closePath();
      ctx.fillStyle = 'rgba(0,0,0,0.35)';
      ctx.fill();

      ctx.beginPath();
      ctx.moveTo(r.points[0][0], r.points[0][1]);
      for (const [px, py] of r.points) ctx.lineTo(px, py);
      ctx.closePath();
      const g = ctx.createLinearGradient(0, -r.radius, 0, r.radius);
      g.addColorStop(0, '#3a4158');
      g.addColorStop(1, '#222839');
      ctx.fillStyle = g;
      ctx.fill();
      ctx.strokeStyle = '#4a5470';
      ctx.lineWidth = 0.1;
      ctx.stroke();
      ctx.restore();
    }
  }

  _drawCamps(ctx, world) {
    for (const camp of world.camps) {
      const p = world.players[camp.owner];
      ctx.save();
      ctx.translate(camp.x + camp.shakeX, camp.y + camp.shakeY);

      // Ground disc
      ctx.beginPath();
      ctx.arc(0, 0, camp.radius, 0, TAU);
      ctx.fillStyle = 'rgba(0,0,0,0.28)';
      ctx.fill();
      const g = ctx.createRadialGradient(0, 0, 0.5, 0, 0, camp.radius);
      g.addColorStop(0, this._alpha(camp.color, 0.32 + camp.depositGlow * 0.3));
      g.addColorStop(1, this._alpha(camp.color, 0.06));
      ctx.fillStyle = g;
      ctx.fill();

      // Rim
      ctx.beginPath();
      ctx.arc(0, 0, camp.radius, 0, TAU);
      ctx.strokeStyle = this._alpha(camp.color, 0.85);
      ctx.lineWidth = 0.16 + camp.depositGlow * 0.12;
      ctx.stroke();

      // Alarm ring when raided / raidable
      if (camp.alarm > 0) {
        const a = (Math.sin(camp.pulse * 6) * 0.5 + 0.5) * camp.alarm;
        ctx.beginPath();
        ctx.arc(0, 0, camp.radius + 0.35, 0, TAU);
        ctx.strokeStyle = `rgba(255,80,80,${a * 0.9})`;
        ctx.lineWidth = 0.24;
        ctx.stroke();
      }

      // Vault: a stack of coins whose height tracks the hoard.
      const stack = clamp(camp.vault / 320, 0, 1);
      const layers = Math.max(1, Math.round(stack * 9));
      for (let i = 0; i < layers; i++) {
        const y = -i * 0.17;
        const w = lerp(1.55, 0.55, i / Math.max(1, layers - 1)) * (0.75 + stack * 0.45);
        ctx.beginPath();
        ctx.ellipse(0, y, w, w * 0.42, 0, 0, TAU);
        ctx.fillStyle = i === layers - 1 ? COLORS.gold : (i % 2 ? '#e0ac25' : COLORS.gold);
        ctx.fill();
        ctx.strokeStyle = COLORS.goldDark;
        ctx.lineWidth = 0.045;
        ctx.stroke();
      }
      if (camp.flash > 0) {
        ctx.beginPath();
        ctx.arc(0, 0, camp.radius, 0, TAU);
        ctx.fillStyle = `rgba(255,255,255,${camp.flash * 0.35})`;
        ctx.fill();
      }

      // Banner: owner + vault
      ctx.save();
      ctx.scale(0.1, 0.1);
      this._label(ctx, `${p.name}`, 0, -camp.radius * 10 - 16, {
        size: 13, color: camp.color, weight: 800,
      });
      this._label(ctx, `${Math.floor(camp.vault)}g`, 0, -camp.radius * 10 - 2, {
        size: 17, color: COLORS.gold, weight: 900,
      });
      ctx.restore();

      // Flag pole
      ctx.beginPath();
      ctx.moveTo(camp.radius * 0.72, 0.2);
      ctx.lineTo(camp.radius * 0.72, -2.6);
      ctx.strokeStyle = '#8b93ad';
      ctx.lineWidth = 0.12;
      ctx.stroke();
      const wave = Math.sin(camp.pulse * 2) * 0.18;
      ctx.beginPath();
      ctx.moveTo(camp.radius * 0.72, -2.6);
      ctx.quadraticCurveTo(camp.radius * 0.72 + 0.7, -2.35 + wave, camp.radius * 0.72 + 1.4, -2.6 + wave);
      ctx.lineTo(camp.radius * 0.72 + 1.4, -1.7 + wave);
      ctx.quadraticCurveTo(camp.radius * 0.72 + 0.7, -1.45 + wave, camp.radius * 0.72, -1.7);
      ctx.closePath();
      ctx.fillStyle = camp.color;
      ctx.fill();
      ctx.restore();
    }
  }

  _drawShops(ctx, world) {
    for (const shop of world.shops) {
      ctx.save();
      ctx.translate(shop.x, shop.y);
      const bob = Math.sin(shop.bob) * 0.12;

      ctx.beginPath();
      ctx.arc(0, 0, SHOP.browseRange, 0, TAU);
      ctx.fillStyle = 'rgba(90,200,255,0.05)';
      ctx.fill();
      ctx.strokeStyle = `rgba(120,215,255,${0.22 + shop.flash * 0.5})`;
      ctx.lineWidth = 0.1;
      ctx.setLineDash([0.6, 0.5]);
      ctx.stroke();
      ctx.setLineDash([]);

      ctx.beginPath();
      ctx.ellipse(0, 0.4, shop.radius * 0.95, shop.radius * 0.4, 0, 0, TAU);
      ctx.fillStyle = 'rgba(0,0,0,0.35)';
      ctx.fill();

      ctx.translate(0, bob);
      // Counter
      ctx.beginPath();
      ctx.roundRect(-shop.radius * 0.85, -0.7, shop.radius * 1.7, 1.5, 0.25);
      ctx.fillStyle = '#3b2f52';
      ctx.fill();
      ctx.strokeStyle = '#6f5fa0';
      ctx.lineWidth = 0.1;
      ctx.stroke();
      // Awning
      const stripes = 6;
      for (let i = 0; i < stripes; i++) {
        ctx.beginPath();
        const x0 = -shop.radius + (i * 2 * shop.radius) / stripes;
        ctx.rect(x0, -2.2, (2 * shop.radius) / stripes, 1.1);
        ctx.fillStyle = i % 2 ? '#ff6f91' : '#ffe6ee';
        ctx.fill();
      }
      ctx.beginPath();
      ctx.moveTo(-shop.radius, -2.2);
      ctx.lineTo(shop.radius, -2.2);
      ctx.lineTo(shop.radius * 0.78, -3.0);
      ctx.lineTo(-shop.radius * 0.78, -3.0);
      ctx.closePath();
      ctx.fillStyle = '#54406f';
      ctx.fill();

      ctx.save();
      ctx.scale(0.1, 0.1);
      this._label(ctx, 'SHOP', 0, -32, { size: 13, color: '#ffe6ee', weight: 900 });
      ctx.restore();
      ctx.restore();
    }
  }

  _drawPoppers(ctx, world) {
    for (const pop of world.poppers) {
      const big = pop.kind === 'big';
      ctx.save();
      ctx.translate(pop.x + pop.shakeX, pop.y + pop.shakeY);

      // Harvest ring
      ctx.beginPath();
      ctx.arc(0, 0, pop.reach, 0, TAU);
      ctx.fillStyle = `rgba(255,201,57,${0.04 + pop.shake * 0.05})`;
      ctx.fill();
      ctx.strokeStyle = `rgba(255,201,57,${0.18 + pop.shake * 0.35})`;
      ctx.lineWidth = 0.09;
      ctx.setLineDash([0.5, 0.45]);
      ctx.stroke();
      ctx.setLineDash([]);

      // Shadow
      ctx.beginPath();
      ctx.ellipse(0, pop.radius * 0.55, pop.radius * 1.0, pop.radius * 0.38, 0, 0, TAU);
      ctx.fillStyle = 'rgba(0,0,0,0.4)';
      ctx.fill();

      const wob = 1 + Math.sin(pop.bob) * 0.02 + pop.shake * 0.06;
      ctx.rotate(Math.sin(pop.shakePhase * 1.7) * pop.shake * 0.07);
      ctx.scale(wob, 2 - wob);

      // Body
      const r = pop.radius;
      ctx.beginPath();
      ctx.roundRect(-r * 0.8, -r * 1.15, r * 1.6, r * 1.75, r * 0.3);
      const bodyG = ctx.createLinearGradient(0, -r, 0, r);
      bodyG.addColorStop(0, big ? '#4b3f6e' : '#3d3559');
      bodyG.addColorStop(1, '#241f36');
      ctx.fillStyle = bodyG;
      ctx.fill();
      ctx.strokeStyle = big ? '#8a76c9' : '#6b5da3';
      ctx.lineWidth = 0.12;
      ctx.stroke();

      // Glass dome full of coins
      ctx.beginPath();
      ctx.arc(0, -r * 0.95, r * 0.72, Math.PI, TAU);
      ctx.fillStyle = 'rgba(180,220,255,0.16)';
      ctx.fill();
      ctx.strokeStyle = 'rgba(190,225,255,0.4)';
      ctx.lineWidth = 0.08;
      ctx.stroke();

      const fill = clamp(pop.gold / pop.cap, 0, 1);
      const coinRows = Math.max(1, Math.round(fill * 5));
      for (let i = 0; i < coinRows; i++) {
        const y = -r * 0.6 - i * r * 0.18;
        const w = r * (0.62 - i * 0.07);
        ctx.beginPath();
        ctx.ellipse(Math.sin(pop.shakePhase + i) * pop.shake * 0.12, y, w, w * 0.4, 0, 0, TAU);
        ctx.fillStyle = i % 2 ? '#e0ac25' : COLORS.gold;
        ctx.fill();
      }

      // Spout
      ctx.beginPath();
      ctx.roundRect(-r * 0.34, r * 0.1, r * 0.68, r * 0.42, 0.08);
      ctx.fillStyle = '#141826';
      ctx.fill();

      // Rotating rate dial
      ctx.save();
      ctx.translate(0, -r * 0.05);
      ctx.rotate(pop.spinPhase);
      ctx.beginPath();
      for (let i = 0; i < 4; i++) {
        const a = (i / 4) * TAU;
        ctx.moveTo(0, 0);
        ctx.lineTo(Math.cos(a) * r * 0.3, Math.sin(a) * r * 0.3);
      }
      ctx.strokeStyle = `rgba(255,201,57,${0.35 + pop.shake * 0.5})`;
      ctx.lineWidth = 0.09;
      ctx.stroke();
      ctx.restore();
      ctx.restore();

      // Labels (unscaled by the wobble)
      ctx.save();
      ctx.translate(pop.x + pop.shakeX, pop.y + pop.shakeY);
      ctx.scale(0.1, 0.1);
      this._label(ctx, `${Math.floor(pop.gold)}g`, 0, (pop.radius + 1.5) * 10, {
        size: big ? 19 : 15, color: COLORS.gold, weight: 900,
      });
      this._label(ctx, `+${Math.round(pop.rate * 60 * (world.rushing ? MATCH.rushPopperMultiplier : 1))}/min`,
        0, (pop.radius + 2.6) * 10, { size: 10, color: 'rgba(255,225,150,0.7)', weight: 700 });
      ctx.restore();
    }
  }

  _drawPickups(ctx, world) {
    for (const g of world.pickups) {
      const a = g.fading ? 0.35 + Math.abs(Math.sin(g.bob * 3)) * 0.65 : 1;
      const bob = Math.sin(g.bob) * 0.08;
      ctx.save();
      ctx.translate(g.x, g.y + bob);
      ctx.globalAlpha = a;
      ctx.beginPath();
      ctx.ellipse(0, 0.28, 0.34, 0.13, 0, 0, TAU);
      ctx.fillStyle = 'rgba(0,0,0,0.35)';
      ctx.fill();
      const sq = Math.abs(Math.cos(g.rot));
      ctx.beginPath();
      ctx.ellipse(0, 0, 0.34 * (0.35 + sq * 0.65), 0.34, 0, 0, TAU);
      ctx.fillStyle = COLORS.gold;
      ctx.fill();
      ctx.strokeStyle = COLORS.goldDark;
      ctx.lineWidth = 0.06;
      ctx.stroke();
      ctx.restore();
    }
  }

  /* ------------------------------------------------------------- players */

  _drawPlayers(ctx, world, ui) {
    const order = [...world.players].sort((a, b) => a.y - b.y);
    for (const p of order) {
      if (!p.alive) continue;
      const r = p.radius;
      const sq = p.squash;

      ctx.save();
      ctx.translate(p.x, p.y);

      // Shadow
      ctx.beginPath();
      ctx.ellipse(0, r * 0.75, r * 0.95, r * 0.38, 0, 0, TAU);
      ctx.fillStyle = 'rgba(0,0,0,0.4)';
      ctx.fill();

      // Charge aura
      if (p.glow > 0.01) {
        const gr = r * (1.6 + p.glow * 1.5);
        const g = ctx.createRadialGradient(0, 0, r * 0.5, 0, 0, gr);
        g.addColorStop(0, `rgba(255,240,180,${0.35 * p.glow})`);
        g.addColorStop(1, 'rgba(255,220,120,0)');
        ctx.fillStyle = g;
        ctx.beginPath();
        ctx.arc(0, 0, gr, 0, TAU);
        ctx.fill();
        if (p.chargeRatio >= 1) {
          ctx.beginPath();
          ctx.arc(0, 0, r * 1.5 + Math.sin(world.fx.time * 22) * 0.08, 0, TAU);
          ctx.strokeStyle = 'rgba(255,255,255,0.85)';
          ctx.lineWidth = 0.09;
          ctx.stroke();
        }
      }

      // Punch arc telegraph
      if (p.phase === 'active') {
        const o = p.punchOrigin();
        ctx.save();
        ctx.rotate(p.facing);
        ctx.beginPath();
        ctx.moveTo(0, 0);
        ctx.arc(0, 0, r + o.range * 0.8, -0.55, 0.55);
        ctx.closePath();
        ctx.fillStyle = `rgba(255,255,255,${0.18 + p.punchPower * 0.25})`;
        ctx.fill();
        ctx.restore();
      }

      ctx.save();
      ctx.scale(1 / sq, sq);

      // Body
      ctx.beginPath();
      ctx.arc(0, 0, r, 0, TAU);
      const bg = ctx.createRadialGradient(-r * 0.35, -r * 0.4, r * 0.15, 0, 0, r);
      bg.addColorStop(0, this._lighten(p.color, 0.35));
      bg.addColorStop(1, p.color);
      ctx.fillStyle = bg;
      ctx.fill();
      ctx.lineWidth = 0.12;
      ctx.strokeStyle = p.colorDark;
      ctx.stroke();

      if (p.invuln > 0 && Math.floor(world.fx.time * 24) % 2 === 0) {
        ctx.beginPath();
        ctx.arc(0, 0, r, 0, TAU);
        ctx.fillStyle = 'rgba(255,255,255,0.28)';
        ctx.fill();
      }
      if (p.hitFlash > 0) {
        ctx.beginPath();
        ctx.arc(0, 0, r, 0, TAU);
        ctx.fillStyle = `rgba(255,255,255,${p.hitFlash * 0.85})`;
        ctx.fill();
      }
      ctx.restore();

      // Eyes look where you face; they cross when stunned.
      ctx.save();
      ctx.rotate(p.facing);
      const eyeX = r * 0.34;
      for (const sgn of [-1, 1]) {
        ctx.beginPath();
        ctx.ellipse(eyeX, sgn * r * 0.33, r * 0.2, r * 0.24, 0, 0, TAU);
        ctx.fillStyle = '#fff';
        ctx.fill();
        ctx.beginPath();
        const stunned = p.stun > 0;
        ctx.arc(eyeX + r * (stunned ? 0.04 : 0.09), sgn * r * 0.33 + (stunned ? sgn * r * 0.06 : 0), r * 0.1, 0, TAU);
        ctx.fillStyle = '#101422';
        ctx.fill();
      }

      // Fist
      const punching = p.phase === 'active' || p.phase === 'windup';
      const reachOut = p.phase === 'active' ? 1 : p.phase === 'windup' ? -0.35 : 0;
      const fx0 = r * (0.75 + reachOut * (0.9 + p.punchPower * 0.7));
      ctx.beginPath();
      ctx.arc(fx0, 0, r * (0.3 + p.punchPower * 0.12), 0, TAU);
      ctx.fillStyle = punching ? '#fff' : this._lighten(p.color, 0.18);
      ctx.fill();
      ctx.strokeStyle = p.colorDark;
      ctx.lineWidth = 0.08;
      ctx.stroke();
      ctx.restore();

      // Carried gold pouch
      if (p.bag > 0) {
        const t = p.bagFill;
        ctx.save();
        ctx.translate(-r * 0.15, -r * 1.25 - t * 0.25);
        ctx.beginPath();
        ctx.ellipse(0, 0, 0.34 + t * 0.3, 0.3 + t * 0.28, 0, 0, TAU);
        ctx.fillStyle = '#6b4a1f';
        ctx.fill();
        ctx.beginPath();
        ctx.ellipse(0, -0.06 - t * 0.05, 0.2 + t * 0.2, 0.13 + t * 0.12, 0, 0, TAU);
        ctx.fillStyle = COLORS.gold;
        ctx.fill();
        ctx.restore();
      }

      // Thief marker
      if (p.canSteal) {
        ctx.save();
        ctx.translate(r * 0.95, -r * 1.15);
        ctx.rotate(Math.sin(world.fx.time * 3 + p.index) * 0.2);
        ctx.beginPath();
        ctx.moveTo(0, -0.28);
        ctx.lineTo(0.26, 0.22);
        ctx.lineTo(-0.26, 0.22);
        ctx.closePath();
        ctx.fillStyle = '#ff9d5d';
        ctx.fill();
        ctx.restore();
      }

      ctx.restore();

      // Name + bag bar, drawn in screen-ish units
      ctx.save();
      ctx.translate(p.x, p.y);
      ctx.scale(0.1, 0.1);
      const top = -(r + 1.15) * 10;
      this._label(ctx, p.name, 0, top, { size: 11, color: p.color, weight: 800 });
      const bw = 26, bh = 4.4;
      ctx.beginPath();
      ctx.roundRect(-bw / 2, top + 3, bw, bh, 2);
      ctx.fillStyle = 'rgba(0,0,0,0.55)';
      ctx.fill();
      ctx.beginPath();
      ctx.roundRect(-bw / 2, top + 3, bw * p.bagFill, bh, 2);
      ctx.fillStyle = p.bagFill > 0.95 ? '#fff0a8' : COLORS.gold;
      ctx.fill();
      if (p.bag > 0) {
        this._label(ctx, `${Math.floor(p.bag)}`, 0, top + 15, { size: 10, color: '#ffe9a8', weight: 800 });
      }
      // Dash cooldown pip
      if (p.dashCooldown > 0) {
        ctx.beginPath();
        ctx.arc(bw / 2 + 4, top + 5.2, 2.2, -Math.PI / 2, -Math.PI / 2 + TAU * (1 - p.dashCooldown / 2.4));
        ctx.strokeStyle = 'rgba(255,255,255,0.5)';
        ctx.lineWidth = 1.6;
        ctx.stroke();
      }
      ctx.restore();

      if (ui && ui.debugAI && p.brain) {
        ctx.save();
        ctx.translate(p.x, p.y);
        ctx.scale(0.1, 0.1);
        this._label(ctx, p.brain.debugLabel, 0, (r + 2.6) * 10, { size: 9, color: '#8fa', weight: 700 });
        ctx.restore();
        this._drawPath(ctx, p.brain.follow, p.color);
      }
    }

    // Shop panels float above everything else in world space.
    for (const p of world.players) if (p.shop) this._drawShopPanel(ctx, world, p);
  }

  _drawPath(ctx, follow, color) {
    if (!follow.path) return;
    ctx.save();
    ctx.globalAlpha = 0.55;
    ctx.beginPath();
    for (let i = follow.index; i < follow.path.length; i++) {
      const w = follow.path[i];
      if (i === follow.index) ctx.moveTo(w.x, w.y); else ctx.lineTo(w.x, w.y);
    }
    ctx.strokeStyle = color;
    ctx.lineWidth = 0.08;
    ctx.setLineDash([0.4, 0.3]);
    ctx.stroke();
    ctx.setLineDash([]);
    ctx.restore();
  }

  /* ---------------------------------------------------------- shop panel */

  _drawShopPanel(ctx, world, p) {
    const rows = shopRows(p);
    const W = 17.5, rowH = 1.55;
    const H = rows.length * rowH + 3.1;
    // Open outward, into the empty margin beside the arena, so the panel never
    // covers the centre of the fight.
    let x = p.shop.x < 0 ? p.shop.x - W - 4.6 : p.shop.x + 4.6;
    let y = p.shop.y - H / 2 + (p.index % 2 ? 0.6 : -0.6);
    // Nudge the panel so two customers at one shop don't overlap.
    if (p.shop.customers.size > 1) y += (p.index - 1.5) * 1.6;

    ctx.save();
    ctx.translate(x, y);
    ctx.beginPath();
    ctx.roundRect(0, 0, W, H, 0.5);
    ctx.fillStyle = 'rgba(12,15,24,0.92)';
    ctx.fill();
    ctx.strokeStyle = p.color;
    ctx.lineWidth = 0.12;
    ctx.stroke();

    ctx.save();
    ctx.scale(0.1, 0.1);
    const vault = p.home ? Math.floor(p.home.vault) : 0;
    this._label(ctx, `${p.name}  ·  ${Math.floor(p.bag)}g bag  +  ${vault}g vault`, W * 5, 16,
      { size: 12, color: COLORS.gold, weight: 800 });
    ctx.restore();

    rows.forEach((row, i) => {
      const ry = 2.2 + i * rowH;
      const sel = i === p.shopIndex;
      if (sel) {
        ctx.beginPath();
        ctx.roundRect(0.25, ry - 0.6, W - 0.5, rowH - 0.12, 0.3);
        ctx.fillStyle = this._alpha(p.color, 0.22);
        ctx.fill();
        ctx.strokeStyle = this._alpha(p.color, 0.8);
        ctx.lineWidth = 0.07;
        ctx.stroke();
        if (p.buyHold > 0) {
          ctx.beginPath();
          ctx.roundRect(0.25, ry - 0.6, (W - 0.5) * clamp(p.buyHold / 0.45, 0, 1), rowH - 0.12, 0.3);
          ctx.fillStyle = this._alpha(COLORS.gold, 0.45);
          ctx.fill();
        }
      }
      ctx.save();
      ctx.scale(0.1, 0.1);
      const color = row.maxed ? '#6d7691' : row.affordable ? '#eaf0ff' : '#98a1bd';
      this._label(ctx, row.def.name, 9, ry * 10 + 3.5, { size: 10.5, color, weight: 700, align: 'left' });
      const lvl = row.def.max > 1 ? `${row.level}/${row.def.max}` : (row.level ? 'OWNED' : '');
      this._label(ctx, lvl, W * 10 - 52, ry * 10 + 3.5, { size: 9, color: '#7f89a8', weight: 700, align: 'right' });
      // Amber price = the vault has to chip in, which costs you final score.
      const priceColor = row.maxed ? '#6d7691'
        : !row.affordable ? '#b5603f'
          : row.needsVault ? '#ffa552' : COLORS.gold;
      this._label(ctx, row.maxed ? 'MAX' : `${row.price}g`, W * 10 - 8, ry * 10 + 3.5, {
        size: 11, color: priceColor, weight: 800, align: 'right',
      });
      ctx.restore();
    });

    ctx.save();
    ctx.scale(0.1, 0.1);
    this._label(ctx, p.isHuman ? 'ACTION = next   ·   HOLD PUNCH = buy' : 'shopping…', W * 5, H * 10 - 5,
      { size: 8.5, color: '#8b95b4', weight: 700 });
    ctx.restore();
    ctx.restore();
  }

  /* ------------------------------------------------------------------ fx */

  _drawParticles(ctx, fx) {
    for (const p of fx.particles) {
      const t = p.life / p.maxLife;
      ctx.globalAlpha = clamp(t * 1.4, 0, 1);
      if (p.shape === 'coin') {
        ctx.save();
        ctx.translate(p.x, p.y);
        const sq = Math.abs(Math.cos(p.rot));
        ctx.beginPath();
        ctx.ellipse(0, 0, p.r * (0.3 + sq * 0.7), p.r, 0, 0, TAU);
        ctx.fillStyle = p.color;
        ctx.fill();
        ctx.restore();
      } else {
        ctx.beginPath();
        ctx.arc(p.x, p.y, p.r * (0.4 + t * 0.6), 0, TAU);
        ctx.fillStyle = p.color;
        ctx.fill();
      }
    }
    ctx.globalAlpha = 1;
  }

  _drawRings(ctx, fx, above) {
    for (const r of fx.rings) {
      const t = 1 - r.life / r.maxLife;
      const rad = lerp(r.r0, r.r1, easeOutCubic(t));
      ctx.globalAlpha = ringAlpha(r) * (above ? 0.9 : 0.6);
      ctx.beginPath();
      ctx.arc(r.x, r.y, rad, 0, TAU);
      ctx.strokeStyle = r.color;
      ctx.lineWidth = r.width * (1 - t * 0.6);
      ctx.stroke();
    }
    ctx.globalAlpha = 1;
  }

  _drawTexts(ctx, fx) {
    for (const t of fx.texts) {
      const k = t.life / t.maxLife;
      ctx.save();
      ctx.translate(t.x, t.y);
      ctx.scale(0.1, 0.1);
      ctx.globalAlpha = clamp(k * 1.6, 0, 1);
      this._label(ctx, t.str, 0, 0, {
        size: 13 * t.size * (1 + (1 - k) * 0.18), color: t.color, weight: 900, outline: t.outline,
      });
      ctx.restore();
    }
    ctx.globalAlpha = 1;
  }

  /* -------------------------------------------------------------- helpers */

  _label(ctx, text, x, y, { size = 12, color = '#fff', weight = 700, align = 'center', outline = '#0b0d14' } = {}) {
    ctx.font = `${weight} ${size}px "Trebuchet MS", "Segoe UI", system-ui, sans-serif`;
    ctx.textAlign = align;
    ctx.textBaseline = 'middle';
    if (outline) {
      ctx.lineWidth = Math.max(2, size * 0.28);
      ctx.strokeStyle = outline;
      ctx.lineJoin = 'round';
      ctx.strokeText(text, x, y);
    }
    ctx.fillStyle = color;
    ctx.fillText(text, x, y);
  }

  _alpha(hex, a) {
    const { r, g, b } = hexToRgb(hex);
    return `rgba(${r},${g},${b},${a})`;
  }

  _lighten(hex, amount) {
    const { r, g, b } = hexToRgb(hex);
    return `rgb(${Math.round(lerp(r, 255, amount))},${Math.round(lerp(g, 255, amount))},${Math.round(lerp(b, 255, amount))})`;
  }
}

export function hexToRgb(hex) {
  const h = hex.replace('#', '');
  const v = h.length === 3
    ? h.split('').map((c) => parseInt(c + c, 16))
    : [parseInt(h.slice(0, 2), 16), parseInt(h.slice(2, 4), 16), parseInt(h.slice(4, 6), 16)];
  return { r: v[0], g: v[1], b: v[2] };
}
