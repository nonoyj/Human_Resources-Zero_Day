/*
 * HR//0D -- browser interaction twin: pure rules, no DOM.
 * Mirrors unity/Assets/Scripts/Game/Interaction/*.cs and Core/Rules/ScoringService.cs so the
 * playable prototype and the Unity build follow the same fairness contract. Any rule change here
 * must be made in the C# source first; this file is the executable picture of it.
 */

export const TELLS = {
  portrait_asymmetry:   { category: 'SYNTHETIC_MEDIA', surface: 'ApplicantStage', artifact: 'mirror',    style: 'callout', wording: 'A mole sits on the left cheek in the photo and the right cheek in real life.', severity: 2 },
  portrait_blend_seam:  { category: 'SYNTHETIC_MEDIA', surface: 'ApplicantStage', artifact: 'seam',      style: 'magnifier', wording: 'The hair edge cuts a dead-straight line into the background.', severity: 2 },
  voice_flat_metrics:   { category: 'SYNTHETIC_MEDIA', surface: 'CoverLetter',    artifact: 'cover_letter', style: 'callout', wording: 'Every sentence is the same length and there are no contractions.', severity: 1 },
  exp_vs_grad:          { category: 'DOC_INCONSISTENCY', surface: 'ResumeRow',    artifact: 'resume_experience', style: 'callout', wording: 'The claimed years of experience are more than the years since graduation.', severity: 3 },
  employer_founded:     { category: 'DOC_INCONSISTENCY', surface: 'ResumeRow',    artifact: 'resume_employment', style: 'callout', wording: 'The applicant started at a company before that company existed.', severity: 3 },
  grad_too_young:       { category: 'DOC_INCONSISTENCY', surface: 'ResumeRow',    artifact: 'resume_education', style: 'callout', wording: 'The applicant graduated at an age that is not possible.', severity: 3 },
  id_checksum:          { category: 'DOC_INCONSISTENCY', surface: 'IdTool',       artifact: 'id_tool', style: 'callout', wording: 'The ID number fails its checksum digit.', severity: 3 },
  ref_unreachable:      { category: 'IMPERSONATION', surface: 'ReferenceTool',    artifact: 'reference_tool', style: 'callout', wording: 'The referee says they have never heard of this applicant.', severity: 3 },
  identity_clone:       { category: 'IMPERSONATION', surface: 'IdTool',           artifact: 'id_tool', style: 'callout', wording: 'The ID number is registered to a different name.', severity: 3 },
  bait_script:          { category: 'COERCION', surface: 'CoverLetter',           artifact: 'cover_letter', style: 'callout', wording: 'The letter reads like dictated instructions rather than someone writing about themselves.', severity: 2 },
  bait_pressure:        { category: 'COERCION', surface: 'CoverLetter',           artifact: 'cover_letter', style: 'callout', wording: 'The letter pleads for an answer by a deadline that is not the company\'s.', severity: 2 },
  golden_fast_track:    { category: 'SOCIAL_ENGINEERING', surface: 'ResumeRow',   artifact: 'resume_referral', style: 'callout', wording: 'A director fast-track referral with no requisition number.', severity: 2 },
};

export const SURFACE_UNLOCK = {
  1: ['ApplicantStage', 'CoverLetter'],
  2: ['ApplicantStage', 'CoverLetter', 'ResumeRow', 'IdTool'],
  3: ['ApplicantStage', 'CoverLetter', 'ResumeRow', 'IdTool'],
  4: ['ApplicantStage', 'CoverLetter', 'ResumeRow', 'IdTool', 'ReferenceTool'],
  5: ['ApplicantStage', 'CoverLetter', 'ResumeRow', 'IdTool', 'ReferenceTool'],
};

export const REASONS = [
  'SYNTHETIC_MEDIA', 'DOC_INCONSISTENCY', 'IMPERSONATION',
  'COERCION', 'SOCIAL_ENGINEERING', 'INSUFFICIENT',
];

