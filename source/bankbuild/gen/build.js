const fs = require('fs');
const path = require('path');
const core = require('./core');

// ---------- 1. 模板生成 ----------
core.reset();
require('./math');
require('./english');
require('./chinese');
require('./history');
require('./geography');
require('./politics');
require('./physics');
require('./chemistry');
require('./biology');
require('./coding');
require('./elementary');

const preGenerated = core.sortedBySubject();
const preTotal = core.stats(preGenerated).total;

// ---------- 2. 导入 E-EVAL 中考真题（val 集，含答案） ----------
function parseCsv(text) {
  const rows = [];
  let row = [], field = '', quoted = false;
  for (let i = 0; i < text.length; i++) {
    const c = text[i];
    if (quoted) {
      if (c === '"') {
        if (text[i + 1] === '"') { field += '"'; i++; }
        else quoted = false;
      } else field += c;
    } else if (c === '"') quoted = true;
    else if (c === ',') { row.push(field); field = ''; }
    else if (c === '\n') { row.push(field); field = ''; rows.push(row); row = []; }
    else if (c === '\r') { /* skip */ }
    else field += c;
  }
  if (field.length || row.length) { row.push(field); rows.push(row); }
  return rows;
}

const EE = {
  Biology: ['biology', 7],
  Chemistry: ['chemistry', 9],
  Chinese: ['chinese', 0],
  English: ['english', 0],
  Geography: ['geography', 8],
  History: ['history', 8],
  Mathematics: ['math', 0],
  Physics: ['physics', 8],
  Politics: ['politics', 9]
};
const LETTERS = { A: 0, B: 1, C: 2, D: 3 };
let eevalCount = 0;

for (const name of Object.keys(EE)) {
  const f = path.join(__dirname, '..', 'external', `eeval_mid_${name}.csv`);
  if (!fs.existsSync(f)) continue;
  const rows = parseCsv(fs.readFileSync(f, 'utf8'));
  const [subject, grade] = EE[name];
  for (let r = 1; r < rows.length; r++) {
    const row = rows[r];
    if (row.length < 7) continue;
    const text = (row[1] || '').trim();
    const opts = [row[2], row[3], row[4], row[5]].map(x => (x || '').trim());
    const key = (row[6] || '').trim().toUpperCase();
    if (!text || text.length > 120) continue;
    if (opts.some(x => !x || x.length > 55)) continue;
    if (/\$|\\|<img|http|（图）|如图/.test(text)) continue;
    if (new Set(opts).size !== 4) continue;
    if (LETTERS[key] === undefined) continue;
    const answer = opts[LETTERS[key]];
    const distractors = opts.filter((x, i) => i !== LETTERS[key]);
    if (core.one(subject, grade, 2, text, answer, distractors, '')) eevalCount++;
  }
}

// ---------- 2a. 导入 C-EVAL 初中真题（val/dev/test 均含答案） ----------
let cevalCount = 0;
{
  const f = path.join(__dirname, '..', 'external', 'ceval_middle.jsonl');
  if (fs.existsSync(f)) {
    for (const line of fs.readFileSync(f, 'utf8').split(/\r?\n/)) {
      if (!line.trim()) continue;
      let q;
      try { q = JSON.parse(line); } catch (e) { continue; }
      const text = String(q.question || '').replace(/\s+/g, ' ').trim();
      const opts = ['A', 'B', 'C', 'D'].map(k => String(q[k] || '').trim());
      const key = String(q.answer || '').trim().toUpperCase();
      if (!text || text.length > 120) continue;
      if (opts.some(x => !x || x.length > 55)) continue;
      if (new Set(opts).size !== 4) continue;
      if (/\$|\\|<img|http/.test(text + opts.join(''))) continue;
      if (LETTERS[key] === undefined) continue;
      const answer = opts[LETTERS[key]];
      const ds = opts.filter((x, i) => i !== LETTERS[key]);
      const exp = /\$|\\/.test(String(q.explanation || '')) ? '' : String(q.explanation || '').replace(/\s+/g, ' ').trim().slice(0, 120);
      if (core.one(q.subject, q.grade, 2, text, answer, ds, exp)) cevalCount++;
    }
  }
}

// ---------- 2a2. 导入 C-MHChem 化学真题（剔除明显高中内容） ----------
let chemExam = 0;
{
  const f = path.join(__dirname, '..', 'external', 'C-MHChem.jsonl');
  const HS = /物质的量|摩尔|阿伏加德罗|氧化还原|离子方程式|化学平衡|平衡常数|焓|熵|电解|原电池|有机物|烃|醇|醛|羧酸|酯化|同分异构|取代反应|加成反应|消去反应|苯环|乙醇|乙酸|葡萄糖|蛋白质|胶体|容量瓶|滴定|气体摩尔体积|元素周期律|电子式|化学键/;
  if (fs.existsSync(f)) {
    for (const line of fs.readFileSync(f, 'utf8').split(/\r?\n/)) {
      if (!line.trim()) continue;
      let q;
      try { q = JSON.parse(line); } catch (e) { continue; }
      const text = String(q.question || '').replace(/\s+/g, ' ').trim();
      const opts = ['A', 'B', 'C', 'D'].map(k => String(q[k] || '').trim());
      const key = String(q.answer || '').trim().toUpperCase();
      if (!text || text.length > 120) continue;
      if (opts.some(x => !x || x.length > 55)) continue;
      if (new Set(opts).size !== 4) continue;
      if (/\$|\\|<img|http/.test(text + opts.join(''))) continue;
      if (HS.test(text)) continue;
      if (LETTERS[key] === undefined) continue;
      const answer = opts[LETTERS[key]];
      const ds = opts.filter((x, i) => i !== LETTERS[key]);
      if (core.one('chemistry', 9, 2, text, answer, ds, '')) chemExam++;
    }
  }
}

