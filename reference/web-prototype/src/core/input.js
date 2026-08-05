/**
 * Input layer.
 *
 * The rest of the game never reads the keyboard directly: it asks a
 * `Controller` for an analogue movement vector plus two buttons (ATTACK and
 * ACTION). Controllers are backed either by a keyboard scheme or by a gamepad,
 * which is what lets the same Player code drive humans and AI alike (the AI
 * simply writes into a `VirtualController`).
 */

import { clamp } from './math.js';

export const KEY_SCHEMES = [
  {
    id: 'wasd',
    name: 'WASD',
    keys: { up: 'KeyW', left: 'KeyA', down: 'KeyS', right: 'KeyD', attack: 'Space', action: 'ShiftLeft' },
    label: 'WASD · Space punch · Shift dash',
  },
  {
    id: 'ijkl',
    name: 'IJKL',
    // Dash is U, not P: P is the global pause hotkey and P2 would keep
    // freezing the match every time they dashed.
    keys: { up: 'KeyI', left: 'KeyJ', down: 'KeyK', right: 'KeyL', attack: 'KeyO', action: 'KeyU' },
    label: 'IJKL · O punch · U dash',
  },
  {
    id: 'arrows',
    name: 'Arrows',
    keys: { up: 'ArrowUp', left: 'ArrowLeft', down: 'ArrowDown', right: 'ArrowRight', attack: 'Slash', action: 'Period' },
    label: 'Arrows · / punch · . dash',
  },
  {
    id: 'numpad',
    name: 'Numpad',
    keys: { up: 'Numpad8', left: 'Numpad4', down: 'Numpad5', right: 'Numpad6', attack: 'Numpad0', action: 'NumpadDecimal' },
    label: 'Numpad 8456 · 0 punch · . dash',
  },
];

class Button {
  constructor() {
    this.down = false;
    this.pressed = false;      // went down this frame
    this.released = false;     // went up this frame
    this.heldTime = 0;         // how long it has been down
    this.releaseHold = 0;      // how long it was down when released
  }

  update(down, dt) {
    this.pressed = down && !this.down;
    this.released = !down && this.down;
    if (this.released) this.releaseHold = this.heldTime;
    this.down = down;
    this.heldTime = down ? this.heldTime + dt : 0;
  }

  reset() {
    this.down = this.pressed = this.released = false;
    this.heldTime = this.releaseHold = 0;
  }
}

export class Controller {
  constructor(label = '') {
    this.label = label;
    this.ax = 0;
    this.ay = 0;
    this.attack = new Button();
    this.action = new Button();
    this.connected = true;
  }

  /** Magnitude of the movement stick, clamped to 1. */
  get moveMag() {
    return Math.min(1, Math.hypot(this.ax, this.ay));
  }

  reset() {
    this.ax = this.ay = 0;
    this.attack.reset();
    this.action.reset();
  }
}

/** A controller written to by the AI rather than by hardware. */
export class VirtualController extends Controller {
  constructor(label = 'CPU') {
    super(label);
    this.want = { ax: 0, ay: 0, attack: false, action: false };
  }

  update(dt) {
    this.ax = this.want.ax;
    this.ay = this.want.ay;
    this.attack.update(this.want.attack, dt);
    this.action.update(this.want.action, dt);
  }
}

export class InputHub {
  constructor(target = window) {
    this.keys = new Set();
    this.justPressed = new Set();
    this.controllers = [];
    this.anyKeySignal = false;

    this._onDown = (e) => {
      // Stop the page scrolling / activating when playing.
      if (SWALLOW.has(e.code)) e.preventDefault();
      if (!this.keys.has(e.code)) this.justPressed.add(e.code);
      this.keys.add(e.code);
      this.anyKeySignal = true;
    };
    this._onUp = (e) => this.keys.delete(e.code);
    this._onBlur = () => {
      this.keys.clear();
      for (const c of this.controllers) c.reset();
    };

    target.addEventListener('keydown', this._onDown, { passive: false });
    target.addEventListener('keyup', this._onUp);
    target.addEventListener('blur', this._onBlur);
  }

