/** Shop catalogue helpers: pricing, availability and purchase application. */

import { ITEMS } from '../config.js';

export const ITEM_BY_ID = Object.fromEntries(ITEMS.map((i) => [i.id, i]));

/** Current level of an item for a player (scaleUp/scaleDown share one axis). */
export function levelOf(player, id) {
  if (id === 'scaleUp') return Math.max(0, player.scaleLevel);
  if (id === 'scaleDown') return Math.max(0, -player.scaleLevel);
  return player.upgrades[id] || 0;
}

export function priceOf(player, id) {
  const def = ITEM_BY_ID[id];
  return def.cost(levelOf(player, id));
}

export function isMaxed(player, id) {
  const def = ITEM_BY_ID[id];
  return levelOf(player, id) >= def.max;
}

/**
 * Spendable gold: what you are carrying plus what is in your vault.
 *
 * Charging the bag alone was tried first and it warps the whole game — the bag
 * size becomes a hard price ceiling, so anything expensive (Steal above all)
 * can only be bought by standing around with a fat unbanked bag, which is
 * exactly what rivals punch out of you. Letting the vault pay keeps every
 * upgrade reachable and makes the cost honest: it comes straight off your
 * final score.
 */
export function funds(player) {
  return player.bag + (player.home ? player.home.vault : 0);
}

/** Can this player afford + legally buy the item right now? */
export function canBuy(player, id) {
  if (isMaxed(player, id)) return false;
  return funds(player) >= priceOf(player, id);
}

/**
 * Applies a purchase. Returns the price paid, or 0 when the buy was rejected.
 * Carried gold is spent first, then the vault covers the remainder.
 */
export function buy(player, id) {
  if (!canBuy(player, id)) return 0;
  const price = priceOf(player, id);
  const fromBag = Math.min(player.bag, price);
  player.bag -= fromBag;
  const fromVault = price - fromBag;
  if (fromVault > 0) player.home.vault -= fromVault;
  player.spent += price;
  player.spentFromVault = (player.spentFromVault || 0) + fromVault;

  switch (id) {
    case 'scaleUp':
      player.scaleLevel = Math.min(ITEM_BY_ID.scaleUp.max, player.scaleLevel + 1);
      break;
    case 'scaleDown':
      player.scaleLevel = Math.max(-ITEM_BY_ID.scaleDown.max, player.scaleLevel - 1);
      break;
    case 'steal':
      player.upgrades.steal = 1;
      player.canSteal = true;
      break;
    default:
      player.upgrades[id] = (player.upgrades[id] || 0) + 1;
      break;
  }
  player.purchases.push(id);
  return price;
}

/** Ordered list for the shop UI, with per-player state baked in. */
export function shopRows(player) {
  return ITEMS.map((def) => ({
    def,
    id: def.id,
    level: levelOf(player, def.id),
    price: priceOf(player, def.id),
    maxed: isMaxed(player, def.id),
    affordable: canBuy(player, def.id),
    needsVault: player.bag < priceOf(player, def.id),
  }));
}
