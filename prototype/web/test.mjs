/*
 * Headless checks for the browser interaction twin. Run: node prototype/web/test.mjs
 * These are the same assertions the C# headless runner makes against the shipping code.
 */
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { EvidenceLedger, Session, ToolBudget, TELLS, isUnlocked, toolResult, buildFlags, paperworkDelta } from './rules.js';

const read = (p) => JSON.parse(readFileSync(new URL(p, import.meta.url), 'utf8'));
const run = read('fixtures/run-1000.json');
const keys = read('fixtures/run-1000.key.json');
const copy = read('copy.json');

let passed = 0;
function test(name, fn) {
  try { fn(); console.log('PASS  ' + name); passed++; }
  catch (e) { console.log('FAIL  ' + name + ': ' + e.message); process.exitCode = 1; }
}

test('fixture has no truth flag in the public record', () => {
  for (const day of Object.values(run.days))
    for (const a of day) {
      assert.ok(!('synthetic' in a), 'truth leaked into the public fixture');
      assert.ok(!('archetype' in a), 'archetype leaked into the public fixture');
    }
});

test('every synthetic is provable on its day (I1b)', () => {
  for (const [day, applicants] of Object.entries(run.days)) {
    for (let i = 0; i < applicants.length; i++) {
      const key = keys.days[day][i];
      if (!key.synthetic) continue;
      const reachable = applicants[i].tells.filter(id => isUnlocked(Number(day), TELLS[id].surface));
      assert.ok(reachable.length > 0, `day ${day} applicant ${i} has no reachable tell`);
    }
  }
});

test('honest humans never produce proven evidence (I5)', () => {
  for (const [day, applicants] of Object.entries(run.days))
    for (let i = 0; i < applicants.length; i++) {
      if (keys.days[day][i].archetype !== 'human_solid') continue;
      const ledger = new EvidenceLedger();
      ledger.begin(applicants[i]);
      for (const artifact of ['seam', 'mirror', 'ear', 'hands', 'straight_badge', 'shadow', 'no_blink'])
        assert.equal(ledger.inspect(artifact, Number(day), 9).proven, false);
      assert.equal(ledger.foundCount, 0);
    }
});

test('body tell opens a magnifier; face tell opens a callout', () => {
  const ledger = new EvidenceLedger();
  ledger.begin({ tells: ['portrait_blend_seam'] });
  const first = ledger.inspect('seam', 1);
  assert.equal(first.style, 'magnifier');
  assert.equal(first.proven, true);
  assert.equal(ledger.inspect('seam', 1).changed, false, 'repeat tap double counted');
  assert.equal(ledger.foundCount, 1);
  const face = new EvidenceLedger();
  face.begin({ tells: ['portrait_asymmetry'] });
  const faceResult = face.inspect('mirror', 1);
  assert.equal(faceResult.style, 'callout');
  assert.equal(faceResult.proven, true);
});

test('no_blink needs a longer look and never becomes catalog evidence', () => {
  const ledger = new EvidenceLedger();
  ledger.begin({ tells: [] });
  assert.equal(ledger.inspect('no_blink', 1, 2).requiresMoreTime, true);
  const late = ledger.inspect('no_blink', 1, 9);
  assert.equal(late.proven, false);
  assert.equal(ledger.foundCount, 0);
});

test('locked surfaces stay locked', () => {
  const ledger = new EvidenceLedger();
  ledger.begin({ tells: ['exp_vs_grad'] });
  assert.equal(ledger.openSurface('ResumeRow', 1).length, 0);
  assert.equal(ledger.openSurface('ResumeRow', 2).length, 1);
});

test('tools are capped 2/2/1 and can say VALID for an AI', () => {
  const state = ToolBudget.fresh();
  assert.equal(ToolBudget.use(state, 'id_registry', 1), true);
  assert.equal(ToolBudget.use(state, 'id_registry', 1), true);
  assert.equal(ToolBudget.use(state, 'id_registry', 1), false);
  assert.equal(ToolBudget.use(state, 'metadata_scan', 1), true);
  assert.equal(ToolBudget.use(state, 'metadata_scan', 1), true);
  assert.equal(ToolBudget.use(state, 'metadata_scan', 1), false);
  assert.equal(ToolBudget.use(state, 'reference_call', 1), true);
  assert.equal(ToolBudget.use(state, 'reference_call', 1), false);
  assert.equal(ToolBudget.use(state, 'id_registry', 2), true, 'day 2 use refused');
  assert.equal(ToolBudget.remaining(state, 'id_registry'), 1, 'budget did not reset on day 2');

  const syntheticWithResumeTell = { tells: ['exp_vs_grad'] };
  const valid = toolResult('id_registry', syntheticWithResumeTell, copy);
  assert.equal(valid.ok, true, 'tool solved the game by flagging an AI');
  assert.match(valid.wording, /VALID/);
  const flagged = toolResult('reference_call', { tells: ['ref_unreachable'] }, copy);
  assert.equal(flagged.ok, false);
});

test('paperwork flags stay hidden, lock at stamp and pay trust', () => {
  const flags = buildFlags('This is urgent. [NAME] must reply today. Regards, someone', 'Employee referral');
  assert.equal(flags.length, 5);
  assert.equal(flags.find(f => f.id === 'paper_urgency').isReal, true);
  assert.equal(flags.find(f => f.id === 'paper_mass_template').isReal, true);
  assert.equal(flags.find(f => f.id === 'paper_missing_signature').isReal, false);
  assert.equal(flags.find(f => f.id === 'paper_wrong_reference').isReal, false);
  const picked = new Set(['paper_urgency', 'paper_wrong_reference']);
  const delta = paperworkDelta(flags, picked);
  assert.equal(delta, 4 - 5, 'wrong flag was not charged against the right one');

  const session = new Session(run, keys, copy);
  const before = session.trust;
  session.stamp('APPROVE', null, 5, delta);
  assert.equal(session.trust, Math.min(100, before + 2 + delta), 'flag delta did not reach trust');
});

test('first playable minute: inspect, reject with reason, next applicant', () => {
  const session = new Session(run, keys, copy);
  assert.equal(session.current.name, run.days['1'][0].name);
  while (session.current && !session.current.tells.some(id => TELLS[id].surface === 'ApplicantStage')) {
    session.stamp('APPROVE', null, 12);
    session.beginCurrent();
  }
  assert.ok(session.current, 'no stage-tell applicant found on day 1');
  session.tick(9);
  const seam = session.inspect('seam');
  const mirror = session.inspect('mirror');
  assert.ok(seam.proven || mirror.proven, 'stage tells were not revealed');
  const result = session.stamp('REJECT', 'SYNTHETIC_MEDIA', 22);
  assert.equal(result.isCorrect, true, 'correct rejection scored wrong');
  assert.equal(session.ledger.foundCount > 0, true);
  assert.equal(session.beginCurrent(), true);
  assert.equal(session.ledger.foundCount, 0, 'notebook leaked to next applicant');
});

console.log(`${passed} interaction-twin checks passed`);