/* The five paperwork flags mirror Core/Data/PaperFlag.cs. isReal is hidden until debrief. */
export const FLAG_DEFS = [
  { id: 'paper_money_demand', needles: ['fee', 'deposit', 'processing cost'], trustDelta: 6 },
  { id: 'paper_urgency', needles: ['immediately', 'today', 'urgent'], trustDelta: 4 },
  { id: 'paper_mass_template', needles: ['[name]', 'dear sir/madam'], trustDelta: 5 },
  { id: 'paper_wrong_reference', needles: null, trustDelta: 5 },
  { id: 'paper_missing_signature', needles: ['regards'], trustDelta: 3 },
];

export function buildFlags(letterText, referral) {
  const text = letterText || '';
  const has = (needle) => text.toLowerCase().includes(needle.toLowerCase());
  return FLAG_DEFS.map(def => ({
    id: def.id,
    trustDelta: def.trustDelta,
    isReal: def.needles === null
      ? !referral
      : def.id === 'paper_missing_signature' ? !has('regards') : def.needles.some(has),
  }));
}

export function paperworkDelta(flags, flagged) {
  let total = 0;
  for (const f of flags || []) {
    if (!flagged || !flagged.has(f.id)) continue;
    total += f.isReal ? f.trustDelta : -f.trustDelta;
  }
  return total;
}

export const ARTIFACTS = {
  seam:           { tell: 'portrait_blend_seam', style: 'magnifier' },
  ear:            { tell: null, style: 'magnifier' },
  hands:          { tell: null, style: 'magnifier' },
  straight_badge: { tell: null, style: 'magnifier' },
  shadow:         { tell: null, style: 'magnifier' },
  mirror:         { tell: 'portrait_asymmetry', style: 'callout' },
  glasses_merge:  { tell: null, style: 'callout' },
  skin_band:      { tell: null, style: 'callout' },
  no_blink:       { tell: null, style: 'callout' },
};

export const isUnlocked = (day, surface) => (SURFACE_UNLOCK[day] || []).includes(surface);
export const isSyntheticReason = (category) => category !== 'INSUFFICIENT';

export class EvidenceLedger {
  constructor() { this.begin(null); }

  begin(applicant) {
    this.applicant = applicant;
    this.tells = new Set();
    this.artifacts = new Set();
    this.surfaces = new Set();
    this.notes = [];
  }

  inspect(artifact, day, observedSeconds = 0) {
    const out = { artifact, tellId: null, style: 'callout', proven: false,
                  changed: false, requiresMoreTime: false, wording: '' };
    const target = ARTIFACTS[artifact];
    if (!target) return out;
    out.style = target.style;
    if (artifact === 'no_blink' && observedSeconds < 6) {
      out.requiresMoreTime = true;
      return out;
    }
    const firstArtifact = !this.artifacts.has(artifact);
    this.artifacts.add(artifact);
    this.surfaces.add('ApplicantStage');
    if (target.tell && this.reveal(target.tell, day)) {
      out.proven = true;
      out.tellId = target.tell;
      out.wording = TELLS[target.tell].wording;
      out.changed = true;
    } else {
      out.changed = firstArtifact;
    }
    return out;
  }

  openSurface(surface, day) {
    const results = [];
    if (!this.applicant || !isUnlocked(day, surface)) return results;
    this.surfaces.add(surface);
    for (const id of this.applicant.tells || []) {
      const tell = TELLS[id];
      if (!tell || tell.surface !== surface) continue;
      const first = this.reveal(id, day);
      results.push({ artifact: tell.artifact, tellId: id, proven: true, changed: first,
                     wording: tell.wording, style: 'callout' });
    }
    return results;
  }

  hasInspected(id) { return this.tells.has(id); }
  get foundCount() { return this.tells.size; }

  reveal(id, day) {
    if (!this.applicant || !TELLS[id]) return false;
    if (!isUnlocked(day, TELLS[id].surface)) return false;
    if (!(this.applicant.tells || []).includes(id)) return false;
    if (this.tells.has(id)) return false;
    this.tells.add(id);
    this.notes.push({ id, wording: TELLS[id].wording, surface: TELLS[id].surface,
                      category: TELLS[id].category });
    return true;
  }

  decisionInput(decision, reason, secondsOnCard) {
    return { decision, reason: decision === 'APPROVE' ? null : reason,
             tellsInspected: [...this.tells], surfacesOpened: [...this.surfaces],
             secondsOnCard };
  }
}

