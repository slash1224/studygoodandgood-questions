// 一次性追加脚本：把 elementary.js 生成的小学题合并进现有 questions.json
// 不依赖 bankbuild/external 外部数据集，不会丢失现有初中真题。
// 用法：node source/bankbuild/gen/append_elementary.js
const fs = require('fs');
const path = require('path');
const core = require('./core');

core.reset();
require('./elementary');
const gen = core.sortedBySubject();

const SRC = path.join(__dirname, '..', '..', '..'); // 仓库根目录
const files = [
  path.join(SRC, 'questions.json'),
  path.join(SRC, 'source', 'StudyLock', 'questions.json'),
  path.join(SRC, 'source', 'StudyTV', 'assets', 'questions.json'),
];

const base = JSON.parse(fs.readFileSync(files[0], 'utf8'));
const seen = new Set();
const merged = [];
for (const q of base) {
  const t = core.norm(q.text || '');
  if (!t || seen.has(t)) continue;
  seen.add(t);
  merged.push(q);
}
let added = 0;
for (const q of gen) {
  const t = core.norm(q.text);
  if (seen.has(t)) continue;
  seen.add(t);
  merged.push(q);
  added++;
}

// 校验
const problems = [];
for (const q of merged) {
  if (!Array.isArray(q.options) || q.options.length !== 4) problems.push(['选项数', q.text]);
  else if (new Set(q.options).size !== 4) problems.push(['选项重复', q.text]);
  if (typeof q.answer !== 'number' || q.answer < 0 || q.answer > 3) problems.push(['答案下标', q.text]);
  if (!q.text || q.text.length > 140) problems.push(['题干长度', q.text]);
}
merged.forEach((q, i) => q.id = i + 1);

const json = JSON.stringify(merged);
for (const f of files) fs.writeFileSync(f, json, 'utf8');

console.log('小学题生成:', gen.length, '，去重后新增:', added);
console.log('原有:', base.length, '，最终:', merged.length, '，校验问题:', problems.length);
if (problems.length) console.log(problems.slice(0, 20));
const st = core.stats(merged);
console.log('科目:', JSON.stringify(st.bySubject));
console.log('年级:', JSON.stringify(st.byGrade));
console.log('小学(1-6)各年级题量:', [1, 2, 3, 4, 5, 6].map(g => `${g}年级:${st.byGrade[g] || 0}`).join(' '));
console.log('文件大小:', (Buffer.byteLength(json) / 1024 / 1024).toFixed(2), 'MB');
