#!/usr/bin/env node
/**
 * Zero-dependency bundler.
 *
 * Walks the ES module graph from src/main.js, wraps each module in a tiny
 * CommonJS-style registry (so top-level names in different modules can't
 * collide), inlines styles.css and emits:
 *
 *   dist/goldhunter.html  — standalone page, open it with a double click
 *   dist/artifact.html    — same page as a body fragment for Claude Artifacts
 *
 * Usage: node tools/build.js
 */

const fs = require('fs');
const path = require('path');

const ROOT = path.resolve(__dirname, '..');
const ENTRY = 'src/main.js';

/* ------------------------------------------------------------ module graph */

const modules = new Map();

function resolve(fromId, spec) {
  if (!spec.startsWith('.')) throw new Error(`Bare import "${spec}" in ${fromId} — not supported`);
  const p = path.normalize(path.join(path.dirname(fromId), spec)).replace(/\\/g, '/');
  if (!fs.existsSync(path.join(ROOT, p))) throw new Error(`Cannot resolve "${spec}" from ${fromId}`);
  return p;
}

const IMPORT_RE = /^[ \t]*import\s+([\s\S]*?)\s+from\s+['"]([^'"]+)['"]\s*;?[ \t]*$/gm;
const SIDE_EFFECT_RE = /^[ \t]*import\s+['"]([^'"]+)['"]\s*;?[ \t]*$/gm;

/** Rewrites one module's source into a registry factory body. */
function transform(id, src) {
  const exported = new Set();
  let code = src;

  code = code.replace(SIDE_EFFECT_RE, (_m, spec) => `__require(${JSON.stringify(resolve(id, spec))});`);

  code = code.replace(IMPORT_RE, (_m, clause, spec) => {
    const dep = JSON.stringify(resolve(id, spec));
    const c = clause.trim();
    if (c.startsWith('{')) {
      const inner = c.replace(/^\{|\}$/g, '')
        .split(',')
        .map((s) => s.trim())
        .filter(Boolean)
        .map((s) => {
          const m = s.match(/^(\S+)\s+as\s+(\S+)$/);
          return m ? `${m[1]}: ${m[2]}` : s;
        })
        .join(', ');
      return `const { ${inner} } = __require(${dep});`;
    }
    if (c.startsWith('*')) {
      const ns = c.replace(/^\*\s+as\s+/, '');
      return `const ${ns} = __require(${dep});`;
    }
    return `const ${c} = __require(${dep}).default;`;
  });

  // export class/function/const/let/var Foo
  code = code.replace(/^[ \t]*export\s+(default\s+)?(async\s+)?(class|function|const|let|var)\s+([A-Za-z0-9_$]+)/gm,
    (_m, def, asyncKw, kind, name) => {
      exported.add(name);
      return `${asyncKw || ''}${kind} ${name}`;
    });

  // export { a, b as c };
  code = code.replace(/^[ \t]*export\s*\{([^}]*)\}\s*;?[ \t]*$/gm, (_m, inner) => {
    inner.split(',').map((s) => s.trim()).filter(Boolean).forEach((s) => {
      const m = s.match(/^(\S+)\s+as\s+(\S+)$/);
      exported.add(m ? `${m[2]}: ${m[1]}` : s);
    });
    return '';
  });

  if (exported.size) {
    code += `\n__exports.__set({ ${[...exported].join(', ')} });\n`;
  }
  return code;
}

function load(id) {
  if (modules.has(id)) return;
  const src = fs.readFileSync(path.join(ROOT, id), 'utf8');
  modules.set(id, null); // reserve to break cycles
  const deps = [];
  for (const m of src.matchAll(IMPORT_RE)) deps.push(resolve(id, m[2]));
  for (const m of src.matchAll(SIDE_EFFECT_RE)) deps.push(resolve(id, m[1]));
  modules.set(id, transform(id, src));
  for (const d of deps) load(d);
}

load(ENTRY);

/* ---------------------------------------------------------------- emit js */

const runtime = `
(function () {
  "use strict";
  var __defs = {};
  var __cache = {};
  function __require(id) {
    if (__cache[id]) return __cache[id].e;
    var mod = { e: {} };
    mod.e.__set = function (obj) { for (var k in obj) mod.e[k] = obj[k]; };
    __cache[id] = mod;
    __defs[id](mod.e, __require);
    return mod.e;
  }
`;

let js = runtime;
for (const [id, code] of modules) {
  js += `\n__defs[${JSON.stringify(id)}] = function (__exports, __require) {\n${code}\n};\n`;
}
js += `\n  __require(${JSON.stringify(ENTRY)});\n})();\n`;

/* -------------------------------------------------------------- emit html */

const css = fs.readFileSync(path.join(ROOT, 'styles.css'), 'utf8');
const TITLE = 'GoldHunter — 4-player gold rush brawler';
const DESC = 'A 2.5 minute, 4-player arena game: mine gold, punch it out of your rivals, '
  + 'upgrade at the shops, and bank more than anyone else.';

const body = `<canvas id="game"></canvas>
<div id="overlay" class="overlay"></div>
<script>
${js}
</script>`;

const standalone = `<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1, viewport-fit=cover">
<title>${TITLE}</title>
<meta name="description" content="${DESC}">
<style>
${css}
</style>
</head>
<body>
${body}
</body>
</html>
`;

// Artifacts wrap the file in their own <!doctype>/<head>/<body>, so ship a fragment.
const fragment = `<title>${TITLE}</title>
<style>
${css}
</style>
${body}
`;

fs.mkdirSync(path.join(ROOT, 'dist'), { recursive: true });
fs.writeFileSync(path.join(ROOT, 'dist/goldhunter.html'), standalone);
fs.writeFileSync(path.join(ROOT, 'dist/artifact.html'), fragment);

const kb = (s) => `${(Buffer.byteLength(s) / 1024).toFixed(1)} kB`;
console.log(`bundled ${modules.size} modules`);
console.log(`  dist/goldhunter.html  ${kb(standalone)}`);
console.log(`  dist/artifact.html    ${kb(fragment)}`);
