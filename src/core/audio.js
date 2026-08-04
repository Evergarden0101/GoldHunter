/**
 * Tiny WebAudio synth. Every sound is generated from oscillators and noise
 * buffers so the whole game stays a single dependency-free file.
 */

export class Sfx {
  constructor() {
    this.ctx = null;
    this.master = null;
    this.enabled = true;
    this.volume = 0.55;
    this._noise = null;
    this._chargeVoice = null;
  }

  /** Must be called from a user gesture (browsers block audio otherwise). */
  unlock() {
    if (this.ctx) {
      if (this.ctx.state === 'suspended') this.ctx.resume();
      return;
    }
    const AC = window.AudioContext || window.webkitAudioContext;
    if (!AC) { this.enabled = false; return; }
    this.ctx = new AC();
    this.master = this.ctx.createGain();
    this.master.gain.value = this.volume;
    this.master.connect(this.ctx.destination);

    const len = Math.floor(this.ctx.sampleRate * 0.6);
    const buf = this.ctx.createBuffer(1, len, this.ctx.sampleRate);
    const data = buf.getChannelData(0);
    for (let i = 0; i < len; i++) data[i] = Math.random() * 2 - 1;
    this._noise = buf;
  }

  setVolume(v) {
    this.volume = v;
    if (this.master) this.master.gain.value = v;
  }

  toggle() {
    this.enabled = !this.enabled;
    if (this.master) this.master.gain.value = this.enabled ? this.volume : 0;
    return this.enabled;
  }

  get t() { return this.ctx ? this.ctx.currentTime : 0; }

  _tone({ freq = 440, to = null, type = 'sine', dur = 0.16, gain = 0.3, delay = 0, attack = 0.005 }) {
    if (!this.ctx || !this.enabled) return;
    const t0 = this.t + delay;
    const osc = this.ctx.createOscillator();
    const g = this.ctx.createGain();
    osc.type = type;
    osc.frequency.setValueAtTime(freq, t0);
    if (to !== null) osc.frequency.exponentialRampToValueAtTime(Math.max(20, to), t0 + dur);
    g.gain.setValueAtTime(0.0001, t0);
    g.gain.exponentialRampToValueAtTime(gain, t0 + attack);
    g.gain.exponentialRampToValueAtTime(0.0001, t0 + dur);
    osc.connect(g).connect(this.master);
    osc.start(t0);
    osc.stop(t0 + dur + 0.05);
  }

  _noiseBurst({ dur = 0.15, gain = 0.3, freq = 900, q = 1.2, delay = 0, type = 'lowpass' }) {
    if (!this.ctx || !this.enabled || !this._noise) return;
    const t0 = this.t + delay;
    const src = this.ctx.createBufferSource();
    src.buffer = this._noise;
    const filt = this.ctx.createBiquadFilter();
    filt.type = type;
    filt.frequency.setValueAtTime(freq, t0);
    filt.Q.value = q;
    const g = this.ctx.createGain();
    g.gain.setValueAtTime(gain, t0);
    g.gain.exponentialRampToValueAtTime(0.0001, t0 + dur);
    src.connect(filt).connect(g).connect(this.master);
    src.start(t0);
    src.stop(t0 + dur + 0.02);
  }

  punch(power = 0) {
    this._noiseBurst({ dur: 0.1 + power * 0.14, gain: 0.32 + power * 0.3, freq: 700 + power * 500, q: 0.9 });
    this._tone({ freq: 190 - power * 50, to: 55, type: 'square', dur: 0.13 + power * 0.14, gain: 0.22 + power * 0.2 });
    if (power > 0.55) this._tone({ freq: 90, to: 40, type: 'sawtooth', dur: 0.25, gain: 0.18, delay: 0.01 });
  }

  whiff() {
    this._noiseBurst({ dur: 0.11, gain: 0.1, freq: 2200, q: 0.6, type: 'bandpass' });
  }

  /** Rising whine while a charged punch builds. */
  charge(power) {
    if (!this.ctx || !this.enabled) return;
    if (!this._chargeVoice) {
      const osc = this.ctx.createOscillator();
      const g = this.ctx.createGain();
      osc.type = 'triangle';
      g.gain.value = 0.0001;
      osc.connect(g).connect(this.master);
      osc.start();
      this._chargeVoice = { osc, g };
    }
    const { osc, g } = this._chargeVoice;
    const t = this.t;
    osc.frequency.setTargetAtTime(180 + power * 620, t, 0.03);
    g.gain.setTargetAtTime(0.03 + power * 0.09, t, 0.03);
  }

  chargeStop() {
    if (!this._chargeVoice) return;
    this._chargeVoice.g.gain.setTargetAtTime(0.0001, this.t, 0.02);
  }

  coin(pitch = 0) {
    this._tone({ freq: 900 + pitch * 120, type: 'square', dur: 0.05, gain: 0.09 });
    this._tone({ freq: 1500 + pitch * 160, type: 'square', dur: 0.07, gain: 0.07, delay: 0.03 });
  }

  deposit() {
    this._tone({ freq: 520, type: 'triangle', dur: 0.1, gain: 0.16 });
    this._tone({ freq: 780, type: 'triangle', dur: 0.12, gain: 0.14, delay: 0.06 });
    this._tone({ freq: 1040, type: 'triangle', dur: 0.16, gain: 0.12, delay: 0.12 });
  }

  buy() {
    [660, 880, 1320].forEach((f, i) =>
      this._tone({ freq: f, type: 'square', dur: 0.12, gain: 0.12, delay: i * 0.055 }));
  }

  deny() {
    this._tone({ freq: 160, to: 90, type: 'sawtooth', dur: 0.16, gain: 0.14 });
  }

  steal() {
    this._tone({ freq: 300, to: 1200, type: 'sawtooth', dur: 0.28, gain: 0.16 });
    this._noiseBurst({ dur: 0.3, gain: 0.14, freq: 1800, q: 3, type: 'bandpass' });
  }

  alarm() {
    for (let i = 0; i < 3; i++) {
      this._tone({ freq: 880, to: 620, type: 'square', dur: 0.13, gain: 0.12, delay: i * 0.16 });
    }
  }

  dash() {
    this._noiseBurst({ dur: 0.16, gain: 0.14, freq: 1400, q: 1.4, type: 'bandpass' });
  }

  pop() {
    this._tone({ freq: 420, to: 900, type: 'sine', dur: 0.09, gain: 0.09 });
  }

  beep(high = false) {
    this._tone({ freq: high ? 900 : 520, type: 'square', dur: high ? 0.35 : 0.16, gain: 0.18 });
  }

  fanfare() {
    [523, 659, 784, 1046].forEach((f, i) =>
      this._tone({ freq: f, type: 'triangle', dur: 0.4, gain: 0.16, delay: i * 0.12 }));
  }

  rush() {
    this._tone({ freq: 220, to: 660, type: 'sawtooth', dur: 0.5, gain: 0.14 });
    this._noiseBurst({ dur: 0.5, gain: 0.1, freq: 900, q: 0.7 });
  }
}

export const sfx = new Sfx();
