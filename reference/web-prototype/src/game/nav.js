/**
 * Navigation for the AI.
 *
 * The arena is an open octagon dotted with circular blockers (rocks, shops,
 * poppers). Pure steering gets stuck on those, so bots path properly:
 *
 *   1. `NavGrid` rasterises the static blockers into a coarse grid, inflated by
 *      the agent radius so a path never clips a corner.
 *   2. A* (8-way, no corner cutting) finds a cell route.
 *   3. The route is string-pulled with line-of-sight tests so bots run in
 *      straight diagonals instead of following the grid staircase.
 *   4. `PathFollower` walks the waypoints and adds local avoidance for the
 *      dynamic stuff (other players), plus a repath timer.
 */

import { NAV } from '../config.js';
import { dist, segmentHitsCircle, clamp } from '../core/math.js';

class MinHeap {
  constructor() { this.a = []; }
  get size() { return this.a.length; }
  push(node) {
    const a = this.a;
    a.push(node);
    let i = a.length - 1;
    while (i > 0) {
      const p = (i - 1) >> 1;
      if (a[p].f <= a[i].f) break;
      [a[p], a[i]] = [a[i], a[p]];
      i = p;
    }
  }
  pop() {
    const a = this.a;
    const top = a[0];
    const last = a.pop();
    if (a.length) {
      a[0] = last;
      let i = 0;
      for (;;) {
        const l = i * 2 + 1;
        const r = l + 1;
        let m = i;
        if (l < a.length && a[l].f < a[m].f) m = l;
        if (r < a.length && a[r].f < a[m].f) m = r;
        if (m === i) break;
        [a[m], a[i]] = [a[i], a[m]];
        i = m;
      }
    }
    return top;
  }
}

export class NavGrid {
  /**
   * @param {object} bounds  { half, cornerCut } octagon description
   * @param {Array}  blockers [{x,y,r}]
   */
  constructor(bounds, blockers) {
    this.bounds = bounds;
    this.blockers = blockers;
    this.cell = NAV.cell;
    this.min = -bounds.half;
    this.cols = Math.ceil((bounds.half * 2) / this.cell);
    this.rows = this.cols;
    this.blocked = new Uint8Array(this.cols * this.rows);
    this.rebuild();
  }

  rebuild() {
    const c = this.cell;
    const clearance = NAV.agentClearance;
    for (let gy = 0; gy < this.rows; gy++) {
      for (let gx = 0; gx < this.cols; gx++) {
        const x = this.min + (gx + 0.5) * c;
        const y = this.min + (gy + 0.5) * c;
        let bad = !this.insideArena(x, y, clearance);
        if (!bad) {
          for (const b of this.blockers) {
            const rr = b.r + clearance;
            if (dist(x, y, b.x, b.y) < rr) { bad = true; break; }
          }
        }
        this.blocked[gy * this.cols + gx] = bad ? 1 : 0;
      }
    }
  }

  insideArena(x, y, margin = 0) {
    const h = this.bounds.half - margin;
    if (x < -h || x > h || y < -h || y > h) return false;
    const diag = this.bounds.half * 2 - this.bounds.cornerCut - margin * 1.42;
    return Math.abs(x) + Math.abs(y) <= diag;
  }

  gx(x) { return clamp(Math.floor((x - this.min) / this.cell), 0, this.cols - 1); }
  gy(y) { return clamp(Math.floor((y - this.min) / this.cell), 0, this.rows - 1); }
  wx(gx) { return this.min + (gx + 0.5) * this.cell; }
  wy(gy) { return this.min + (gy + 0.5) * this.cell; }
  isBlocked(gx, gy) {
    if (gx < 0 || gy < 0 || gx >= this.cols || gy >= this.rows) return true;
    return this.blocked[gy * this.cols + gx] === 1;
  }

  /** Nearest walkable cell, spiralling outwards. Used when a goal sits inside a blocker. */
  nearestFree(gx, gy, maxRadius = 14) {
    if (!this.isBlocked(gx, gy)) return [gx, gy];
    for (let r = 1; r <= maxRadius; r++) {
      for (let dy = -r; dy <= r; dy++) {
        for (let dx = -r; dx <= r; dx++) {
          if (Math.max(Math.abs(dx), Math.abs(dy)) !== r) continue;
          if (!this.isBlocked(gx + dx, gy + dy)) return [gx + dx, gy + dy];
        }
      }
    }
    return null;
  }

  /** True when a straight run from a->b touches nothing static. */
  lineOfSight(ax, ay, bx, by, pad = NAV.agentClearance * 0.85) {
    for (const b of this.blockers) {
      if (segmentHitsCircle(ax, ay, bx, by, b.x, b.y, b.r + pad)) return false;
    }
    const d = dist(ax, ay, bx, by);
    const steps = Math.max(2, Math.ceil(d / 0.6));
    for (let i = 0; i <= steps; i++) {
      const t = i / steps;
      if (!this.insideArena(ax + (bx - ax) * t, ay + (by - ay) * t, pad * 0.6)) return false;
    }
    return true;
  }