  dispose(target = window) {
    target.removeEventListener('keydown', this._onDown);
    target.removeEventListener('keyup', this._onUp);
    target.removeEventListener('blur', this._onBlur);
  }

  isDown(code) { return this.keys.has(code); }
  wasPressed(code) { return this.justPressed.has(code); }

  /** Creates (or reuses) a keyboard-backed controller for a scheme id. */
  keyboardController(schemeId) {
    const scheme = KEY_SCHEMES.find((s) => s.id === schemeId) || KEY_SCHEMES[0];
    const c = new Controller(scheme.label);
    c.kind = 'keyboard';
    c.scheme = scheme;
    c._read = (dt) => {
      const k = scheme.keys;
      c.ax = (this.isDown(k.right) ? 1 : 0) - (this.isDown(k.left) ? 1 : 0);
      c.ay = (this.isDown(k.down) ? 1 : 0) - (this.isDown(k.up) ? 1 : 0);
      // Latch: a key tapped and released between two frames would otherwise be
      // polled as "never down" and the jab would silently vanish.
      const held = (code) => this.isDown(code) || this.justPressed.has(code);
      c.attack.update(held(k.attack), dt);
      c.action.update(held(k.action), dt);
    };
    this.controllers.push(c);
    return c;
  }

  /** Gamepad-backed controller. `index` is the navigator.getGamepads() slot. */
  padController(index) {
    const c = new Controller(`Gamepad ${index + 1} · A punch · B dash`);
    c.kind = 'gamepad';
    c.padIndex = index;
    c._read = (dt) => {
      const pads = navigator.getGamepads ? navigator.getGamepads() : [];
      const pad = pads && pads[index];
      if (!pad) {
        c.connected = false;
        c.ax = c.ay = 0;
        c.attack.update(false, dt);
        c.action.update(false, dt);
        return;
      }
      c.connected = true;
      const dz = (v) => (Math.abs(v) < 0.22 ? 0 : v);
      let ax = dz(pad.axes[0] || 0);
      let ay = dz(pad.axes[1] || 0);
      // D-pad fallback (standard mapping buttons 12..15).
      if (pad.buttons.length > 15) {
        if (pad.buttons[15].pressed) ax = 1;
        if (pad.buttons[14].pressed) ax = -1;
        if (pad.buttons[13].pressed) ay = 1;
        if (pad.buttons[12].pressed) ay = -1;
      }
      c.ax = clamp(ax, -1, 1);
      c.ay = clamp(ay, -1, 1);
      const btn = (i) => !!(pad.buttons[i] && pad.buttons[i].pressed);
      c.attack.update(btn(0) || btn(2) || btn(7), dt);
      c.action.update(btn(1) || btn(3) || btn(6) || btn(5), dt);
    };
    this.controllers.push(c);
    return c;
  }

  virtual(label) {
    const c = new VirtualController(label);
    c.kind = 'cpu';
    c._read = (dt) => c.update(dt);
    this.controllers.push(c);
    return c;
  }

  clearControllers() {
    this.controllers.length = 0;
  }

  update(dt) {
    for (const c of this.controllers) c._read(dt);
  }

  /** Called at the very end of a frame. */
  endFrame() {
    this.justPressed.clear();
    this.anyKeySignal = false;
  }

  static connectedPads() {
    const pads = navigator.getGamepads ? navigator.getGamepads() : [];
    const out = [];
    for (let i = 0; i < pads.length; i++) if (pads[i]) out.push(i);
    return out;
  }
}

const SWALLOW = new Set([
  'Space', 'ArrowUp', 'ArrowDown', 'ArrowLeft', 'ArrowRight',
  'Slash', 'Period', 'Numpad0', 'NumpadDecimal', 'Tab',
]);
