/**
 * DOM screens: the lobby (slot setup) and the results board.
 * The match itself is drawn entirely on the canvas.
 */

import { KEY_SCHEMES, InputHub } from './core/input.js';
import { NPC_PROFILES, DIFFICULTY, COLORS, MATCH, PLAYER, POPPERS, ITEMS } from './config.js';

const CORNER_NAMES = ['North-West', 'North-East', 'South-West', 'South-East'];

export class Ui {
  constructor(root, game) {
    this.root = root;
    this.game = game;
    this.slots = [
      { type: 'human', scheme: 'wasd', pad: -1, profile: 'allround' },
      { type: 'cpu', scheme: 'ijkl', pad: -1, profile: 'bruiser' },
      { type: 'cpu', scheme: 'arrows', pad: -1, profile: 'banker' },
      { type: 'cpu', scheme: 'numpad', pad: -1, profile: 'thief' },
    ];
    this.difficulty = 'normal';
  }

  clear() { this.root.innerHTML = ''; this.root.className = 'overlay'; }

  /* --------------------------------------------------------------- lobby */

  showLobby() {
    this.clear();
    this.root.classList.add('show');
    const pads = InputHub.connectedPads();

    const el = document.createElement('div');
    el.className = 'panel lobby';
    el.innerHTML = `
      <header>
        <h1>GOLD<span>HUNTER</span></h1>
        <p class="tagline">
          Four prospectors. One motherlode. ${MATCH.duration / 60 === 2.5 ? '2·5' : MATCH.duration}
          minutes to bank more gold than anyone else.
        </p>
      </header>
      <div class="slots"></div>
      <div class="row options">
        <label>CPU skill
          <select id="difficulty">
            ${Object.entries(DIFFICULTY).map(([k, v]) =>
    `<option value="${k}" ${k === this.difficulty ? 'selected' : ''}>${v.label}</option>`).join('')}
          </select>
        </label>
        <label class="check"><input type="checkbox" id="showpaths"> show AI paths</label>
        <button class="start" id="start">START MATCH <kbd>Enter</kbd></button>
      </div>
      <div class="rules">
        <div>
          <h3>How to win</h3>
          <ul>
            <li>Only gold <b>inside your base camp</b> at the whistle counts. Carried gold is worth nothing.</li>
            <li>Camps sit <b>25 m</b> from the centre <b>Motherlode</b> (${POPPERS.big.start}g, +${POPPERS.big.ratePerMin}/min).</li>
            <li>Two small poppers hold ${POPPERS.small.start}g and pump +${POPPERS.small.ratePerMin}/min.</li>
            <li>Your bag starts at <b>${PLAYER.bagCapacity}g</b> — walk it home before someone takes it.</li>
          </ul>
        </div>
        <div>
          <h3>Fighting &amp; shopping</h3>
          <ul>
            <li><b>Tap punch</b> for a quick jab, <b>hold</b> to charge a smash that rips far more gold.</li>
            <li>Two shops sell ${ITEMS.length} upgrades. They bill your <b>bag first, then your vault</b> — so every upgrade costs you score.</li>
            <li>Buy <b>Steal</b> to punch open enemy vaults.</li>
            <li>Last ${MATCH.rushAt}s is the <b>Gold Rush</b>: poppers pump ${MATCH.rushPopperMultiplier}×.</li>
          </ul>
        </div>
      </div>
      <footer>Empty seats are filled by NPCs · <kbd>M</kbd> mute · <kbd>P</kbd> pause · <kbd>H</kbd> hints</footer>
    `;
    this.root.appendChild(el);

    const slotsEl = el.querySelector('.slots');
    this.slots.forEach((slot, i) => slotsEl.appendChild(this._slotCard(slot, i, pads)));

    el.querySelector('#difficulty').addEventListener('change', (e) => { this.difficulty = e.target.value; });
    el.querySelector('#showpaths').addEventListener('change', (e) => { this.game.debugAI = e.target.checked; });
    el.querySelector('#start').addEventListener('click', () => this.game.startMatch());
  }

