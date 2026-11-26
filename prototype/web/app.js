/*
 * HR//0D -- browser interaction twin.
 * Renders the shipped desk sprites using the same desk_layout.json as Unity and drives the
 * playable minute through rules.js (a mirror of the C# interaction layer). No backend needed.
 */
import { REASONS, TELLS, Session, HotspotHitTest, buildFlags, paperworkDelta } from './rules.js';

const $ = (id) => document.getElementById(id);
const url = (p, base = import.meta.url) => new URL(p, base).href;
const root = '../../';
const params = new URLSearchParams(location.search);
const seed = Number(params.get('seed')) || 1000;

const state = {
  copy: null, layout: null, manifest: null, byPath: new Map(), images: new Map(),
  run: null, keys: null, session: null, tab: 'resume', examining: false,
  reduceMotion: false, frame: 0, lastFrame: performance.now(), placed: 0, phase: null,
  flags: [], flagged: new Set(), letterText: '',
};

const desk = $('desk');
const ctx = desk.getContext('2d');
const mag = $('magnifier');
const mctx = mag.getContext('2d');

async function load() {
  const [copy, layout, manifest, run, keys] = await Promise.all([
    fetch(url('copy.json')).then(r => r.json()),
    fetch(url(root + 'unity/Assets/Content/desk_layout.json')).then(r => r.json()),
    fetch(url(root + 'art/kit/unity_sprites.json')).then(r => r.json()),
    fetch(url(`fixtures/run-${seed}.json`)).then(r => r.json()),
    fetch(url(`fixtures/run-${seed}.key.json`)).then(r => r.json()),
  ]);
  state.copy = copy;
  state.layout = layout;
  state.manifest = manifest;
  state.run = run;
  state.keys = keys;
  state.byPath = new Map(manifest.sprites.map(s => [s.source, s]));
  state.session = new Session(run, keys, copy);

  const sources = new Set(manifest.sprites.map(s => s.source));
  await Promise.all([...sources].map(source => new Promise(resolve => {
    const img = new Image();
    img.onload = img.onerror = () => { state.images.set(source, img); resolve(); };
    img.src = url(root + source);
  })));

  buildHotspots();
  buildReasonSelect();
  buildTools();
  bindUI();
  refreshApplicant();
  renderStatus();
  requestAnimationFrame(loop);
}

function buildHotspots() {
  const layer = $('hotspotLayer');
  layer.innerHTML = '';
  const app = state.layout.layers.find(l => l.id === 'applicant');
  const scale = app.scaleFactor > 0 ? app.scaleFactor : 1;   // composition L: 0.96
  for (const hp of state.layout.hotspots) {
    const el = document.createElement('div');
    el.className = 'hotspot';
    el.dataset.artifact = hp.artifact;
    el.title = hp.label;
    const [x, y, w, h] = hp.rect;
    el.style.left = `${(app.anchor[0] + x * scale) / 480 * 100}%`;
    el.style.top = `${(270 - (app.anchor[1] + (y + h) * scale)) / 270 * 100}%`;
    el.style.width = `${w * scale / 480 * 100}%`;
    el.style.height = `${h * scale / 270 * 100}%`;
    el.innerHTML = `<span>${hp.label}</span>`;
    el.addEventListener('click', (e) => { e.stopPropagation(); inspect(hp.artifact); });
    layer.appendChild(el);
  }
}

function buildReasonSelect() {
  const select = $('reasonSelect');
  select.innerHTML = '';
  for (const reason of REASONS) {
    const option = document.createElement('option');
    option.value = reason;
    option.textContent = state.copy.reasonLabels[reason];
    select.appendChild(option);
  }
  select.value = 'INSUFFICIENT';
}

function buildTools() {
  const row = $('toolRow');
  row.innerHTML = '';
  for (const tool of ['id_registry', 'metadata_scan', 'reference_call']) {
    const button = document.createElement('button');
    button.className = 'tool';
    button.type = 'button';
    button.dataset.tool = tool;
    button.innerHTML = `${state.copy.toolLabels[tool]}<span class="pips"></span>`;
    button.addEventListener('click', () => useTool(tool));
    row.appendChild(button);
  }
}

