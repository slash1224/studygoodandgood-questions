const { one } = require('./core');

/** 可复现的随机数（每次构建出的题库一致，方便测试与在线更新） */
let seed = 20261107;
function rnd() {
  seed = (seed * 1103515245 + 12345) & 0x7fffffff;
  return seed / 0x7fffffff;
}
function ri(a, b) { return Math.floor(rnd() * (b - a + 1)) + a; }
function pick(arr) { return arr[Math.floor(rnd() * arr.length)]; }
function N(x) { return x < 0 ? `(${x})` : `${x}`; }
function uniq(answer, ds) {
  const out = [];
  for (const d of ds) if (String(d) !== String(answer) && !out.includes(d)) out.push(d);
  const cands = [answer + 1, answer - 1, answer + 2, answer - 2, answer + 10, answer - 10, -answer, answer * 2];
  for (const c of cands) if (out.length < 3 && String(c) !== String(answer) && !out.includes(c)) out.push(c);
  return out.slice(0, 3);
}
function add(text, answer, ds, grade, diff) {
  one('math', grade, diff || 1, text, answer, ds);
}

function integerPairs(n, fn) {
  for (let i = 0; i < n; i++) fn(i);
}

// ---- 七年级：有理数与整式 ----
integerPairs(90, () => {
  const a = ri(-20, 20), b = ri(-20, 20);
  add(`计算：${N(a)} + ${N(b)} = ?`, a + b, uniq(a + b, [a - b, -a + b, -(a + b), a + b + 2]), 7, 1);
});
integerPairs(70, () => {
  const a = ri(-15, 20), b = ri(-15, 20);
  if (a - b === 0) return;
  add(`计算：${N(a)} - ${N(b)} = ?`, a - b, uniq(a - b, [a + b, b - a, a - b + 1, -a - b]), 7, 1);
});
integerPairs(70, () => {
  const a = ri(-12, 12), b = ri(-12, 12);
  add(`计算：${N(a)} × ${N(b)} = ?`, a * b, uniq(a * b, [a + b, a - b, -a * b, a * b + 1]), 7, 1);
});
integerPairs(50, () => {
  const b = ri(2, 12), q = ri(-12, 12), a = b * q;
  add(`计算：${N(a)} ÷ ${N(b)} = ?`, q, uniq(q, [-q, q + 1, q - 1, q + 2]), 7, 1);
});
integerPairs(40, () => {
  const a = ri(-20, 20);
  add(`|-${Math.abs(a)}| = ?`, Math.abs(a), uniq(Math.abs(a), [-Math.abs(a), a, 0, Math.abs(a) + 1]), 7, 1);
});
integerPairs(40, () => {
  const a = ri(-15, 15);
  if (a === 0) return;
  add(`数 ${a} 的相反数是？`, -a, uniq(-a, [a, a + 1, -a + 1, -a - 1]), 7, 1);
});
integerPairs(30, () => {
  const p = ri(1, 9), q = ri(2, 9);
  if (p === q) return;
  add(`分数 ${p}/${q} 的倒数是？`, `${q}/${p}`, [`${p}/${q}`, `${q}/${p + 1}`, `-${q}/${p}`, `${p + 1}/${q}`], 7, 2);
});
integerPairs(60, () => {
  const a = ri(2, 9), x = ri(-8, 8), b = ri(-15, 15);
  const c = a * x + b;
  add(`若 ${a}x + ${b} = ${c}，则 x = ?`, x, uniq(x, [x + 1, x - 1, -x, x + 2]), 7, 2);
});
integerPairs(50, () => {
  const a = ri(2, 7), b = ri(1, 9), c = ri(1, 6), d = ri(1, 9);
  const k = a - c, m = b + d;
  const sign = m >= 0 ? `+ ${m}` : `- ${-m}`;
  add(`化简：${a}x + ${b} - (${c}x - ${d}) = ?`, `${k}x ${sign}`, [`${k}x ${m >= 0 ? '- ' + m : '+ ' + (-m)}`, `${a + c}x ${sign}`, `${k}x ${sign}`, `${k}x ${m}`], 7, 2);
});
integerPairs(50, () => {
  const a = ri(2, 9), b = ri(1, 9), c = ri(2, 9), d = ri(1, 9);
  const k = a - c, m = b + d;
  add(`合并同类项：${a}x + ${b}y - ${c}x + ${d}y = ?`, `${k}x + ${m}y`, [`${k}x - ${m}y`, `${a + c}x + ${m}y`, `${k}x + ${b + d}y`, `${a - c}x - ${m}y`], 7, 2);
});
integerPairs(40, () => {
  const a = ri(1, 6), b = ri(1, 6);
  add(`计算：${a}x + ${b}x = ?`, `${a + b}x`, [`${a * b}x`, `${a + b}x²`, `${a - b}x`, `${a + b}`], 7, 1);
});
integerPairs(40, () => {
  const b = ri(1, 6);
  add(`计算：x³ · x^${b} = ?`, `x^${3 + b}`, [`x^${3 * b}`, `x^${b}`, `x^${3 + b + 1}`, `${3 + b}x`], 8, 2);
});
integerPairs(40, () => {
  const a = ri(2, 9);
  add(`计算：x^5 · x^${a} = ?`, `x^${5 + a}`, [`x^${5 * a}`, `x^${a}`, `x^${5 + a + 1}`, `${5 + a}x`], 8, 2);
});
integerPairs(40, () => {
  const a = ri(2, 12);
  add(`计算：(x + ${a})(x - ${a}) = ?`, `x² - ${a * a}`, [`x² + ${a * a}`, `x² - ${2 * a}`, `x² + ${2 * a}x`, `x² - ${a}`], 8, 2);
});
integerPairs(40, () => {
  const a = ri(2, 10);
  add(`计算：(x + ${a})² = ?`, `x² + ${2 * a}x + ${a * a}`, [`x² + ${a * a}`, `x² + ${2 * a}x - ${a * a}`, `x² + ${a}x + ${a * a}`, `x² - ${2 * a}x + ${a * a}`], 8, 3);
});
integerPairs(50, () => {
  const r1 = ri(1, 8), r2 = ri(1, 8);
  const p = r1 + r2, q = r1 * r2;
  add(`因式分解：x² + ${p}x + ${q} = ?`, `(x + ${r1})(x + ${r2})`, [`(x - ${r1})(x - ${r2})`, `(x + ${r1})(x - ${r2})`, `(x + ${p})(x + ${q})`, `(x + ${r1 + 1})(x + ${r2})`], 8, 3);
});
integerPairs(50, () => {
  const r1 = ri(1, 8), r2 = ri(1, 8), s = r1 + r2, p = r1 * r2;
  add(`方程 x² - ${s}x + ${p} = 0 的两个根是？`, `${r1} 和 ${r2}`, [`${-r1} 和 ${-r2}`, `${r1} 和 ${-r2}`, `${r1 + 1} 和 ${r2}`, `${s} 和 ${p}`], 9, 2);
});
integerPairs(35, () => {
  const a = ri(2, 6), b = ri(-6, 6), v = ri(-5, 5);
  const c = a * v + b;
  const rel = rnd() > 0.5 ? '<' : '>';
  add(`解不等式：${a}x + ${b} ${rel} ${c}，得 x ${rel} ${v}`, `x ${rel} ${v}`, [`x ${rel === '<' ? '>' : '<'} ${v}`, `x ${rel} ${-v}`, `x ${rel} ${v + 1}`, `x ${rel === '<' ? '<' : '>'} ${v + 1}`], 8, 3);
});