  _slotCard(slot, i, pads) {
    const card = document.createElement('div');
    card.className = 'slot';
    card.style.setProperty('--c', COLORS.players[i]);
    const schemeOpts = KEY_SCHEMES.map((s) =>
      `<option value="k:${s.id}" ${slot.type === 'human' && slot.pad < 0 && slot.scheme === s.id ? 'selected' : ''}>${s.name}</option>`).join('');
    const padOpts = pads.map((p) =>
      `<option value="p:${p}" ${slot.pad === p ? 'selected' : ''}>Gamepad ${p + 1}</option>`).join('');
    const profOpts = NPC_PROFILES.map((p) =>
      `<option value="${p.id}" ${slot.profile === p.id ? 'selected' : ''}>${p.name} · ${p.tag}</option>`).join('');

    card.innerHTML = `
      <div class="slot-head">
        <span class="chip"></span>
        <b>P${i + 1}</b>
        <span class="corner">${CORNER_NAMES[i]}</span>
      </div>
      <div class="toggle">
        <button data-type="human" class="${slot.type === 'human' ? 'on' : ''}">HUMAN</button>
        <button data-type="cpu" class="${slot.type === 'cpu' ? 'on' : ''}">NPC</button>
      </div>
      <div class="cfg human-cfg" ${slot.type === 'human' ? '' : 'hidden'}>
        <select class="device">${schemeOpts}${padOpts}</select>
        <small class="hint"></small>
      </div>
      <div class="cfg cpu-cfg" ${slot.type === 'cpu' ? '' : 'hidden'}>
        <select class="profile">${profOpts}</select>
        <small class="hint"></small>
      </div>
    `;

    const humanCfg = card.querySelector('.human-cfg');
    const cpuCfg = card.querySelector('.cpu-cfg');
    const refresh = () => {
      humanCfg.hidden = slot.type !== 'human';
      cpuCfg.hidden = slot.type !== 'cpu';
      card.classList.toggle('cpu', slot.type === 'cpu');
      if (slot.type === 'human') {
        const scheme = KEY_SCHEMES.find((s) => s.id === slot.scheme);
        humanCfg.querySelector('.hint').textContent = slot.pad >= 0
          ? 'Left stick · A punch · B dash'
          : (scheme ? scheme.label : '');
      } else {
        const prof = NPC_PROFILES.find((p) => p.id === slot.profile);
        cpuCfg.querySelector('.hint').textContent = prof
          ? `atk ${pct(prof.attackWill)} · bank ${pct(prof.saveGoldWill)} · steal ${pct(prof.stealWill)}`
          : '';
      }
    };

    card.querySelectorAll('.toggle button').forEach((b) => {
      b.addEventListener('click', () => {
        slot.type = b.dataset.type;
        card.querySelectorAll('.toggle button').forEach((x) => x.classList.toggle('on', x === b));
        refresh();
      });
    });
    card.querySelector('.device').addEventListener('change', (e) => {
      const [kind, val] = e.target.value.split(':');
      if (kind === 'k') { slot.scheme = val; slot.pad = -1; } else { slot.pad = Number(val); }
      refresh();
    });
    card.querySelector('.profile').addEventListener('change', (e) => { slot.profile = e.target.value; refresh(); });
    refresh();
    return card;
  }

  /* ------------------------------------------------------------- results */

  showResults(world) {
    this.clear();
    this.root.classList.add('show');
    const rows = world.results;
    const el = document.createElement('div');
    el.className = 'panel results';
    el.innerHTML = `
      <header>
        <h1 class="winner" style="color:${rows[0].color}">${rows[0].name} WINS</h1>
        <p class="tagline">${Math.floor(rows[0].total)} gold banked · ${MATCH.duration}s match</p>
      </header>
      <table>
        <thead>
          <tr><th>#</th><th>Player</th><th class="n">Vault</th><th class="n">Bonus</th>
          <th class="n">Total</th><th class="n">Mined</th><th class="n">Robbed</th>
          <th class="n">Lost</th><th class="n">Raids</th><th class="n">Hits</th></tr>
        </thead>
        <tbody>
          ${rows.map((r) => `
            <tr class="${r.place === 1 ? 'first' : ''}">
              <td><span class="place">${r.place}</span></td>
              <td><span class="dot" style="background:${r.color}"></span>${r.name}
                <small>${r.isHuman ? 'human' : (r.profile ? r.profile.tag : 'cpu')}</small></td>
              <td class="n">${Math.floor(r.vault)}</td>
              <td class="n">${r.bonus > 0 ? '+' + Math.floor(r.bonus) : '—'}</td>
              <td class="n strong">${Math.floor(r.total)}</td>
              <td class="n">${Math.floor(r.stats.mined)}</td>
              <td class="n">${Math.floor(r.stats.robbed + r.stats.raidedFor)}</td>
              <td class="n">${Math.floor(r.stats.lost)}</td>
              <td class="n">${r.stats.campRaids}</td>
              <td class="n">${r.stats.punchesLanded}</td>
            </tr>`).join('')}
        </tbody>
      </table>
      <div class="row options">
        <button class="start" id="rematch">REMATCH <kbd>R</kbd></button>
        <button class="ghost" id="tolobby">CHANGE LINE-UP <kbd>Esc</kbd></button>
      </div>
    `;
    this.root.appendChild(el);
    el.querySelector('#rematch').addEventListener('click', () => this.game.startMatch());
    el.querySelector('#tolobby').addEventListener('click', () => this.game.toLobby());
  }

  hide() {
    this.clear();
    this.root.classList.remove('show');
  }

  /** Turns the lobby config into World setup slots. */
  buildSetup(hub) {
    hub.clearControllers();
    let humans = 0;
    const slots = this.slots.map((slot, i) => {
      if (slot.type === 'human') {
        humans++;
        const controller = slot.pad >= 0 ? hub.padController(slot.pad) : hub.keyboardController(slot.scheme);
        return { type: 'human', controller, name: humans === 1 && this.humanCount() === 1 ? 'YOU' : `P${i + 1}` };
      }
      const profile = NPC_PROFILES.find((p) => p.id === slot.profile) || NPC_PROFILES[3];
      return { type: 'cpu', controller: hub.virtual(profile.name), profile, name: profile.name };
    });
    return { slots, difficulty: this.difficulty, seed: (Math.random() * 0xffff) | 0 };
  }

  humanCount() { return this.slots.filter((s) => s.type === 'human').length; }
}

const pct = (v) => `${Math.round(v * 100)}%`;