export const ToolBudget = {
  limits: { id_registry: 2, metadata_scan: 2, reference_call: 1 },
  fresh() { return { day: 1, used: { id_registry: 0, metadata_scan: 0, reference_call: 0 } }; },
  remaining(state, tool) {
    return this.limits[tool] - (state.used[tool] || 0);
  },
  use(state, tool, day) {
    if (state.day !== day) { state.day = day; state.used = { id_registry: 0, metadata_scan: 0, reference_call: 0 }; }
    if (this.remaining(state, tool) <= 0) return false;
    state.used[tool] = (state.used[tool] || 0) + 1;
    return true;
  },
};

const TOOL_TELLS = {
  id_registry: ['id_checksum', 'identity_clone'],
  metadata_scan: ['portrait_asymmetry', 'portrait_blend_seam', 'voice_flat_metrics'],
  reference_call: ['ref_unreachable'],
};

/** Tools can say VALID for an AI. Only a tell on that tool's surface changes the answer. */
export function toolResult(tool, applicant, copy) {
  const ids = TOOL_TELLS[tool] || [];
  for (const id of (applicant.tells || [])) {
    if (ids.includes(id)) return { ok: false, wording: TELLS[id].wording };
  }
  return { ok: true, wording: copy.toolValid };
}

export function evaluate(input, applicant, truth, copy) {
  const r = { isCorrect: false, quality: 'WrongCall', trustDelta: 0,
              wrongfulRejection: 0, syntheticHired: 0,
              missedEvidence: (applicant.tells || []).filter(id => !input.tellsInspected.includes(id)),
              debriefLine: '' };
  const hired = input.decision === 'APPROVE';
  const rejected = input.decision === 'REJECT';
  const shouldHire = truth.synthetic === false && (applicant.tells || []).length === 0;
  const shouldReject = truth.synthetic === true || truth.archetype === 'human_bait';

  if (hired) {
    r.isCorrect = shouldHire;
    r.quality = r.isCorrect ? 'CorrectRightReason' : 'WrongCall';
    r.trustDelta = r.isCorrect ? 2 : -12;
    if (!r.isCorrect) {
      r.syntheticHired = 1;
      r.debriefLine = copy.debriefHired + describe(r.missedEvidence);
    } else r.debriefLine = copy.debriefHonest;
    return r;
  }
  if (rejected) {
    r.isCorrect = shouldReject;
    if (!r.isCorrect) {
      r.trustDelta = -8;
      r.wrongfulRejection = 1;
      r.debriefLine = copy.debriefWrongful +
        ((applicant.distractors || []).length ? copy.debriefExplained : copy.debriefPlain);
      return r;
    }
    const supported = (applicant.tells || []).some(id => TELLS[id] && TELLS[id].category === input.reason);
    r.quality = supported ? 'CorrectRightReason' : 'CorrectWrongReason';
    r.trustDelta = 4;
    if (input.reason === 'INSUFFICIENT') {
      r.trustDelta -= 2;
      r.debriefLine = copy.debriefInsufficient + describe(applicant.tells);
    } else if (!supported) {
      r.debriefLine = copy.debriefWrongReason + describe(applicant.tells);
    } else {
      r.debriefLine = copy.debriefCorrect + describe(applicant.tells);
    }
    return r;
  }
  r.debriefLine = copy.debriefNone;
  return r;
}

const describe = (ids) => (ids || []).length
  ? ids.map(id => TELLS[id] ? TELLS[id].wording : id).join(' / ')
  : 'nothing';

export class Session {
  constructor(run, keys, copy) {
    this.run = run;
    this.keys = keys;
    this.copy = copy;
    this.day = 1;
    this.index = 0;
    this.trust = 100;
    this.syntheticsHired = 0;
    this.syntheticsCaught = 0;
    this.wrongfulRejections = 0;
    this.outcome = null;
    this.dayStats = this.freshDayStats();
    this.ledger = new EvidenceLedger();
    this.tools = ToolBudget.fresh();
    this.observedSeconds = 0;
    this.beginCurrent();
  }