function bindUI() {
  document.querySelectorAll('.tab').forEach(tab => tab.addEventListener('click', () => {
    state.tab = tab.dataset.tab;
    document.querySelectorAll('.tab').forEach(t => t.classList.toggle('active', t === tab));
    renderSheet();
  }));
  $('approveBtn').addEventListener('click', () => commit('APPROVE'));
  $('rejectBtn').addEventListener('click', () => commit('REJECT'));
  $('inspectBtn').addEventListener('click', () => {
    state.examining = !state.examining;
    $('hotspotLayer').style.display = state.examining ? 'block' : 'none';
    $('inspectBtn').textContent = state.examining ? 'STOP EXAMINING' : 'EXAMINE';
    if (!state.examining) hideEvidence();
  });
  $('evidenceClose').addEventListener('click', hideEvidence);
  $('modalAction').addEventListener('click', advance);
  $('motionToggle').addEventListener('change', (e) => {
    state.reduceMotion = e.target.checked;
    if (state.reduceMotion) state.frame = 0;
  });
  $('hint').textContent = state.copy.hint;
  $('hotspotLayer').style.display = 'none';
}

function refreshApplicant() {
  const a = state.session.current;
  if (!a) return;
  $('candidateName').textContent = a.name;
  $('seedLabel').textContent = String(seed);
  state.placed = 0;
  state.toolLog = [];
  state.flags = [];
  state.flagged = new Set();
  state.letterText = '';
  hideEvidence();
  renderPortrait(a);
  renderSheet();
  renderNotes();
  renderStatus();
  renderTools();
}

function renderPortrait(a) {
  const source = `art/kit/portraits/portrait_${a.sprite}_96x96.png`;
  const img = state.images.get(source);
  const c = $('portrait');
  if (!img) return;
  c.getContext('2d').drawImage(img, 0, 0, 96, 96, 0, 0, 48, 48);
}

function renderSheet() {
  const a = state.session.current;
  const sheet = $('sheet');
  if (!a) return;
  if (state.tab === 'resume') {
    const rows = [
      ['NAME', a.name], ['AGE', String(a.age)], ['ID NUMBER', a.idNo],
      ['UNIVERSITY', a.university], ['GRADUATED', String(a.gradYear)],
      ['EMPLOYER', a.employer], ['EMPLOYER FOUNDED', String(a.employerFounded)],
      ['STARTED', String(a.startYear)], ['EXPERIENCE (YRS)', String(a.expYears)],
      ['REFERRAL', a.referral], ['ROLE', a.role],
    ];
    sheet.innerHTML = rows.map(([k, v]) => `<div class="row"><span>${k}</span><b>${esc(v)}</b></div>`).join('');
  } else if (state.tab === 'letter') {
    const kind = a.sprite.startsWith('synthetic') ? 'flat' : a.sprite === 'human_bait_a' ? 'bait' : 'plain';
    const signature = state.copy.letterSignature.replace('{name}', a.name);
    const text = state.copy.letters[kind]
      .replace('{role}', a.role).replace('{employer}', a.employer)
      .replace('{startYear}', a.startYear).replace('{expYears}', a.expYears) + '\n' + signature;
    state.letterText = text;
    state.flags = buildFlags(text, a.referral);
    const rows = state.flags.map(f =>
      `<button type="button" class="flag ${state.flagged.has(f.id) ? 'on' : ''}" data-flag="${f.id}">` +
      `[${state.flagged.has(f.id) ? 'X' : ' '}] ${esc(state.copy.flagLabels[f.id])}</button>`).join('');
    sheet.innerHTML = `<p>${esc(text).replace(/\n/g, '<br>')}</p><div class="flags">${rows}</div>`;
    sheet.querySelectorAll('.flag').forEach(button => button.addEventListener('click', () => {
      const id = button.dataset.flag;
      if (state.flagged.has(id)) state.flagged.delete(id); else state.flagged.add(id);
      renderSheet();
    }));
  } else {
    sheet.innerHTML = `<p>Three checks per shift. A check that finds nothing reports VALID - that result does not prove a person is genuine.</p><div id="toolLog"></div>`;
    renderToolLog();
  }
}

function renderTools() {
  const s = state.session;
  document.querySelectorAll('.tool').forEach(button => {
    const tool = button.dataset.tool;
    const max = { id_registry: 2, metadata_scan: 2, reference_call: 1 }[tool];
    const left = s.tools.day === s.day ? (s.tools.used[tool] || 0) : 0;
    button.disabled = left >= max;
    button.querySelector('.pips').textContent = ` ${'#'.repeat(Math.max(0, max - left))}`;
  });
}

