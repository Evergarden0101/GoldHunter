#!/usr/bin/env node
/**
 * Screenshot helper — renders the lobby, a mid-match frame and the results
 * board from the built game into docs/.
 *
 * Usage: node tools/shots.js [--out docs] [--at 60]
 */

const path = require('path');
const fs = require('fs');
const { chromium } = require(process.env.PW || '/opt/node22/lib/node_modules/playwright');

const ROOT = path.resolve(__dirname);
const argv = process.argv.slice(2);
const arg = (n, d) => {
  const i = argv.indexOf(`--${n}`);
  return i >= 0 && argv[i + 1] ? argv[i + 1] : d;
};
const OUT = path.resolve(ROOT, arg('out', 'docs'));
const AT = Number(arg('at', 55));           // seconds into the match

(async () => {
  fs.mkdirSync(OUT, { recursive: true });
  const browser = await chromium.launch({
    executablePath: process.env.CHROME || '/opt/pw-browsers/chromium',
  });
  const page = await browser.newPage({ viewport: { width: 1440, height: 860 }, deviceScaleFactor: 1 });
  page.on('pageerror', (e) => console.log('PAGE ERROR:', e.message));
  page.on('console', (m) => { if (m.type() === 'error') console.log('CONSOLE:', m.text()); });

  await page.goto('file://' + path.join(ROOT, 'dist/goldhunter.html'), { waitUntil: 'load' });
  await page.waitForFunction(() => !!window.game, null, { timeout: 15000 });
  await page.waitForTimeout(300);
  await page.screenshot({ path: path.join(OUT, 'lobby.png') });

  // Mid-match: four bots, stepped to `AT` seconds, then one rendered frame.
  await page.evaluate(({ at }) => {
    const g = window.game;
    g.ui.slots.forEach((s, i) => { s.type = 'cpu'; s.profile = ['bruiser', 'banker', 'thief', 'allround'][i]; });
    g.startMatch();
    const w = g.world;
    for (let i = 0; i < 60 * at; i++) { g.hub.update(1 / 60); w.update(1 / 60); g.hub.endFrame(); }
    // Nudge some juice into frame so the shot shows the effects layer.
    const p = w.players[0];
    w.fx.shake(0.35);
    w.fx.text(p.x, p.y - 2, '+24', { color: '#ffc939', size: 1.3, life: 2 });
    g._render();
  }, { at: AT });
  await page.screenshot({ path: path.join(OUT, 'match.png') });

  // A shop panel open, for the UI shot.
  await page.evaluate(() => {
    const g = window.game;
    const w = g.world;
    const p = w.players[0];
    p.x = w.shops[1].x - 3; p.y = w.shops[1].y;
    p.bag = 64;
    for (let i = 0; i < 12; i++) { g.hub.update(1 / 60); w.update(1 / 60); g.hub.endFrame(); }
    p.shopIndex = 6;
    p.buyHold = 0.3;
    g._render();
  });
  await page.screenshot({ path: path.join(OUT, 'shop.png') });

  // Results.
  await page.evaluate(() => {
    const g = window.game;
    const w = g.world;
    while (w.state !== 'ended') { g.hub.update(1 / 60); w.update(1 / 60); g.hub.endFrame(); }
    g._render();
    g.state = 'results';
    g.ui.showResults(w);
  });
  await page.waitForTimeout(200);
  await page.screenshot({ path: path.join(OUT, 'results.png') });

  await browser.close();
  console.log('wrote lobby.png, match.png, shop.png, results.png to', path.relative(ROOT, OUT) || '.');
})().catch((e) => { console.error(e); process.exit(1); });
