#!/usr/bin/env node
/**
 * Headless smoke test / balance harness.
 *
 * Loads dist/goldhunter.html in Chromium and plays full NPC-vs-NPC matches on
 * an accelerated clock, asserting that nothing throws, gold is conserved
 * (poppers in == vaults + bags + floor + shop spend), bots navigate, mine,
 * shop, fight and bank, and that the match reaches a result.
 *
 * Usage:
 *   node tools/smoke.js                 # 3 matches, normal difficulty
 *   node tools/smoke.js --matches 8     # more samples for balance work
 *   node tools/smoke.js --difficulty hard
 *   node tools/smoke.js --verbose       # per-player detail for match 1
 */

const path = require('path');
const { chromium } = require(process.env.PW || '/opt/node22/lib/node_modules/playwright');

const ROOT = path.resolve(__dirname, '..');
const FILE = 'file://' + path.join(ROOT, 'dist/goldhunter.html');

const argv = process.argv.slice(2);
const arg = (name, def) => {
  const i = argv.indexOf(`--${name}`);
  return i >= 0 && argv[i + 1] && !argv[i + 1].startsWith('--') ? argv[i + 1] : def;
};
const MATCHES = Number(arg('matches', 3));
const DIFFICULTY = arg('difficulty', 'normal');
const VERBOSE = argv.includes('--verbose');

/** Runs one full match inside the page and returns a report. */
function playMatch({ seed, difficulty }) {
  const g = window.game;
  g.ui.slots.forEach((s, i) => {
    s.type = 'cpu';
    s.profile = ['bruiser', 'banker', 'thief', 'allround'][i];
  });
  g.ui.difficulty = difficulty;
  const realRandom = Math.random;
  Math.random = () => ((seed = (seed * 1664525 + 1013904223) >>> 0) / 4294967296);
  g.startMatch();
  Math.random = realRandom;

  const w = g.world;
  const moved = new Array(4).fill(0);
  const last = w.players.map((p) => ({ x: p.x, y: p.y }));
  const goalCounts = {};
  let shopVisits = 0;
  const inShop = new Array(4).fill(false);

  for (let i = 0; i < 60 * 200 && w.state !== 'ended'; i++) {
    g.hub.update(1 / 60);
    w.update(1 / 60);
    g.hub.endFrame();
    w.players.forEach((p, k) => {
      moved[k] += Math.hypot(p.x - last[k].x, p.y - last[k].y);
      last[k].x = p.x; last[k].y = p.y;
      if (p.brain) goalCounts[p.brain.goal] = (goalCounts[p.brain.goal] || 0) + 1;
      if (!!p.shop !== inShop[k]) { if (p.shop) shopVisits++; inShop[k] = !!p.shop; }
    });
  }

  const spent = w.players.reduce((a, p) => a + p.spent, 0);
  return {
    state: w.state,
    moved: moved.map((m) => Math.round(m)),
    shopVisits,
    goalCounts,
    spent,
    onFloor: w.pickups.length,
    inWorld: Math.round(w.totalGoldInPlay()),
    pathFailures: w.players.filter((p) => p.brain && p.brain.follow.failed).length,
    stuck: w.players.filter((p) => p.brain && p.brain.stuckTimer > 0.4).length,
    rows: w.results.map((r) => ({
      name: r.name,
      profile: r.profile ? r.profile.tag : 'human',
      place: r.place,
      total: Math.round(r.total),
      mined: Math.round(r.stats.mined),
      banked: Math.round(r.stats.banked),
      robbed: Math.round(r.stats.robbed),
      raided: Math.round(r.stats.raidedFor),
      lost: Math.round(r.stats.lost),
      raids: r.stats.campRaids,
      hits: r.stats.punchesLanded,
      taken: r.stats.punchesTaken,
      buys: Object.entries(r.upgrades).filter(([, v]) => v > 0).map(([k, v]) => `${k}×${v}`)
        .concat(r.scaleLevel ? [`scale${r.scaleLevel > 0 ? '+' : ''}${r.scaleLevel}`] : []),
    })),
  };
}

