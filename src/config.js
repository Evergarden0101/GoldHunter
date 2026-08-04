/**
 * Central tuning file. Every gameplay number lives here so balance passes never
 * require touching simulation code.
 *
 * Units: metres (m), seconds (s), gold (g). The renderer converts m -> px.
 */

export const MATCH = {
  duration: 150,           // 2.5 minute match
  countdown: 3,            // "3 - 2 - 1 - GO" before the clock starts
  rushAt: 25,              // seconds remaining when the RUSH phase begins
  rushPopperMultiplier: 2.5,
  rushBurst: 60,           // one-off gold injected into the centre popper at rush
};

export const ARENA = {
  half: 35,                // arena spans -35m .. +35m on both axes
  wallBounce: 0.25,
  campRadius: 25,          // base camps sit 25m from the centre popper
  cornerCut: 9,            // arena corners are chamfered by this much
};

export const PLAYER = {
  radius: 1.2,             // characters read clearly at whole-arena zoom
  speed: 6.2,              // m/s at full tilt
  accel: 46,               // m/s^2
  friction: 12,
  bagCapacity: 40,
  turnRate: 14,            // rad/s for the facing indicator
  stunTime: 0.42,
  invulnAfterHit: 0.55,
  knockbackDecay: 5.5,
  dashSpeed: 15.5,
  dashTime: 0.16,
  dashCooldown: 2.4,
  respawnGrace: 1.0,
};

export const COMBAT = {
  punchWindup: 0.06,
  punchActive: 0.09,
  punchRecover: 0.2,
  punchCooldown: 0.28,
  punchReach: 1.55,        // added to the player's radius
  punchArc: Math.PI * 0.62,

  chargeMinHold: 0.18,     // hold longer than this and the punch becomes charged
  chargeFull: 1.15,        // hold time for maximum power
  chargeMoveSlow: 0.42,    // movement multiplier while charging
  chargeReachBonus: 0.9,   // extra reach at full charge
  chargeCooldown: 0.5,

  // Fraction of the victim's bag that a connecting punch rips loose.
  lightSteal: 0.35,
  chargedStealMin: 0.45,
  chargedStealMax: 0.8,
  attackerShare: 0.75,     // rest of the loot scatters on the floor
  minSteal: 4,             // a punch on an almost-empty bag still shakes coins out

  knockbackLight: 9.0,
  knockbackChargedMin: 13,
  knockbackChargedMax: 24,
  stunChargedBonus: 0.3,

  hitstopLight: 0.055,
  hitstopChargedMin: 0.09,
  hitstopChargedMax: 0.19,
  shakeLight: 0.28,
  shakeCharged: 0.75,
};

export const POPPERS = {
  big: {
    id: 'big',
    radius: 2.5,
    start: 50,
    ratePerMin: 200,
    cap: 320,
    harvestRate: 34,       // gold/s pulled into a bag while standing in range
    reach: 3.9,
  },
  small: {
    id: 'small',
    radius: 1.7,
    start: 20,
    ratePerMin: 80,
    cap: 160,
    harvestRate: 26,
    reach: 3.1,
  },
};

export const CAMP = {
  radius: 3.2,
  depositRate: 95,         // gold/s moved from bag to vault
  stealCooldown: 4.5,      // per (thief, camp) pair
  stealFraction: 0.25,     // of the vault
  stealCap: 70,
  stealMin: 10,
};

export const SHOP = {
  radius: 3.0,
  browseRange: 4.6,
  buyHold: 0.45,           // hold the action key this long to confirm a purchase
  cycleCooldown: 0.12,
};

/**
 * Shop catalogue. `cost(level)` is the price of the NEXT level.
 *
 * Purchases are paid out of the gold you are *carrying*, so the bag capacity
 * is also the price ceiling: every tier-1 upgrade fits inside the starting 40g
 * bag, while deeper levels and Steal only become reachable once Gold Bag Up
 * has widened the wallet. That makes bag size the spine of the tech tree.
 */
export const ITEMS = [
  {
    id: 'attackUp',
    name: 'Attack Up',
    blurb: 'Punches rip +22% more gold and hit harder.',
    icon: 'fist',
    max: 4,
    cost: (lvl) => 28 + lvl * 24,      // 28 / 52 / 76 / 100
  },
  {
    id: 'defenseUp',
    name: 'Defense Up',
    blurb: 'Lose 18% less gold per hit, shrug off knockback.',
    icon: 'shield',
    max: 4,
    cost: (lvl) => 28 + lvl * 24,
  },
  {
    id: 'goldBagUp',
    name: 'Gold Bag Up',
    blurb: '+25 carry capacity — and a bigger shopping budget.',
    icon: 'bag',
    max: 4,
    cost: (lvl) => 30 + lvl * 26,      // 30 / 56 / 82 / 108
  },
  {
    id: 'baseCampUp',
    name: 'Base Camp Up',
    blurb: 'Vault armour, faster deposits, +4% end bonus.',
    icon: 'camp',
    max: 3,
    cost: (lvl) => 36 + lvl * 34,      // 36 / 70 / 104
  },
  {
    id: 'scaleUp',
    name: 'Scale Up',
    blurb: 'Bigger: more reach, more knockback, slower.',
    icon: 'up',
    max: 3,
    cost: () => 34,
  },
  {
    id: 'scaleDown',
    name: 'Scale Down',
    blurb: 'Smaller: faster, harder to hit, weaker punch.',
    icon: 'down',
    max: 3,
    cost: () => 34,
  },
  {
    id: 'steal',
    name: 'Steal',
    blurb: 'Punch enemy base camps to rob their vault. Needs a bigger bag first.',
    icon: 'steal',
    max: 1,
    cost: () => 52,
  },
];