function renderToolLog() {
  const log = $('toolLog');
  if (!log) return;
  const entries = state.toolLog || [];
  log.innerHTML = entries.map(e => `<div class="row"><span>${esc(e.label)}</span><b class="${e.ok ? '' : 'flag'}">${esc(e.text)}</b></div>`).join('');
}

function useTool(tool) {
  const s = state.session;
  if (!s.useTool(tool)) return;
  const result = s.tool(tool);
  state.toolLog = state.toolLog || [];
  state.toolLog.unshift({ label: state.copy.toolLabels[tool], ok: result.ok, text: result.wording });
  if (!result.ok) s.ledger.reveal(toolTell(result.wording), s.day);
  renderTools();
  renderNotes();
  if (state.tab === 'tools') renderToolLog();
  showCallout(state.copy.toolLabels[tool], result.wording);
}

function toolTell(wording) {
  for (const [id, t] of Object.entries(TELLS)) if (t.wording === wording) return id;
  return null;
}

function inspect(artifact) {
  const result = state.session.inspect(artifact);
  if (result.requiresMoreTime) {
    showCallout(labelFor(artifact), state.copy.watchLonger);
    return;
  }
  if (result.style === 'magnifier') showMagnifier(artifact, result);
  else showCallout(labelFor(artifact), result.wording || state.copy.observationOnly);
  renderNotes();
}

function labelFor(artifact) {
  const hit = state.layout.hotspots.find(h => h.artifact === artifact);
  return hit ? hit.label : artifact;
}

function showMagnifier(artifact, result) {
  const hp = state.layout.hotspots.find(h => h.artifact === artifact);
  const app = state.layout.layers.find(l => l.id === 'applicant');
  const scale = app.scaleFactor > 0 ? app.scaleFactor : 1;
  const scaled = hp.rect.map(v => v * scale);
  const region = HotspotHitTest.localToCanvas(scaled, app.anchor[0], app.anchor[1]);
  mag.width = region.w * 4;
  mag.height = region.h * 4;
  mctx.imageSmoothingEnabled = false;
  mctx.clearRect(0, 0, mag.width, mag.height);
  mctx.drawImage(desk, region.x, 270 - region.y - region.h, region.w, region.h,
                 0, 0, mag.width, mag.height);
  $('evidenceTag').textContent = 'MAGNIFIER 2x';
  $('evidenceText').textContent = result.wording || state.copy.observationOnly;
  mag.hidden = false;
  $('evidenceBox').hidden = false;
}

function showCallout(label, wording) {
  $('evidenceTag').textContent = 'OBSERVATION';
  mag.hidden = true;
  $('evidenceText').textContent = `${label}: ${wording}`;
  $('evidenceBox').hidden = false;
}

function hideEvidence() {
  $('evidenceBox').hidden = true;
  mag.hidden = false;
}

function renderNotes() {
  const notes = state.session.ledger.notes;
  $('noteCount').textContent = String(notes.length);
  $('notes').innerHTML = notes.length
    ? notes.map(n => `<div class="note"><b>${esc(n.category)}</b>${esc(n.wording)}</div>`).join('')
    : `<p class="empty">${esc(state.copy.manualEmpty)}</p>`;
}

function renderStatus() {
  const s = state.session;
  $('dayLabel').textContent = `${s.day} / 5`;
  $('trustLabel').textContent = String(s.trust);
  $('trustFill').style.width = `${s.trust}%`;
  $('trustFill').style.background = s.trust > 55 ? '#18e0b0' : s.trust > 25 ? '#ffd166' : '#ff4757';
  $('strikeLabel').textContent = `${s.syntheticsHired} / 3`;
}

function commit(decision) {
  if (state.phase || state.session.finished || state.session.dayFinished) return;
  const reason = $('reasonSelect').value;
  const letterDelta = paperworkDelta(state.flags, state.flagged);
  const result = state.session.stamp(decision, reason, state.placed, letterDelta);
  if (!result) return;
  state.examining = false;
  $('hotspotLayer').style.display = 'none';
  $('inspectBtn').textContent = 'EXAMINE';
  hideEvidence();
  state.phase = 'debrief';
  showModal('DECISION LOGGED', result.debriefLine,
            `TRUST ${signed(result.trustDelta)}  |  FOUND ${state.session.ledger.foundCount}  |  ` +
            `MISSED ${result.missedEvidence.length}  |  FLAGS ${signed(letterDelta)}`,
            state.session.dayFinished ? state.copy.closeShift : state.copy.nextApplicant);
}

