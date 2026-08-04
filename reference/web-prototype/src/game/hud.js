/**
 * In-match HUD, drawn on the same canvas as the game.
 *
 * Each player's card sits in the screen corner nearest their base camp
 * (NW/NE/SW/SE), so you never have to hunt for your own numbers.
 */

import { MATCH, COLORS, ITEMS } from '../config.js';
import { clamp, TAU } from '../core/math.js';

const CORNERS = ['nw', 'ne', 'sw', 'se'];

export class Hud {
  constructor() {
    this.pulse = 0;
  }

  draw(ctx, world, view, ui) {
    const { w, h } = view;
    this.pulse += 1 / 60;

    this._cards(ctx, world, w, h);
    this._timer(ctx, world, w, h);
    this._ticker(ctx, world, w, h);
    this._banner(ctx, world, w, h);
    if (ui && ui.showHelp) this._help(ctx, world, w, h);
  }

  /* ----------------------------------------------------------- top clock */

  _timer(ctx, world, w) {
    const left = world.state === 'countdown' ? MATCH.duration : world.timeLeft;
    const m = Math.floor(left / 60);
    const s = Math.floor(left % 60);
    const rush = world.rushing;
    const txt = `${m}:${String(s).padStart(2, '0')}`;

    const bw = 168, bh = 54;
    const x = w / 2 - bw / 2;
    ctx.save();
    ctx.beginPath();
    ctx.roundRect(x, 10, bw, bh, 12);
    ctx.fillStyle = 'rgba(10,13,21,0.82)';
    ctx.fill();
    ctx.strokeStyle = rush ? '#ff7a3c' : 'rgba(140,155,195,0.35)';
    ctx.lineWidth = rush ? 2.5 + Math.sin(this.pulse * 9) * 1.2 : 1.5;
    ctx.stroke();

    label(ctx, txt, w / 2, 34, {
      size: rush ? 34 : 31, weight: 900,
      color: rush ? '#ff9a5c' : left < 30 ? '#ffd23f' : '#eef2ff',
    });
    label(ctx, rush ? 'GOLD RUSH' : 'TIME LEFT', w / 2, 55, {
      size: 10, weight: 800, color: rush ? '#ffb37a' : 'rgba(180,192,220,0.75)',
    });

    // Match progress bar
    const p = clamp(1 - left / MATCH.duration, 0, 1);
    ctx.beginPath();
    ctx.roundRect(x + 10, 10 + bh - 6, bw - 20, 3, 2);
    ctx.fillStyle = 'rgba(255,255,255,0.12)';
    ctx.fill();
    ctx.beginPath();
    ctx.roundRect(x + 10, 10 + bh - 6, (bw - 20) * p, 3, 2);
    ctx.fillStyle = rush ? '#ff7a3c' : COLORS.gold;
    ctx.fill();
    ctx.restore();
  }

  /* -------------------------------------------------------- player cards */

  _cards(ctx, world, w, h) {
    const ranked = [...world.camps].sort((a, b) => b.vault - a.vault);
    const rankOf = (i) => ranked.findIndex((c) => c.owner === i) + 1;
    const cw = Math.min(236, Math.max(178, w * 0.19));
    const ch = 76;
    const pad = 12;

    world.players.forEach((p, i) => {
      const corner = CORNERS[i];
      const x = corner[1] === 'w' ? pad : w - cw - pad;
      const y = corner[0] === 'n' ? pad : h - ch - pad;
      const camp = world.camps[i];
      const rank = rankOf(i);

      ctx.save();
      ctx.beginPath();
      ctx.roundRect(x, y, cw, ch, 10);
      ctx.fillStyle = 'rgba(10,13,21,0.8)';
      ctx.fill();
      ctx.strokeStyle = rank === 1 ? COLORS.gold : hexA(p.color, 0.55);
      ctx.lineWidth = rank === 1 ? 2.2 : 1.4;
      ctx.stroke();

      // Colour chip + name
      ctx.beginPath();
      ctx.roundRect(x + 9, y + 9, 16, 16, 5);
      ctx.fillStyle = p.color;
      ctx.fill();
      label(ctx, p.name, x + 32, y + 18, { size: 13, weight: 800, color: '#eef2ff', align: 'left' });
      const kind = p.isHuman ? 'YOU' : (p.profile ? p.profile.tag.toUpperCase() : 'CPU');
      label(ctx, kind, x + cw - 10, y + 18, {
        size: 9.5, weight: 800, align: 'right', color: p.isHuman ? '#8ee6a4' : 'rgba(160,172,200,0.8)',
      });

      // Vault
      const vaultTxt = `${Math.floor(camp.vault)}`;
      label(ctx, vaultTxt, x + 32, y + 41, { size: 24, weight: 900, color: COLORS.gold, align: 'left' });
      label(ctx, 'g banked', x + 36 + textW(ctx, vaultTxt, 24, 900), y + 46, {
        size: 10, weight: 700, color: 'rgba(200,208,230,0.65)', align: 'left',
      });

      // Rank badge
      const rx = x + cw - 24;
      ctx.beginPath();
      ctx.arc(rx, y + 41, 13, 0, TAU);
      ctx.fillStyle = rank === 1 ? COLORS.gold : 'rgba(255,255,255,0.09)';
      ctx.fill();
      label(ctx, `${rank}`, rx, y + 42, {
        size: 14, weight: 900, color: rank === 1 ? '#241a05' : '#dbe2f5',
      });

      // Bag bar
      const by = y + 58;
      ctx.beginPath();
      ctx.roundRect(x + 10, by, cw - 20, 9, 4);
      ctx.fillStyle = 'rgba(255,255,255,0.1)';
      ctx.fill();
      ctx.beginPath();
      ctx.roundRect(x + 10, by, (cw - 20) * p.bagFill, 9, 4);
      ctx.fillStyle = p.bagFill > 0.95 ? '#fff2b0' : COLORS.gold;
      ctx.fill();
      label(ctx, `${Math.floor(p.bag)}/${p.bagCap}`, x + cw / 2, by + 5, {
        size: 9, weight: 800, color: p.bagFill > 0.5 ? '#3a2c05' : 'rgba(230,236,250,0.85)', outline: null,
      });

      // Upgrade pips
      let px = x + 10;
      const py = y + ch - 3;
      for (const def of ITEMS) {
        const lvl = def.id === 'scaleUp' ? Math.max(0, p.scaleLevel)
          : def.id === 'scaleDown' ? Math.max(0, -p.scaleLevel)
            : p.upgrades[def.id];
        if (!lvl) continue;
        ctx.beginPath();
        ctx.roundRect(px, py - 4, 4 + lvl * 5, 5, 2.5);
        ctx.fillStyle = PIP_COLORS[def.id] || '#fff';
        ctx.fill();
        px += 8 + lvl * 5;
      }
      if (p.canSteal) {
        label(ctx, 'THIEF', x + cw - 10, py - 1, { size: 8.5, weight: 900, color: '#ff9d5d', align: 'right' });
      }
      ctx.restore();
    });
  }