export const UPGRADE = {
  attackPerLevel: 0.22,
  defensePerLevel: 0.18,
  bagPerLevel: 25,
  campArmorPerLevel: 0.3,
  campDepositPerLevel: 0.35,
  campEndBonusPerLevel: 0.04,
  scaleStep: 0.16,          // size delta per scale level
  scaleSpeedPerLevel: -0.1, // bigger = slower
  scalePowerPerLevel: 0.18, // bigger = stronger
  scaleReachPerLevel: 0.22,
};

export const PICKUP = {
  radius: 0.42,
  magnetRange: 2.2,
  magnetSpeed: 13,
  life: 22,
  scatterSpeed: [3.5, 8.5],
  drag: 3.4,
  clumpSize: 12,           // gold per dropped coin blob
  autoPickupDelay: 0.35,
};

/** Personalities for the AI. Every "will" is 0..1 and blends into the utility scores. */
export const NPC_PROFILES = [
  {
    id: 'bruiser', name: 'Bruno', tag: 'Bruiser',
    attackWill: 0.9, saveGoldWill: 0.42, stealWill: 0.45, shopWill: 0.55,
    greed: 0.75, caution: 0.2,
    shopBias: { attackUp: 1.6, scaleUp: 1.3, defenseUp: 0.7, goldBagUp: 0.5, baseCampUp: 0.3, steal: 1.1, scaleDown: 0.1 },
  },
  {
    id: 'banker', name: 'Coinsworth', tag: 'Banker',
    attackWill: 0.2, saveGoldWill: 0.92, stealWill: 0.15, shopWill: 0.7,
    greed: 0.5, caution: 0.85,
    shopBias: { goldBagUp: 1.7, baseCampUp: 1.5, defenseUp: 1.1, scaleDown: 0.8, attackUp: 0.3, scaleUp: 0.2, steal: 0.2 },
  },
  {
    id: 'thief', name: 'Sly', tag: 'Thief',
    attackWill: 0.55, saveGoldWill: 0.45, stealWill: 0.95, shopWill: 0.8,
    greed: 0.9, caution: 0.5,
    shopBias: { steal: 2.2, scaleDown: 1.2, attackUp: 0.9, goldBagUp: 0.7, defenseUp: 0.5, baseCampUp: 0.4, scaleUp: 0.2 },
  },
  {
    id: 'allround', name: 'Pip', tag: 'All-round',
    attackWill: 0.55, saveGoldWill: 0.6, stealWill: 0.5, shopWill: 0.6,
    greed: 0.6, caution: 0.5,
    shopBias: { attackUp: 1.0, defenseUp: 1.0, goldBagUp: 1.0, baseCampUp: 0.9, steal: 1.25, scaleUp: 0.6, scaleDown: 0.6 },
  },
];

/** Difficulty knobs multiply on top of a profile. */
export const DIFFICULTY = {
  easy:   { react: 0.42, aim: 0.62, chargeSkill: 0.35, speed: 0.9,  awareness: 16, label: 'Easy' },
  normal: { react: 0.24, aim: 0.82, chargeSkill: 0.65, speed: 1.0,  awareness: 24, label: 'Normal' },
  hard:   { react: 0.12, aim: 0.94, chargeSkill: 0.9,  speed: 1.05, awareness: 34, label: 'Hard' },
};

export const COLORS = {
  players: ['#ff5d5d', '#4ea6ff', '#ffd23f', '#5ce68b'],
  playersDark: ['#8f2626', '#1c548f', '#8f6c11', '#227a44'],
  gold: '#ffc939',
  goldDark: '#a8760d',
  floor: '#171b26',
  floor2: '#1d2231',
  grid: '#252c3d',
  wall: '#2e3750',
  ink: '#0b0d14',
  text: '#e8ecf7',
};

export const NAV = {
  cell: 1.4,               // A* grid resolution in metres
  agentClearance: 1.0,     // obstacles are inflated by this before pathing
  repathInterval: 0.55,
  waypointReach: 1.1,
};