// ---- 百分数、科学记数法、根式 ----
integerPairs(50, () => {
  const p = ri(100, 400) * 5, d = ri(3, 9);
  add(`一件商品原价 ${p} 元，打 ${d} 折后售价是？`, `${p * d / 10} 元`, [`${p * d} 元`, `${p - d * 10} 元`, `${p * d / 100} 元`, `${p * (10 - d) / 10} 元`], 7, 2);
});
integerPairs(40, () => {
  const p = ri(100, 500), r = ri(1, 5) * 10;
  add(`某商品原价 ${p} 元，涨价 ${r}% 后的价格是？`, `${p + p * r / 100} 元`, [`${p * r / 100} 元`, `${p - p * r / 100} 元`, `${p + r} 元`, `${p + p * r / 10} 元`], 7, 2);
});
integerPairs(40, () => {
  const a = ri(1, 9), e = ri(2, 6);
  const v = a * Math.pow(10, e);
  add(`用科学记数法表示 ${v}，结果是？`, `${a}×10^${e}`, [`${a}×10^${e - 1}`, `${a + 1}×10^${e}`, `${a}×10^${e + 1}`, `${a}×10^-${e}`], 7, 2);
});
integerPairs(35, () => {
  const n = ri(2, 12);
  add(`计算：√${n * n} = ?`, n, uniq(n, [n + 1, n - 1, n * n, 2 * n]), 8, 1);
});
integerPairs(30, () => {
  const n = ri(2, 9);
  add(`计算：³√${n * n * n} = ?`, n, uniq(n, [n + 1, n - 1, n * n, 3 * n]), 8, 1);
});