// ---------- 2b. 导入初中英语真题（dry-melon，7/8/9 年级单选） ----------
let engExam = 0;
for (const g of [7, 8, 9]) {
  const f = path.join(__dirname, '..', 'external', `eng_grade${g}_Multiple-Choice-Question.jsonl`);
  if (!fs.existsSync(f)) continue;
  const lines = fs.readFileSync(f, 'utf8').split(/\r?\n/);
  for (const line of lines) {
    if (!line.trim()) continue;
    let obj;
    try { obj = JSON.parse(line); } catch (e) { continue; }
    if (obj.context && String(obj.context).trim()) continue; // 阅读材料题跳过
    const qs = obj.questions || [];
    for (const q of qs) {
      let text = String(q.text || '').replace(/<blank>/gi, '______').trim();
      if (!text) continue;
      const ch = q.choices || {};
      const letters = ['A', 'B', 'C', 'D'];
      if (!letters.every(k => ch[k] && String(ch[k]).trim())) continue;
      const opts = letters.map(k => String(ch[k]).trim());
      if (new Set(opts).size !== 4) continue;
      const key = String(q.answer || '').trim().toUpperCase();
      if (!letters.includes(key)) continue;
      const answer = opts[letters.indexOf(key)];
      const ds = opts.filter((x, i) => i !== letters.indexOf(key));
      const exp = String(q.explanation || '').trim().slice(0, 120);
      if (core.one('english', g, 2, text, answer, ds, exp)) engExam++;
    }
  }
}

const generated = core.sortedBySubject();

// ---------- 3. 合并原有题库（保留原有题目与 id） ----------
const basePath = path.join(__dirname, 'base_original.json');
let base = [];
if (fs.existsSync(basePath)) {
  base = JSON.parse(fs.readFileSync(basePath, 'utf8'));
}

function norm(t) { return String(t).replace(/\s+/g, '').trim(); }
const merged = [];
const seen = new Set();
for (const q of base) {
  const t = norm(q.text || '');
  if (!t || seen.has(t)) continue;
  if (String(q.text).trim().length > 140) continue;
  seen.add(t);
  merged.push({
    id: 0, subject: q.subject, grade: q.grade, difficulty: q.difficulty,
    text: String(q.text).trim(), options: q.options, answer: q.answer, explain: q.explain || ''
  });
}
let genAdded = 0;
for (const q of generated) {
  const t = norm(q.text);
  if (seen.has(t)) continue;
  seen.add(t);
  merged.push(q);
  genAdded++;
}

// ---------- 4. 校验 ----------
const problems = [];
for (const q of merged) {
  if (!Array.isArray(q.options) || q.options.length !== 4) problems.push(['选项数', q.text]);
  else if (new Set(q.options).size !== 4) problems.push(['选项重复', q.text]);
  if (typeof q.answer !== 'number' || q.answer < 0 || q.answer > 3) problems.push(['答案下标', q.text]);
  if (!q.text || q.text.length > 140) problems.push(['题干长度', q.text]);
}

merged.forEach((q, i) => q.id = i + 1);

// ---------- 5. 输出 ----------
const ROOT = path.join(__dirname, '..', '..');
const files = [
  path.join(ROOT, 'bankbuild', 'dist', 'questions.json'),
  path.join(ROOT, 'StudyLock', 'questions.json'),
  path.join(ROOT, 'StudyTV', 'assets', 'questions.json')
];
const json = JSON.stringify(merged);
for (const f of files) {
  fs.mkdirSync(path.dirname(f), { recursive: true });
  fs.writeFileSync(f, json, 'utf8');
}

const st = core.stats(merged);
console.log('模板生成:', preTotal, '，E-EVAL:', eevalCount, '，C-EVAL:', cevalCount, '，化学真题:', chemExam, '，英语真题:', engExam);
console.log('生成合计:', generated.length);
console.log('原有题库:', base.length, '，合并新增:', genAdded, '，最终:', merged.length);
console.log('校验问题:', problems.length);
if (problems.length) console.log(problems.slice(0, 20));
console.log('科目:', JSON.stringify(st.bySubject));
console.log('年级:', JSON.stringify(st.byGrade));
console.log('文件大小:', (Buffer.byteLength(json) / 1024 / 1024).toFixed(2), 'MB');