function advance() {
  if (state.phase === 'debrief') {
    if (state.session.dayFinished) {
      const day = state.session.closeDay();
      renderStatus();
      if (state.session.finished) {
        state.phase = 'end';
        showModal(state.copy.runEnded, state.copy.endings[state.session.outcome] || state.copy.endings.Survived,
                  `TRUST ${state.session.trust}  |  SYNTHETICS HIRED ${state.session.syntheticsHired}`, state.copy.continue);
      } else {
        state.phase = 'day';
        const body = state.copy.daySummaryBody
          .replace('{approved}', day.approved).replace('{rejected}', day.rejected)
          .replace('{wrongful}', day.wrongful).replace('{caught}', day.syntheticsCaught);
        const hint = day.quotaMet ? state.copy.quotaHintOn : state.copy.quotaHintOff;
        showModal(state.copy.daySummary, `${body}. ${hint}`, `TRUST ${day.trustAfter}`, state.copy.continue);
      }
      return;
    }
    state.session.beginCurrent();
    state.phase = null;
    state.toolLog = [];
    $('modal').hidden = true;
    refreshApplicant();
    return;
  }
  if (state.phase === 'day' || state.phase === 'end') {
    state.phase = null;
    state.toolLog = [];
    $('modal').hidden = true;
    if (state.session.finished) location.reload();
    else refreshApplicant();
  }
}

function showModal(kicker, body, meta, action) {
  $('modalKicker').textContent = kicker;
  $('modalTitle').textContent = kicker;
  $('modalBody').textContent = body;
  $('modalMeta').textContent = meta;
  $('modalAction').textContent = action;
  $('modal').hidden = false;
}

function signed(n) { return n >= 0 ? `+${n}` : String(n); }
function esc(s) { return String(s).replace(/[&<>"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c])); }

function loop(now) {
  const dt = (now - state.lastFrame) / 1000;
  state.lastFrame = now;
  if (!state.reduceMotion) {
    state.session.tick(dt);
    state.placed += dt;
    if (now - frameAt > 420) { frameAt = now; state.frame = (state.frame + 1) % 2; drawScene(); }
  }
  requestAnimationFrame(loop);
}
let frameAt = performance.now();

function drawScene() {
  const s = state.session;
  if (!s) return;
  const { canvas, layers } = state.layout;
  // Composition L: the room gets darker as shifts pass; corruption is a separate wear overlay.
  const room = s.day >= 3 ? 2 : s.day === 2 ? 1 : 0;
  ctx.clearRect(0, 0, canvas.width, canvas.height);
  for (const layer of [...layers].sort((a, b) => a.sortingOrder - b.sortingOrder)) {
    let source = layer.sprite;
    if (layer.id === 'background') source = `art/kit/backgrounds/office_L${room}.png`;
    if (layer.id === 'applicant' && s.current)
      source = `art/kit/applicants/app_${s.current.sprite}_192x216.png`;
    const entry = state.byPath.get(source);
    const img = state.images.get(source);
    if (!entry || !img) continue;
    let sx = 0, sy = 0, w = entry.width, h = entry.height;
    if (entry.spriteMode === 'Multiple' && entry.sprites && entry.sprites.length) {
      const frame = entry.sprites[Math.min(state.frame, entry.sprites.length - 1)];
      [sx, sy, w, h] = frame.rect;
    }
    const scale = layer.scaleFactor > 0 ? layer.scaleFactor : 1;
    const dw = Math.round(w * scale), dh = Math.round(h * scale);
    const y = canvas.height - layer.anchor[1] - dh;
    ctx.drawImage(img, sx, sy, w, h, layer.anchor[0], y, dw, dh);

    if (layer.id === 'background' && s.corruption > 0) {
      const wear = s.corruption >= 2 ? ['wear_a', 'wear_b'] : ['wear_a'];
      for (const name of wear) {
        const p = `art/kit/fx/${name}.png`;
        const overlay = state.images.get(p);
        if (overlay) ctx.drawImage(overlay, 0, 0, canvas.width, canvas.height);
      }
    }
  }
}

load().catch(err => {
  $('candidateName').textContent = 'LOAD FAILED';
  $('sheet').innerHTML = `<p>${esc(err.message)}</p>`;
  console.error(err);
});