// ---- 几何 ----
const triples = [[3, 4, 5], [5, 12, 13], [6, 8, 10], [8, 15, 17], [7, 24, 25], [9, 12, 15], [12, 16, 20], [20, 21, 29], [10, 24, 26], [15, 20, 25]];
integerPairs(50, () => {
  const [a, b, c] = pick(triples);
  add(`直角三角形两条直角边长分别为 ${a} 和 ${b}，斜边长为？`, c, uniq(c, [a + b, c + 1, c - 1, a * b]), 8, 2);
});
integerPairs(40, () => {
  const a = ri(20, 70), b = ri(20, 70);
  if (a + b >= 170) return;
  add(`三角形中两个内角分别为 ${a}° 和 ${b}°，第三个角是？`, `${180 - a - b}°`, [`${180 - a + b}°`, `${a + b}°`, `${90 - a + b}°`, `${360 - a - b}°`], 7, 1);
});
integerPairs(35, () => {
  const a = ri(1, 12);
  add(`一个 ${a + 2} 边形的内角和是？`, `${(a) * 180}°`, [`${(a + 2) * 180}°`, `${(a + 1) * 180}°`, `${a * 360}°`, `${a * 90}°`], 8, 2);
});
integerPairs(35, () => {
  const s = pick([3, 4, 5, 6, 8, 10, 12]);
  const ang = (s - 2) * 180 / s;
  add(`正 ${s} 边形每个内角的度数是？`, `${ang}°`, [`${180 - ang}°`, `${ang + 10}°`, `${(s - 2) * 180}°`, `${360 / s}°`], 9, 3);
});
integerPairs(40, () => {
  const r = ri(2, 10);
  add(`半径 ${r} cm 的圆的面积是（π 取 3.14）？`, `${(3.14 * r * r).toFixed(2)} cm²`, [`${(2 * 3.14 * r).toFixed(2)} cm²`, `${(3.14 * r).toFixed(2)} cm²`, `${(3.14 * r * r * 2).toFixed(2)} cm²`, `${(6.28 * r).toFixed(2)} cm²`], 9, 2);
});
integerPairs(30, () => {
  const r = ri(2, 10);
  add(`半径 ${r} cm 的圆的周长是（π 取 3.14）？`, `${(2 * 3.14 * r).toFixed(2)} cm`, [`${(3.14 * r).toFixed(2)} cm`, `${(3.14 * r * r).toFixed(2)} cm`, `${(4 * 3.14 * r).toFixed(2)} cm`, `${(3.14 * r * r * 2).toFixed(2)} cm`], 7, 1);
});
integerPairs(30, () => {
  const r = ri(6, 20), a = 90;
  add(`半径为 ${r} cm、圆心角为 ${a}° 的扇形面积是（π 取 3.14）？`, `${(3.14 * r * r / 4).toFixed(2)} cm²`, [`${(3.14 * r * r / 2).toFixed(2)} cm²`, `${(3.14 * r * r / 8).toFixed(2)} cm²`, `${(3.14 * r * r).toFixed(2)} cm²`, `${(3.14 * r / 2).toFixed(2)} cm²`], 9, 3);
});
integerPairs(40, () => {
  const x = pick([-5, -4, -3, -2, 2, 3, 4, 5]);
  const y = pick([-5, -4, -3, -2, 2, 3, 4, 5]);
  const q = x > 0 ? (y > 0 ? '第一象限' : '第四象限') : (y > 0 ? '第二象限' : '第三象限');
  const all = ['第一象限', '第二象限', '第三象限', '第四象限'].filter(v => v !== q);
  add(`点 (${x}, ${y}) 位于第几象限？`, q, all, 7, 2);
});

