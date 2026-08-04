#!/usr/bin/env node
/**
 * Human-input test.
 *
 * The smoke test drives bots through their virtual controllers, which would
 * happily pass even if real keyboard handling were broken. This one presses
 * actual keys in a real browser and checks that a human player can move, jab,
 * charge a smash, dash and buy from a shop.
 *
 * Usage: node tools/input-test.js [--headed]
 */

const path = require('path');
const { chromium } = require(process.env.PW || '/opt/node22/lib/node_modules/playwright');

const ROOT = path.resolve(__dirname);
const FILE = 'file://' + path.join(ROOT, 'dist/goldhunter.html');
const argv = process.argv.slice(2);

const checks = [];
const check = (name, ok, detail = '') => {
  checks.push({ name, ok, detail });
  console.log(`  ${ok ? 'PASS' : 'FAIL'}  ${name}${detail ? `  (${detail})` : ''}`);
};

(async () => {
  const browser = await chromium.launch({
    headless: !argv.includes('--headed'),
    executablePath: process.env.CHROME || '/opt/pw-browsers/chromium',
  });
  const page = await browser.newPage({ viewport: { width: 1280, height: 800 } });
  const errors = [];
  page.on('pageerror', (e) => errors.push(e.message));
  page.on('console', (m) => { if (m.type() === 'error') errors.push(m.text()); });

  await page.goto(FILE, { waitUntil: 'load' });
  await page.waitForFunction(() => !!window.game);

  // P1 human on WASD, the rest bots. Start via the real button.
  await page.click('#start');
  await page.waitForFunction(() => window.game.state === 'playing');

  // Record punches as they are thrown.
  await page.evaluate(() => {
    const w = window.game.world;
    window.__punches = [];
    const orig = w.registerPunch.bind(w);
    w.registerPunch = (p) => { if (p.index === 0) window.__punches.push(p.punchPower); return orig(p); };
  });

  // Let the 3-2-1 countdown finish.
  await page.waitForFunction(() => window.game.world.state === 'playing' && window.game.world.time > 0.2,
    null, { timeout: 8000 });

  const me = () => page.evaluate(() => {
    const p = window.game.world.players[0];
    return {
      x: p.x, y: p.y, bag: p.bag, facing: p.facing, dash: p.dashTimer,
      dashCd: p.dashCooldown, shop: !!p.shop, shopIndex: p.shopIndex,
      purchases: p.purchases.slice(), charging: p.charging, chargeRatio: p.chargeRatio,
      isHuman: p.isHuman,
    };
  });

  check('P1 slot is human', (await me()).isHuman);

  // ---- move -------------------------------------------------------------
  const before = await me();
  await page.keyboard.down('d');
  await page.waitForTimeout(700);
  await page.keyboard.up('d');
  const afterMove = await me();
  check('D key moves the player right', afterMove.x - before.x > 1.5,
    `moved ${(afterMove.x - before.x).toFixed(2)}m`);

  // ---- light jab --------------------------------------------------------
  await page.keyboard.press(' ');
  await page.waitForTimeout(350);
  let punches = await page.evaluate(() => window.__punches);
  check('tap Space throws a light jab', punches.length >= 1 && punches[0] === 0,
    `power=${punches[0]}`);

  // ---- charged smash ----------------------------------------------------
  await page.evaluate(() => { window.__punches.length = 0; });
  await page.keyboard.down(' ');
  await page.waitForTimeout(500);
  const midCharge = await me();
  await page.waitForTimeout(900);
  await page.keyboard.up(' ');
  await page.waitForTimeout(400);
  punches = await page.evaluate(() => window.__punches);
  check('holding Space charges', midCharge.charging && midCharge.chargeRatio > 0.1,
    `ratio=${midCharge.chargeRatio.toFixed(2)}`);
  check('releasing throws a charged smash', punches.length >= 1 && punches[0] > 0.8,
    `power=${(punches[0] ?? 0).toFixed(2)}`);

  // ---- dash -------------------------------------------------------------
  // Dashing is deliberately gated on an idle stance, so let the punch recover.
  await page.waitForFunction(() => window.game.world.players[0].phase === 'idle', null, { timeout: 3000 });
  await page.evaluate(() => { window.game.world.players[0].dashCooldown = 0; });
  await page.keyboard.press('Shift');
  await page.waitForTimeout(80);
  const dashed = await page.evaluate(() => window.game.world.players[0].dashCooldown > 1);
  check('Shift dashes', dashed);

  // ---- shop -------------------------------------------------------------
  await page.evaluate(() => {
    const w = window.game.world;
    const p = w.players[0];
    p.x = w.shops[0].x + 2; p.y = w.shops[0].y;
    p.vx = p.vy = 0;
    p.dashTimer = 0;                  // an in-flight dash would carry us back out
    p.bag = 40;                       // enough for any tier-1 upgrade
  });
  await page.waitForTimeout(300);
  const atShop = await page.evaluate(() => {
    const w = window.game.world; const p = w.players[0];
    return { px: +p.x.toFixed(2), py: +p.y.toFixed(2), sx: w.shops[0].x, sy: w.shops[0].y,
      d: +Math.hypot(p.x - w.shops[0].x, p.y - w.shops[0].y).toFixed(2),
      dash: +p.dashTimer.toFixed(2), shop: !!p.shop, state: w.state, t: +w.time.toFixed(1) };
  });
  check('walking into a shop opens the panel', atShop.shop, JSON.stringify(atShop));

  const idxBefore = (await me()).shopIndex;
  await page.keyboard.press('Shift');      // action = cycle selection
  await page.waitForTimeout(200);
  const idxAfter = (await me()).shopIndex;
  check('action key cycles the shop selection', idxAfter !== idxBefore,
    `${idxBefore} -> ${idxAfter}`);

  // Select Attack Up (index 0) then hold punch to confirm.
  await page.evaluate(() => { window.game.world.players[0].shopIndex = 0; });
  await page.keyboard.down(' ');
  await page.waitForTimeout(800);
  await page.keyboard.up(' ');
  await page.waitForTimeout(200);
  const bought = await me();
  check('holding punch buys the selected item', bought.purchases.includes('attackUp'),
    `purchases=[${bought.purchases.join(',')}] bag=${bought.bag.toFixed(0)}`);

  // ---- pause / hotkeys --------------------------------------------------
  await page.keyboard.press('p');
  await page.waitForTimeout(120);
  check('P pauses', await page.evaluate(() => window.game.paused === true));
  await page.keyboard.press('p');
  await page.waitForTimeout(120);
  check('P resumes', await page.evaluate(() => window.game.paused === false));

  check('no runtime errors', errors.length === 0, errors.join(' | '));

  await browser.close();
  const failed = checks.filter((c) => !c.ok);
  console.log(`\n${checks.length - failed.length}/${checks.length} checks passed`);
  if (failed.length) process.exit(1);
})().catch((e) => { console.error(e); process.exit(1); });
