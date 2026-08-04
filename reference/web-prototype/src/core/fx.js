/**
 * The "juice" layer: particles, floating numbers, shockwave rings, screen
 * shake and hit-stop.
 *
 * Hit-stop works by freezing the simulation clock (`Fx.freeze`) while the
 * presentation layer keeps running on real time, so impacts read as a sharp
 * pause instead of a dropped frame.
 */

import { clamp, TAU, easeOutCubic, makeRng } from './math.js';

const rng = makeRng(0xc0ffee);

export class Fx {
  constructor() {
    this.particles = [];
    this.texts = [];
    this.rings = [];
    this.trauma = 0;         // 0..1, screen shake energy
    this.freeze = 0;         // remaining hit-stop in real seconds
    this.flash = 0;          // white flash intensity
    this.shakeX = 0;
    this.shakeY = 0;
    this.shakeRot = 0;
    this.time = 0;
    this.zoomKick = 0;
  }

  clear() {
    this.particles.length = 0;
    this.texts.length = 0;
    this.rings.length = 0;
    this.trauma = this.freeze = this.flash = this.zoomKick = 0;
  }

  hitstop(seconds) {
    this.freeze = Math.max(this.freeze, seconds);
  }

  shake(amount) {
    this.trauma = clamp(this.trauma + amount, 0, 1);
  }

  kick(amount) {
    this.zoomKick = Math.max(this.zoomKick, amount);
  }

  whiteFlash(a = 0.5) {
    this.flash = Math.max(this.flash, a);
  }

  /**
   * Cone / radial particle burst.
   * `dir` + `spread` shape a cone; omit `dir` for a full circle.
   */
  burst(x, y, {
    count = 10, color = '#fff', speed = [4, 12], life = [0.25, 0.5],
    size = [0.08, 0.22], dir = null, spread = TAU, drag = 4.5, gravity = 0,
    shape = 'dot', glow = false, spin = 0,
  } = {}) {
    for (let i = 0; i < count; i++) {
      const a = dir === null ? rng.angle() : dir + rng.range(-spread / 2, spread / 2);
      const s = rng.range(speed[0], speed[1]);
      this.particles.push({
        x, y, vx: Math.cos(a) * s, vy: Math.sin(a) * s,
        life: rng.range(life[0], life[1]), maxLife: 0, r: rng.range(size[0], size[1]),
        color, drag, gravity, shape, glow,
        rot: rng.angle(), spin: spin || rng.range(-9, 9),
      });
      const p = this.particles[this.particles.length - 1];
      p.maxLife = p.life;
    }
  }

  /** Expanding ring — used for punch impacts, deposits and popper pops. */
  ring(x, y, { r0 = 0.4, r1 = 3, life = 0.32, color = '#fff', width = 0.28, fade = true } = {}) {
    this.rings.push({ x, y, r0, r1, life, maxLife: life, color, width, fade });
  }

  /** Floating combat text. */
  text(x, y, str, { color = '#fff', size = 1.0, life = 0.9, vy = -2.4, outline = '#0b0d14', bold = true } = {}) {
    this.texts.push({ x, y, str, color, size, life, maxLife: life, vy, outline, bold });
  }

  /** Advance presentation-only state. Always fed *real* dt. */
  update(dt) {
    this.time += dt;

    for (let i = this.particles.length - 1; i >= 0; i--) {
      const p = this.particles[i];
      p.life -= dt;
      if (p.life <= 0) { this.particles.splice(i, 1); continue; }
      const d = Math.exp(-p.drag * dt);
      p.vx *= d;
      p.vy *= d;
      p.vy += p.gravity * dt;
      p.x += p.vx * dt;
      p.y += p.vy * dt;
      p.rot += p.spin * dt;
    }

    for (let i = this.texts.length - 1; i >= 0; i--) {
      const t = this.texts[i];
      t.life -= dt;
      if (t.life <= 0) { this.texts.splice(i, 1); continue; }
      t.y += t.vy * dt;
      t.vy *= Math.exp(-2.6 * dt);
    }

    for (let i = this.rings.length - 1; i >= 0; i--) {
      const r = this.rings[i];
      r.life -= dt;
      if (r.life <= 0) this.rings.splice(i, 1);
    }

    // Shake: trauma decays quadratically, offsets use layered sine noise.
    this.trauma = Math.max(0, this.trauma - dt * 1.9);
    const t2 = this.trauma * this.trauma;
    const s = this.time * 47;
    this.shakeX = t2 * 1.35 * (Math.sin(s) + 0.6 * Math.sin(s * 2.3 + 1.7));
    this.shakeY = t2 * 1.35 * (Math.cos(s * 1.13) + 0.6 * Math.sin(s * 2.9));
    this.shakeRot = t2 * 0.035 * Math.sin(s * 0.9);

    this.flash = Math.max(0, this.flash - dt * 3.4);
    this.zoomKick = Math.max(0, this.zoomKick - dt * 2.2);
  }

  /** Returns the simulation dt after applying hit-stop, and consumes it. */
  simDt(realDt) {
    if (this.freeze > 0) {
      this.freeze -= realDt;
      return realDt * 0.035; // a sliver of motion keeps it from looking hung
    }
    return realDt;
  }
}

/** Shared helper for drawing a fading ring. */
export function ringAlpha(r) {
  return r.fade ? 1 - easeOutCubic(1 - r.life / r.maxLife) : 1;
}
