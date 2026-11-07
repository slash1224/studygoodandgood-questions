const fs = require('fs');
const path = require('path');

/** 稳定哈希：让同一道题的选项顺序在每次构建时保持一致 */
function hash(s) {
  let x = 2166136261;
  for (let i = 0; i < s.length; i++) {
    x ^= s.charCodeAt(i);
    x = Math.imul(x, 16777619);
  }
  return (x >>> 0);
}

/** 确定性洗牌：按题干哈希把 [答案+干扰项] 轮转，避免答案总在 A */
function arrange(text, answer, distractors) {
  let opts = [answer];
  const seen = new Set([String(answer).trim()]);
  for (const d of distractors) {
    const v = String(d == null ? '' : d).trim();
    if (!v || seen.has(v)) continue;
    seen.add(v);
    opts.push(v);
    if (opts.length === 4) break;
  }
  if (opts.length < 4) return { options: [], answer: -1 };
  opts = opts.slice(0, 4);
  const n = hash(text) % 4;
  opts = opts.slice(n).concat(opts.slice(0, n));
  return { options: opts, answer: opts.indexOf(String(answer).trim()) };
}

const all = [];
const seenText = new Set();

function norm(t) {
  return String(t).replace(/\s+/g, '').trim();
}

/** 单题：text + 正确答案 + 3 个干扰项 */
function one(subject, grade, difficulty, text, answer, distractors, explain) {
  text = String(text).trim();
  if (!text || text.length > 130) return false;
  const a = String(answer).trim();
  if (!a || a.length > 60) return false;
  const ds = (distractors || []).map(x => String(x).trim()).filter(Boolean);
  if (ds.length < 3) return false;
  if (ds.some(x => x.length > 60)) return false;
  const key = norm(text);
  if (seenText.has(key)) return false;
  const arranged = arrange(text, a, ds);
  if (arranged.answer < 0 || arranged.answer > 3) return false;
  const options = arranged.options;
  if (options.some(o => o.length > 60)) return false;
  seenText.add(key);
  all.push({
    id: 0,
    subject,
    grade,
    difficulty,
    text,
    options,
    answer: arranged.answer,
    explain: explain || ''
  });
  return true;
}

/** 批量事实题：list 每项 [题干, 答案, [干扰1,干扰2,干扰3]] */
function facts(subject, grade, difficulty, list) {
  for (const it of list) {
    if (!it) continue;
    one(subject, grade, difficulty, it[0], it[1], it[2] || [], it[3]);
  }
}

/**
 * 配对题：ask 中用 {} 代表配对里的第一个值，答案取配对第二个值；
 * 干扰项自动从同组其它配对的第二个值里选，适合元素符号/省会/单词释义等。
 */
function pairs(subject, grade, difficulty, ask, list, extra) {
  const answers = list.map(x => String(x[1]).trim());
  for (let i = 0; i < list.length; i++) {
    const [k, a] = list[i];
    const text = ask.replace('{}', k);
    const ds = [];
    const ban = new Set([String(a).trim()]);
    const offset = hash(text) % answers.length;
    for (let j = 0; j < answers.length && ds.length < 3; j++) {
      const cand = answers[(offset + j) % answers.length];
      if (ban.has(cand)) continue;
      ban.add(cand);
      ds.push(cand);
    }
    if (extra) {
      for (const e of extra) {
        if (ds.length >= 3) break;
        if (!ban.has(e)) { ban.add(e); ds.push(e); }
      }
    }
    one(subject, grade, difficulty, text, a, ds);
  }
}

function reset() { all.length = 0; seenText.clear(); }

function sortedBySubject() {
  const order = ['math', 'chinese', 'english', 'physics', 'chemistry', 'biology', 'history', 'geography', 'politics', 'coding'];
  return all.slice().sort((a, b) => {
    const s = order.indexOf(a.subject) - order.indexOf(b.subject);
    if (s !== 0) return s;
    return a.grade - b.grade;
  });
}

function writeJson(file, list, withIds) {
  const arr = list.map((q, i) => Object.assign({ id: withIds ? i + 1 : 0 }, q));
  fs.mkdirSync(path.dirname(file), { recursive: true });
  fs.writeFileSync(file, JSON.stringify(arr, null, 1), 'utf8');
  return arr.length;
}

function stats(list) {
  const bySubject = {}, byGrade = {};
  for (const q of list) {
    bySubject[q.subject] = (bySubject[q.subject] || 0) + 1;
    byGrade[q.grade] = (byGrade[q.grade] || 0) + 1;
  }
  return { total: list.length, bySubject, byGrade };
}

module.exports = { one, facts, pairs, reset, sortedBySubject, writeJson, stats, norm, hash };