// ---- 统计与概率 ----
integerPairs(40, () => {
  const a = ri(60, 100), b = ri(60, 100), c = ri(60, 100), d = ri(60, 100), e = ri(60, 100);
  const avg = (a + b + c + d + e) / 5;
  const fixed = Number.isInteger(avg) ? avg : Number(avg.toFixed(1));
  add(`数据 ${a}、${b}、${c}、${d}、${e} 的平均数是？`, fixed, [`${fixed + 1}`, `${fixed - 1}`, `${fixed + 2}`, `${a + b + c + d + e}`], 8, 2);
});
integerPairs(40, () => {
  const arr = [ri(1, 20), ri(1, 20), ri(1, 20), ri(1, 20), ri(1, 20)].sort((x, y) => x - y);
  const mid = arr[2];
  add(`数据 ${arr.join('、')} 的中位数是？`, mid, uniq(mid, [arr[1], arr[3], (arr[1] + arr[3]) / 2, arr[0]]), 8, 2);
});
integerPairs(35, () => {
  const v = ri(1, 9), a = [v, v, ri(1, 9), ri(1, 9), ri(1, 9)];
  const counts = {};
  a.forEach(x => counts[x] = (counts[x] || 0) + 1);
  const mode = v;
  add(`数据 ${a.join('、')} 的众数是？`, mode, uniq(mode, a.filter(x => x !== mode).slice(0, 3)), 8, 2);
});
integerPairs(40, () => {
  let r = ri(2, 10), w = ri(2, 10);
  if (r === w) w++;
  add(`袋中有 ${r} 个红球和 ${w} 个白球，随机摸出一个是红球的概率是？`, `${r}/${r + w}`, [`${w}/${r + w}`, `${r}/${w}`, `${r + w}/${r}`, `1/${r + w}`], 9, 2);
});

// ---- 函数与方程 ----
integerPairs(50, () => {
  const k = pick([-3, -2, -1, 1, 2, 3, 4]), b = ri(-5, 5), x = ri(-4, 4);
  const y = k * x + b;
  add(`一次函数 y = ${k}x ${b >= 0 ? '+ ' + b : '- ' + (-b)}，当 x = ${x} 时 y = ?`, y, uniq(y, [y + k, y - k, k + x + b, k * x - b]), 8, 2);
});
integerPairs(35, () => {
  const k = pick([2, 3, 4, -2, -3]), x1 = 2, y1 = k * 2, x2 = ri(3, 8);
  add(`y 与 x 成正比例，当 x = 2 时 y = ${y1}，则 x = ${x2} 时 y = ?`, k * x2, uniq(k * x2, [y1 + x2, k * x2 + 2, y1 * x2, k * x2 - 1]), 8, 2);
});
integerPairs(30, () => {
  const a = ri(1, 5), b = ri(-6, 6);
  add(`二次函数 y = (x - ${a})² ${b >= 0 ? '+ ' + b : '- ' + (-b)} 的顶点坐标是？`, `(${a}, ${b})`, [`(${-a}, ${b})`, `(${a}, ${-b})`, `(${b}, ${a})`, `(${-a}, ${-b})`], 9, 3);
});
integerPairs(35, () => {
  const m = ri(1, 4), n = ri(2, 5);
  const a = m * m, b = m * n, c = n * n;
  add(`两个相似三角形的对应边之比为 ${m}:${n}，它们的面积之比是？`, `${a}:${c}`, [`${m}:${n}`, `${a}:${b}`, `${m}:${c}`, `${c}:${a}`], 9, 3);
});