  /**
   * A* + string pulling. Returns an array of world-space waypoints (excluding
   * the start), or null when unreachable.
   */
  findPath(sx, sy, tx, ty) {
    if (this.lineOfSight(sx, sy, tx, ty)) return [{ x: tx, y: ty }];

    const start = this.nearestFree(this.gx(sx), this.gy(sy));
    const goal = this.nearestFree(this.gx(tx), this.gy(ty));
    if (!start || !goal) return null;

    const cols = this.cols;
    const total = cols * this.rows;
    const sIdx = start[1] * cols + start[0];
    const gIdx = goal[1] * cols + goal[0];
    if (sIdx === gIdx) return [{ x: tx, y: ty }];

    const gScore = new Float32Array(total).fill(Infinity);
    const cameFrom = new Int32Array(total).fill(-1);
    const closed = new Uint8Array(total);
    const open = new MinHeap();

    const h = (i) => {
      const ax = i % cols, ay = (i / cols) | 0;
      const bx = gIdx % cols, by = (gIdx / cols) | 0;
      const dx = Math.abs(ax - bx), dy = Math.abs(ay - by);
      return (dx + dy) + (Math.SQRT2 - 2) * Math.min(dx, dy);
    };

    gScore[sIdx] = 0;
    open.push({ i: sIdx, f: h(sIdx) });

    const NB = [
      [1, 0, 1], [-1, 0, 1], [0, 1, 1], [0, -1, 1],
      [1, 1, Math.SQRT2], [1, -1, Math.SQRT2], [-1, 1, Math.SQRT2], [-1, -1, Math.SQRT2],
    ];

    let found = false;
    let guard = 0;
    while (open.size && guard++ < 20000) {
      const cur = open.pop();
      if (closed[cur.i]) continue;
      closed[cur.i] = 1;
      if (cur.i === gIdx) { found = true; break; }

      const cx = cur.i % cols;
      const cy = (cur.i / cols) | 0;
      for (const [dx, dy, cost] of NB) {
        const nx = cx + dx, ny = cy + dy;
        if (this.isBlocked(nx, ny)) continue;
        // No squeezing diagonally between two blockers.
        if (dx && dy && (this.isBlocked(cx + dx, cy) || this.isBlocked(cx, cy + dy))) continue;
        const ni = ny * cols + nx;
        if (closed[ni]) continue;
        const ng = gScore[cur.i] + cost;
        if (ng < gScore[ni]) {
          gScore[ni] = ng;
          cameFrom[ni] = cur.i;
          open.push({ i: ni, f: ng + h(ni) });
        }
      }
    }
    if (!found) return null;

    // Reconstruct, then string-pull.
    const raw = [];
    let i = gIdx;
    while (i !== -1) {
      raw.push({ x: this.wx(i % cols), y: this.wy((i / cols) | 0) });
      if (i === sIdx) break;
      i = cameFrom[i];
    }
    raw.reverse();
    raw[0] = { x: sx, y: sy };
    raw.push({ x: tx, y: ty });

    const out = [];
    let anchor = 0;
    for (let k = 2; k < raw.length; k++) {
      if (!this.lineOfSight(raw[anchor].x, raw[anchor].y, raw[k].x, raw[k].y)) {
        out.push(raw[k - 1]);
        anchor = k - 1;
      }
    }
    out.push(raw[raw.length - 1]);
    return out;
  }
}

export class PathFollower {
  constructor(grid) {
    this.grid = grid;
    this.path = null;
    this.index = 0;
    this.goal = null;
    this.timer = 0;
    this.failed = false;
  }

  setGoal(x, y, force = false) {
    if (!force && this.goal && dist(this.goal.x, this.goal.y, x, y) < 1.2) return;
    this.goal = { x, y };
    this.timer = 0;
    this._repath = true;
  }

  clear() {
    this.path = null;
    this.goal = null;
    this.index = 0;
  }

  /**
   * @returns {[number, number]} unit-ish desired direction, or [0,0] when arrived.
   */
  steer(x, y, dt) {
    if (!this.goal) return [0, 0];
    this.timer -= dt;
    if (this._repath || this.timer <= 0 || !this.path) {
      this._repath = false;
      this.timer = NAV.repathInterval;
      this.path = this.grid.findPath(x, y, this.goal.x, this.goal.y);
      this.index = 0;
      this.failed = !this.path;
      if (!this.path) {
        // Unreachable: fall back to a straight bee-line so the bot still moves.
        const dx = this.goal.x - x, dy = this.goal.y - y;
        const l = Math.hypot(dx, dy) || 1;
        return [dx / l, dy / l];
      }
    }

    while (this.index < this.path.length - 1) {
      const w = this.path[this.index];
      if (dist(x, y, w.x, w.y) < NAV.waypointReach) this.index++;
      else break;
    }
    // Greedy skip: if a later waypoint is already visible, cut to it.
    for (let k = this.path.length - 1; k > this.index; k--) {
      if (this.grid.lineOfSight(x, y, this.path[k].x, this.path[k].y)) { this.index = k; break; }
    }

    const w = this.path[this.index];
    const dx = w.x - x, dy = w.y - y;
    const l = Math.hypot(dx, dy);
    if (l < 0.05) return [0, 0];
    return [dx / l, dy / l];
  }

  distanceToGoal(x, y) {
    if (!this.goal) return Infinity;
    if (!this.path) return dist(x, y, this.goal.x, this.goal.y);
    let total = dist(x, y, this.path[this.index].x, this.path[this.index].y);
    for (let k = this.index; k < this.path.length - 1; k++) {
      total += dist(this.path[k].x, this.path[k].y, this.path[k + 1].x, this.path[k + 1].y);
    }
    return total;
  }
}

/** Steer away from nearby dynamic agents so bots don't clump into a scrum. */
export function separation(self, others, radius = 2.4) {
  let sx = 0, sy = 0;
  for (const o of others) {
    if (o === self || !o.alive) continue;
    const dx = self.x - o.x;
    const dy = self.y - o.y;
    const d = Math.hypot(dx, dy);
    if (d > 1e-4 && d < radius) {
      const w = (radius - d) / radius;
      sx += (dx / d) * w;
      sy += (dy / d) * w;
    }
  }
  return [sx, sy];
}