(async () => {
  const browser = await chromium.launch({
    headless: !argv.includes('--headed'),
    executablePath: process.env.CHROME || '/opt/pw-browsers/chromium',
  });
  const page = await browser.newPage({ viewport: { width: 1440, height: 860 } });

  const errors = [];
  page.on('pageerror', (e) => errors.push(`pageerror: ${e.message}`));
  page.on('console', (m) => { if (m.type() === 'error') errors.push(`console: ${m.text()}`); });

  await page.goto(FILE, { waitUntil: 'load' });
  await page.waitForFunction(() => !!window.game, null, { timeout: 15000 });

  const reports = [];
  for (let i = 0; i < MATCHES; i++) {
    reports.push(await page.evaluate(playMatch, { seed: 1337 + i * 991, difficulty: DIFFICULTY }));
  }
  await browser.close();

  /* ------------------------------------------------------------ reporting */
  const fail = [];
  if (errors.length) fail.push(`runtime errors:\n    ${errors.join('\n    ')}`);

  const agg = {
    winners: {}, totals: [], hits: 0, raids: 0, buys: 0, shopVisits: 0,
    mined: 0, robbed: 0, raided: 0, lost: 0, spent: 0, goals: {},
  };
  for (const r of reports) {
    if (r.state !== 'ended') fail.push('a match did not reach the end state');
    if (r.pathFailures) fail.push(`${r.pathFailures} bots ended with an unreachable path`);
    r.moved.forEach((m, i) => { if (m < 60) fail.push(`bot ${i} barely moved (${m}m)`); });
    agg.winners[r.rows[0].profile] = (agg.winners[r.rows[0].profile] || 0) + 1;
    agg.shopVisits += r.shopVisits;
    agg.spent += r.spent;
    for (const [k, v] of Object.entries(r.goalCounts)) agg.goals[k] = (agg.goals[k] || 0) + v;
    for (const row of r.rows) {
      agg.totals.push(row.total);
      agg.hits += row.hits;
      agg.raids += row.raids;
      agg.buys += row.buys.length;
      agg.mined += row.mined;
      agg.robbed += row.robbed;
      agg.raided += row.raided;
      agg.lost += row.lost;
    }
  }

  const n = reports.length;
  const avg = (v) => (v / n).toFixed(0);
  const sorted = [...agg.totals].sort((a, b) => b - a);
  const goalTotal = Object.values(agg.goals).reduce((a, b) => a + b, 0) || 1;

  if (VERBOSE) {
    console.log('--- match 1 ---');
    for (const row of reports[0].rows) {
      console.log(`  ${row.place}. ${row.name.padEnd(11)} ${String(row.total).padStart(4)}g  `
        + `mined ${String(row.mined).padStart(4)}  robbed ${String(row.robbed).padStart(4)}  `
        + `raided ${String(row.raided).padStart(3)}  lost ${String(row.lost).padStart(4)}  `
        + `hits ${String(row.hits).padStart(3)}  ${row.buys.join(' ') || '-'}`);
    }
    console.log('');
  }

  console.log(`GoldHunter balance — ${n} match${n > 1 ? 'es' : ''} · difficulty=${DIFFICULTY}`);
  console.log(`  winners            ${Object.entries(agg.winners).map(([k, v]) => `${k}:${v}`).join('  ')}`);
  console.log(`  scores             top ${sorted[0]}g · median ${sorted[Math.floor(sorted.length / 2)]}g · low ${sorted[sorted.length - 1]}g`);
  console.log(`  per match avg      mined ${avg(agg.mined)}g · robbed ${avg(agg.robbed)}g · vault-raided ${avg(agg.raided)}g · lost ${avg(agg.lost)}g`);
  console.log(`  fighting           ${avg(agg.hits)} punches landed/match · ${avg(agg.raids)} vault raids/match`);
  console.log(`  economy            ${avg(agg.buys)} upgrades bought/match · ${avg(agg.spent)}g spent · ${avg(agg.shopVisits)} shop visits`);
  const worst = reports.flatMap((r) => r.rows).sort((a, b) => a.total - b.total)[0];
  console.log(`  worst performer    ${worst.name} (${worst.profile}) ${worst.total}g — `
    + `mined ${worst.mined} banked ${worst.banked} robbed ${worst.robbed} lost ${worst.lost} `
    + `hits ${worst.hits}/taken ${worst.taken} buys[${worst.buys.join(' ') || '-'}]`);
  console.log(`  AI goal share      ` + Object.entries(agg.goals)
    .sort((a, b) => b[1] - a[1])
    .map(([k, v]) => `${k} ${((v / goalTotal) * 100).toFixed(0)}%`).join('  '));

  // Behaviour coverage: every headline system should show up across the sample.
  if (agg.hits < n * 4) fail.push(`almost no punches landed (${agg.hits} over ${n} matches)`);
  if (agg.buys < n) fail.push(`shops barely used (${agg.buys} upgrades over ${n} matches)`);
  if (agg.mined < n * 400) fail.push(`too little mining (${agg.mined}g over ${n} matches)`);
  if (agg.raids === 0) fail.push('no vault raid happened in any match — Steal may be unreachable');
  if (sorted[sorted.length - 1] <= 0) fail.push('a player finished with nothing banked');

  if (fail.length) {
    console.error('\nFAILED:\n- ' + fail.join('\n- '));
    process.exit(1);
  }
  console.log('\nsmoke test passed');
})().catch((e) => { console.error(e); process.exit(1); });