// ---- 三角函数与特殊角 ----
const trig = [['sin30°', '1/2'], ['cos60°', '1/2'], ['tan45°', '1'], ['sin45°', '√2/2'], ['cos45°', '√2/2'], ['sin60°', '√3/2'], ['cos30°', '√3/2'], ['tan30°', '√3/3'], ['tan60°', '√3']];
integerPairs(45, () => {
  const [k, v] = pick(trig);
  const wrong = trig.filter(t => t[1] !== v).map(t => t[1]);
  add(`计算：${k} = ?`, v, wrong.slice(0, 3), 9, 3);
});

// ---- 应用题（中考常见题型） ----
integerPairs(40, () => {
  const speed = ri(40, 80), hours = ri(2, 6);
  const dist = speed * hours;
  add(`一辆汽车以 ${speed} 千米/时的速度行驶 ${hours} 小时，行驶的路程是？`, `${dist} 千米`, [`${speed + hours} 千米`, `${dist + speed} 千米`, `${dist - speed} 千米`, `${speed * (hours + 1)} 千米`], 7, 2);
});
integerPairs(40, () => {
  const price = ri(3, 15), n = ri(5, 20);
  add(`买 ${n} 支单价 ${price} 元的笔，共需多少元？`, `${n * price} 元`, [`${n + price} 元`, `${n * price + price} 元`, `${n * price - n} 元`, `${price} 元`], 7, 1);
});
integerPairs(35, () => {
  const len = ri(5, 20), wid = ri(3, 15);
  add(`长方形长 ${len} cm、宽 ${wid} cm，它的面积是？`, `${len * wid} cm²`, [`${2 * (len + wid)} cm²`, `${len + wid} cm²`, `${len * wid / 2} cm²`, `${2 * len * wid} cm²`], 7, 1);
});
integerPairs(35, () => {
  const base = ri(4, 20), h = ri(3, 12);
  add(`三角形底边 ${base} cm、高 ${h} cm，面积是？`, `${base * h / 2} cm²`, [`${base * h} cm²`, `${(base + h) / 2} cm²`, `${base * h / 3} cm²`, `${base + h} cm²`], 7, 1);
});
integerPairs(30, () => {
  const a = ri(2, 9), b = ri(2, 9);
  add(`计算：(${a}x)² = ?`, `${a * a}x²`, [`${a}x²`, `${2 * a}x²`, `${a * a}x`, `${a * a}x³`], 8, 2);
});
integerPairs(30, () => {
  const a = pick([2, 3, 4, 5, 6]);
  add(`计算：√${a * a * 4} = ?`, `${2 * a}`, [`${a}`, `${4 * a}`, `${a * a}`, `${2 * a + 1}`], 8, 2);
});
integerPairs(25, () => {
  const a = ri(1, 5), b = ri(1, 5);
  add(`若 |x - ${a}| = ${b}，则 x 的值是？`, `${a + b} 或 ${a - b}`, [`${a + b} 或 ${-a - b}`, `${a} 或 ${b}`, `${a - b} 或 ${b - a}`, `${a + b} 或 ${b - a + 1}`], 7, 3);
});

module.exports = {};