  /* -------------------------------------------------------------- ticker */

  _ticker(ctx, world, w, h) {
    if (!world.events.length) return;
    ctx.save();
    let y = h - 96;
    for (let i = world.events.length - 1; i >= 0; i--) {
      const e = world.events[i];
      ctx.globalAlpha = clamp(e.life / 1.2, 0, 1) * 0.95;
      label(ctx, e.text, w / 2, y, { size: 12.5, weight: 700, color: e.color || '#dfe6f8' });
      y -= 19;
    }
    ctx.restore();
  }

  /* -------------------------------------------------------------- banner */

  _banner(ctx, world, w, h) {
    const b = world.banner;
    if (!b) return;
    const t = 1 - b.life / b.maxLife;
    const pop = t < 0.25 ? 0.6 + (t / 0.25) * 0.5 : 1 - Math.max(0, (t - 0.7) / 0.3) * 0.2;
    ctx.save();
    ctx.globalAlpha = clamp(b.life * 2.2, 0, 1);
    ctx.translate(w / 2, h * 0.3);
    ctx.scale(pop, pop);
    label(ctx, b.text, 0, 0, { size: 58, weight: 900, color: b.color, outline: '#0b0d14' });
    ctx.restore();
  }

  /* ---------------------------------------------------------------- help */

  _help(ctx, world, w, h) {
    const humans = world.players.filter((p) => p.isHuman);
    const lines = humans.map((p) => `${p.name}: ${p.controller?.label || ''}`);
    lines.push('Tap punch = jab  ·  Hold punch = charged smash  ·  Bank gold at YOUR camp before time runs out');
    ctx.save();
    let y = h - 46;
    for (const l of lines) {
      label(ctx, l, w / 2, y, { size: 11.5, weight: 700, color: 'rgba(190,200,225,0.75)' });
      y += 16;
    }
    ctx.restore();
  }
}

const PIP_COLORS = {
  attackUp: '#ff7a6b',
  defenseUp: '#6fb7ff',
  goldBagUp: '#ffd23f',
  baseCampUp: '#8ee6a4',
  scaleUp: '#d69bff',
  scaleDown: '#9befe0',
  steal: '#ff9d5d',
};

export function label(ctx, text, x, y, {
  size = 12, color = '#fff', weight = 700, align = 'center', outline = '#0b0d14',
} = {}) {
  ctx.font = `${weight} ${size}px "Trebuchet MS", "Segoe UI", system-ui, sans-serif`;
  ctx.textAlign = align;
  ctx.textBaseline = 'middle';
  if (outline) {
    ctx.lineWidth = Math.max(2, size * 0.24);
    ctx.strokeStyle = outline;
    ctx.lineJoin = 'round';
    ctx.strokeText(text, x, y);
  }
  ctx.fillStyle = color;
  ctx.fillText(text, x, y);
}

function textW(ctx, text, size, weight) {
  ctx.font = `${weight} ${size}px "Trebuchet MS", "Segoe UI", system-ui, sans-serif`;
  return ctx.measureText(text).width;
}

function hexA(hex, a) {
  const h = hex.replace('#', '');
  const n = parseInt(h.length === 3 ? h.split('').map((c) => c + c).join('') : h, 16);
  return `rgba(${(n >> 16) & 255},${(n >> 8) & 255},${n & 255},${a})`;
}
