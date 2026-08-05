/**
 * Game shell: screen flow, the fixed-ish frame loop and global hotkeys.
 */

import { InputHub } from './core/input.js';
import { sfx } from './core/audio.js';
import { Renderer } from './game/render.js';
import { Hud } from './game/hud.js';
import { World } from './game/world.js';
import { Ui } from './ui.js';
import { label } from './game/hud.js';

// Safari < 16.4 and older Firefox lack roundRect; the renderer leans on it.
if (typeof CanvasRenderingContext2D !== 'undefined' && !CanvasRenderingContext2D.prototype.roundRect) {
  CanvasRenderingContext2D.prototype.roundRect = function roundRect(x, y, w, h, r) {
    const rr = Math.min(typeof r === 'number' ? r : (r?.[0] ?? 0), Math.abs(w) / 2, Math.abs(h) / 2);
    this.moveTo(x + rr, y);
    this.arcTo(x + w, y, x + w, y + h, rr);
    this.arcTo(x + w, y + h, x, y + h, rr);
    this.arcTo(x, y + h, x, y, rr);
    this.arcTo(x, y, x + w, y, rr);
    this.closePath();
    return this;
  };
}

class Game {
  constructor() {
    this.canvas = document.getElementById('game');
    this.renderer = new Renderer(this.canvas);
    this.hud = new Hud();
    this.hub = new InputHub(window);
    this.ui = new Ui(document.getElementById('overlay'), this);

    this.state = 'lobby';
    this.world = null;
    this.paused = false;
    this.showHelp = true;
    this.debugAI = false;
    this.resultTimer = 0;
    this.last = performance.now();

    window.addEventListener('resize', () => this.renderer.resize());
    window.addEventListener('keydown', (e) => this._hotkeys(e));
    window.addEventListener('pointerdown', () => sfx.unlock(), { once: false });
    window.addEventListener('keydown', () => sfx.unlock(), { once: false });
    window.addEventListener('gamepadconnected', () => { if (this.state === 'lobby') this.ui.showLobby(); });

    this.ui.showLobby();
    requestAnimationFrame((t) => this.loop(t));
  }

  _hotkeys(e) {
    if (e.target && /INPUT|SELECT|TEXTAREA/.test(e.target.tagName)) return;
    const k = e.code;
    if (this.state === 'lobby' && (k === 'Enter' || k === 'NumpadEnter')) { this.startMatch(); return; }
    if (this.state === 'results') {
      if (k === 'KeyR' || k === 'Enter') { this.startMatch(); return; }
      if (k === 'Escape') { this.toLobby(); return; }
    }
    if (this.state === 'playing') {
      if (k === 'KeyP') { this.paused = !this.paused; return; }
      if (k === 'Escape') { this.toLobby(); return; }
      if (k === 'KeyH') { this.showHelp = !this.showHelp; return; }
      if (k === 'F2') { this.debugAI = !this.debugAI; e.preventDefault(); return; }
    }
    if (k === 'KeyM') sfx.toggle();
  }

  startMatch() {
    sfx.unlock();
    const setup = this.ui.buildSetup(this.hub);
    this.world = new World(setup);
    this.state = 'playing';
    this.paused = false;
    this.resultTimer = 0;
    this.ui.hide();
    this.renderer.resize();
  }

  toLobby() {
    this.state = 'lobby';
    this.world = null;
    sfx.chargeStop();
    this.ui.showLobby();
  }

  loop(now) {
    requestAnimationFrame((t) => this.loop(t));
    let dt = (now - this.last) / 1000;
    this.last = now;
    if (!(dt > 0)) dt = 1 / 60;
    dt = Math.min(dt, 1 / 20);            // long stalls must not teleport anyone

    if (this.state === 'playing' && this.world) {
      if (!this.paused) {
        this.hub.update(dt);
        this.world.update(dt);
        if (this.world.state === 'ended') {
          this.resultTimer += dt;
          if (this.resultTimer > 2.0 && this.state === 'playing') {
            this.state = 'results';
            this.ui.showResults(this.world);
          }
        }
      }
      this._render();
    } else if (this.world) {
      // Results screen keeps the arena alive behind the panel.
      this.world.fx.update(dt);
      this._render();
    } else {
      this._renderIdle();
    }
    this.hub.endFrame();
  }

  _render() {
    const r = this.renderer;
    r.draw(this.world, this);
    this.hud.draw(r.ctx, this.world, { w: r.w, h: r.h }, this);
    if (this.paused) {
      const ctx = r.ctx;
      ctx.fillStyle = 'rgba(6,8,14,0.55)';
      ctx.fillRect(0, 0, r.w, r.h);
      label(ctx, 'PAUSED', r.w / 2, r.h / 2 - 10, { size: 54, weight: 900, color: '#eef2ff' });
      label(ctx, 'P to resume · Esc to quit', r.w / 2, r.h / 2 + 30, { size: 14, weight: 700, color: '#9aa6c4' });
    }
  }

  _renderIdle() {
    const r = this.renderer;
    const ctx = r.ctx;
    ctx.setTransform(r.dpr, 0, 0, r.dpr, 0, 0);
    const g = ctx.createLinearGradient(0, 0, 0, r.h);
    g.addColorStop(0, '#0d101a');
    g.addColorStop(1, '#080a11');
    ctx.fillStyle = g;
    ctx.fillRect(0, 0, r.w, r.h);
  }
}

window.addEventListener('DOMContentLoaded', () => { window.game = new Game(); });