  freshDayStats() {
    return { approved: 0, rejected: 0, wrongful: 0, syntheticsHired: 0, syntheticsCaught: 0 };
  }

  get today() { return this.run.days[String(this.day)] || []; }
  get current() { return this.today[this.index]; }
  get dayFinished() { return this.index >= this.today.length; }
  get finished() { return this.outcome !== null; }
  get corruption() { return Math.min(3, this.syntheticsHired); }

  tick(dt) { this.observedSeconds += dt; }

  beginCurrent() {
    if (this.finished || this.dayFinished) return false;
    this.observedSeconds = 0;
    this.ledger.begin(this.current);
    return true;
  }

  inspect(artifact) { return this.ledger.inspect(artifact, this.day, this.observedSeconds); }
  open(surface) { return this.ledger.openSurface(surface, this.day); }
  useTool(tool) { return ToolBudget.use(this.tools, tool, this.day); }
  tool(tool) { return toolResult(tool, this.current, this.copy); }

  stamp(decision, reason, secondsOnCard, letterDelta = 0) {
    if (this.finished || this.dayFinished) return null;
    const applicant = this.current;
    const key = (this.keys.days[String(this.day)] || [])[this.index] || {};
    const input = this.ledger.decisionInput(decision, reason, secondsOnCard);
    const result = evaluate(input, applicant, key, this.copy);
    result.trustDelta += letterDelta;   // same fold as ScoringService.PaperworkTrustDelta
    this.trust = Math.max(0, Math.min(100, this.trust + result.trustDelta));
    if (decision === 'APPROVE') {
      this.dayStats.approved++;
      if (key.synthetic) { this.dayStats.syntheticsHired++; this.syntheticsHired++; }
    } else {
      this.dayStats.rejected++;
      if (result.wrongfulRejection) { this.dayStats.wrongful++; this.wrongfulRejections++; }
      if (key.synthetic || key.archetype === 'human_bait') this.dayStats.syntheticsCaught++;
      if (key.synthetic) this.syntheticsCaught++;
    }
    this.index++;
    if (this.syntheticsHired >= 3) this.outcome = 'DefeatedPerfectEmployee';
    else if (this.trust <= 0) this.outcome = 'DefeatedGhostQuota';
    return result;
  }

  closeDay() {
    if (this.finished) return null;
    const quotaMet = this.dayStats.approved >= 3;
    if (!quotaMet) this.trust = Math.max(0, this.trust - 6);
    const result = { day: this.day, quotaMet, trustAfter: this.trust, ...this.dayStats };
    if (this.syntheticsHired >= 3) this.outcome = 'DefeatedPerfectEmployee';
    else if (this.trust <= 0) this.outcome = 'DefeatedGhostQuota';
    else if (this.day >= 5) this.outcome = 'Survived';
    else {
      this.day++;
      this.index = 0;
      this.dayStats = this.freshDayStats();
      this.beginCurrent();
    }
    return result;
  }
}

export const HotspotHitTest = {
  screenToCanvas(screen, safe, width, height) {
    const w = safe.width > 0 ? safe.width : 1;
    const h = safe.height > 0 ? safe.height : 1;
    const u = Math.max(0, Math.min(1, (screen.x - safe.x) / w));
    const v = Math.max(0, Math.min(1, (screen.y - safe.y) / h));
    return { x: u * width, y: v * height };
  },
  localToCanvas(rect, x, y) {
    return { x: x + rect[0], y: y + rect[1], w: rect[2], h: rect[3] };
  },
  resolve(spots, point, padding, observedSeconds) {
    let best = null;
    let bestArea = Infinity;
    for (const spot of spots) {
      const r = spot.rect;
      if (point.x < r.x - padding || point.x > r.x + r.w + padding ||
          point.y < r.y - padding || point.y > r.y + r.h + padding) continue;
      const area = (r.w + padding * 2) * (r.h + padding * 2);
      const timedWins = best === 'mirror' && spot.artifact === 'no_blink' && observedSeconds >= 6;
      if (area < bestArea || timedWins) { best = spot.artifact; bestArea = area; }
    }
    return best;
  },
};
