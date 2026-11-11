# 原版化学扩展 / Vanilla Chemistry Expansion

以原版化学系统（萃取、加热、冷藏、过滤、蒸发、成型）为基础的扩展，不改动原版产品和反应。

当前内容：

- **梦魔菌毒素**：梦帽菇和梦魇帽菇的合法医疗出路。
- **矿石化学**：把金属矿石搅碎，制取生石灰和硅胶，用来提纯酸和碱，帮助药品达到实验室纯。
- **淀粉基因**：营养果的淀粉和糖变成可以移植的基因。
- **工业酒精**：淀粉水解、酵母过夜发酵、冷冻浓缩。
- **勾兑葡萄酒**：自制乙酸乙酯勾兑假酒，有品质、标签和欺诈机制。
- **高级韧棉织物**：每天回满耐久的韧棉织物。
- **啤酒**：把酒液灌装成桶装啤酒，倒出罐装啤酒或冰镇扎啤，分三档品质。
- **核废料后处理**：撬开核废料，萃取钚做成钚安瓿，玻璃固化裂变产物，小心辐射病。
- **空心灯与失能剂**：种植实验室辣椒，提纯成合法的失能剂，卖给上层区和治安部能冲减罪名和调查进度。
- **电解食盐水**：订购盐矿、电解食盐水得到碱、氢气和氯气，混气加热合成氯化氢，再溶成盐酸，批量自制纯净的酸碱。
- **进阶化学手册**：所有工艺步骤都在这本手册里，化学家开局直接送一本，退休化学家和杰克逊博士也出售。

---

## 一、梦魔菌毒素

一种新液体。容器里同时有**梦帽菇萃取液**和**梦魇帽菇萃取液**，并满足以下条件时，每次结算转化较少那种萃取液的 5%（至少 400 微升）：

- 酸浓度（酸 ÷ 总体积）在 **0.5%–5%** 之间；
- **冷藏**：温度在 **2–8°C**，或者容器正放在**通电冰箱的冷藏区**里（冷冻区不算）。

每次反应各消耗 1 份萃取液，生成 1 份毒素和 1 份残渣（颗粒大，可以过滤）。放进冷藏区后几分钟就能基本反应完（液体越多越久）。

- **毒素耐高温**：反应结束后，可以加热到 100°C 以上，把多出来的萃取液烧掉（原版规则下萃取液高温分解后直接消失），毒素不受影响。
- 唯一会让毒素分解的情况：酸浓度高于 **5%**，每次结算 2% 变成残渣。

### 梦魔菌毒素注射剂

在化学制剂成型机中选择“梦魔菌毒素注射剂”。每份需要 5000 微升，必须干燥，基础价 150。合法医疗品（上层区医美诊所的抢手货），不是违禁品。

只有药用级以上才值钱：

| 纯度 | 价格修正 |
|---|---|
| ≥ 99% 实验室纯 | +135% |
| ≥ 90% 药用级 | +85% |
| ≥ 80% 接近药用 | −60% |
| ≥ 60% 街头标准 | −90% |
| 更低 | −95% |

药用级以上时，按每毫升投入的萃取液计算，收益约等于含违禁加价的街头梦尘，但不产生任何恶名。

### 推荐流程

1. 分别制取梦帽菇萃取液和梦魇帽菇萃取液（梦魇帽菇萃取液超过 50°C 会很快分解）。
2. 按 1:1 混合，加少量酸，让酸占萃取液的 0.5%–5%。蛋白质会消耗酸，注意补充。
3. 放进通电冰箱的冷藏区静置。
4. 有萃取液剩下的话，加热到 100°C 以上烧掉。
5. 加等量的碱中和掉酸，过滤残渣，用蒸发器蒸干。
6. 在化学制剂成型机中压制成注射剂。

### 配方

制取步骤收录在**进阶化学手册**里：只要你还没有这本手册，**退休化学家**每次来店、**杰克逊博士**的商店每次都会出售一本。退休化学家每周第 2 天来店，需要化学家开局或人脉网络的“退休化学家”升级。

---

## 二、矿石化学：提纯酸和碱

原版的酸瓶、碱瓶只有 92%–98% 纯度，其中的**化学污染物**和**重金属**任何过滤器都滤不掉。沉淀蛋白质、中和酸碱时，这些杂质会被带进药品，很难做到实验室纯。矿石化学就是用来解决这个问题的。

### 搅碎金属矿石

金属矿石只能在**装满 4 个化学模组的搅拌机**里搅碎，模组不够时会原样留在输入格里。每块矿石搅碎后的成分会在下面的区间里随机（单位：毫升）：

| 方解石 | 石英砂 | 矿物质 | 重金属 | 物理污染物 |
|---|---|---|---|---|
| 9–15 | 11–17 | 4–8 | 2.5–5.5 | 2–6 |

### 新液体与分级过滤

| 液体 | 颗粒 | 韧棉织物（孔径 150） | 滤水器（孔径 22） |
|---|---|---|---|
| 石英砂 | 200 | 滤掉 | 滤掉 |
| 方解石、生石灰、碳酸钙、硅胶 | 100 | 留下 | 滤掉 |
| 水玻璃 | 5（溶解） | 留下 | 留下 |

（原版的残渣、植物质是 200，矿物质 30，物理污染物 100；重金属、化学污染物、酸、碱都滤不掉。）

### 反应

| 反应 | 条件 | 结果 |
|---|---|---|
| 煅烧 | 容器干燥，**≥ 150°C** | 方解石 → 0.55 份生石灰 |
| 制水玻璃 | 有碱，**≥ 120°C** | 石英砂 + 碱 → 水玻璃（1:1） |
| 制硅胶 | 有酸 | 水玻璃 + 酸 → 硅胶 + 残渣（1:1） |
| 碱的苛化 | 碱 ≥ 50%，有生石灰 | 生石灰 + 化学污染物 → **碱** + 碳酸钙；每次结算 5%，≥ 80°C 时 10% |
| 碱中重金属沉淀 | 碱 ≥ 50% | 重金属自行沉淀成残渣（每次结算 5%），不需要试剂 |
| 酸的脱污 | 酸 ≥ 80%，有硅胶，**≥ 100°C** | 化学污染物变成气体消失，1 份硅胶带走 4 份 |
| 酸的冷冻结晶 | 酸 ≥ 80%，≤ −5°C 或放在**通电冰箱的冷冻区** | 重金属 → 残渣，同时损失等量的酸；剩余硅胶也变成残渣 |

150°C 和 120°C 都需要**升级过的加热板**（默认最高 99°C）。

### 碱的净化流程

1. **搅碎**：金属矿石搅进空容器。
2. **韧棉织物过滤**：只滤掉石英砂。
3. **煅烧**：加热到 ≥ 150°C，保持干燥，方解石变成生石灰。
4. **混合**：把生石灰倒进碱里（碱要占 ≥ 50%）。
5. **反应**：室温下稍等片刻即可，加热到 80–120°C 会更快。
6. **滤水器过滤**：滤掉碳酸钙、残渣、多余的生石灰、矿物质和物理污染物。

一块矿石大约够净化一瓶碱。

### 酸的净化流程

**做硅胶试剂**（一块矿石大约够 4 瓶酸）：

1. **搅碎**：金属矿石搅进空容器（不要先过滤，石英砂要留着）。
2. **加碱**：倒入和石英砂等量的碱（石英砂和碱 1:1 反应，一块矿石约 11–17 毫升石英砂）。
3. **溶解石英**：加热到 ≥ 120°C，石英砂 + 碱 → 水玻璃。
4. **加水过滤**：倒入约 50 毫升水，用滤水器滤掉方解石、矿物质和物理污染物。
5. **析出硅胶**：倒入约 15 毫升酸，水玻璃变成硅胶和残渣；用**韧棉织物**过滤掉残渣，硅胶留下。

**提纯酸**：

6. **混合**：把约 1/4 的硅胶试剂倒进酸瓶。
7. **脱除化学污染物**：加热到 ≥ 100°C，化学污染物变成气体消失，水也会一起蒸发。
8. **冷冻结晶**：放进通电冰箱的冷冻区，重金属变成残渣（同时损失等量的酸）。
9. **滤水器过滤**：滤掉残渣和剩余的硅胶。

第 7、8 步时酸必须占 ≥ 80%，并且要**先加热、再冷冻**。

### 矿石化学笔记

碱和酸的净化步骤收录在**进阶化学手册**里。

### 注意

- 碱占 ≥ 50% 时重金属会自己沉淀，所以普通碱瓶放久了，重金属也会逐渐变成可过滤的残渣。
- 碱净化时如果还混着石英砂，不要加热到 120°C 以上，否则石英砂会和碱反应成水玻璃，白白消耗碱。

---

## 三、淀粉基因

原版营养果的淀粉和糖是写死的成分。本 Mod 把它们改成两个营养果自带的“产出”类基因，总量和原版一样：

| 基因 | 营养果自带 | 复杂度 | 1 级效果 |
|---|---|---|---|
| 淀粉产出 | 4 级 | 3 | 每 100 克果实 +6.13 毫升淀粉（其他作物每个收获物 +15.3 毫升） |
| 糖产出 | 1 级 | 2 | 每 100 克果实 +0.67 毫升糖（其他作物每个收获物 +1.7 毫升） |

- 可以照原版规则提取到基因盘（营养果提取淀粉产出得到 1 级、4 次的盘），写进填充草等作物，做成工业淀粉作物。
- 营养果按大小计算，自带基因不计复杂度；“强效 / 虚弱”对淀粉和糖生效。
- 旧存档的营养果种子读档时会自动补上这两个基因。

## 四、工业酒精

| 步骤 | 条件 | 说明 |
|---|---|---|
| 酸水解 | 酸 ≥ 0.5%，≥ 90°C | 淀粉 → 糖；高温同时杀菌 |
| 中和、冷却 | 酸 + 碱 ≤ 1%，< 45°C | 否则酵母会死 |
| 接种 | 把酵母拖到容器上 | 酿酒酵母 2 毫升、超级酵母 4 毫升、红色酵母 1.5 毫升 + 0.5 毫升微生物；酵母生物反应器 2 毫升，不消耗，每晚一次 |
| 发酵 | **过夜**，20–35°C | 酵母每晚繁殖（翻倍，红色酵母 ×3，上限占总体积 2%），每毫升每晚把 24 毫升糖变成 24 毫升酒精（按体积 1:1）（模组设置可调） |
| 去甲醇 | 65–75°C | 发酵中的微生物会变成甲醇（沸点 64.7°C） |
| 冷冻浓缩 | 冰箱冷冻区 | 水结成冰（韧棉织物可滤），直到非水成分占 25% |
| 精馏 | 蒸发器 + 溶剂回收模组 | 原版机制 |

- 耐酒精：酿酒酵母 12%，超级酵母 18%，红色酵母 15%；超过后每晚死 25%。
- 5 颗营养果约 3–4 晚发酵完，得到约 100 毫升酒精。
- 成分分析仪的检测结果会显示容器温度。
- 模组设置里可以调整水的沸腾速度（10%–1000%）和冰箱降温速度（100%–2000%，默认 400%）。

## 五、香料与勾兑葡萄酒

- **干馏**：干燥的植物质 ≥ 150°C → 30% 乙酸 + 60% 残渣（木炭），另外 10% 变成甲醇蒸气直接逸出。
- **酯化**：乙酸 + 酒精，酸 ≥ 1%，60–75°C → 乙酸乙酯（沸点 77°C）。
- **勾兑葡萄酒**：在化学制剂成型机里每 750 毫升压一瓶，外观和原版葡萄酒相同，不能陈酿。
- **品质**（基础 1 分，照原版葡萄酒等级换算）：酒精 20–25% +3，5–20% 或超过 25% +2，不足 5% −3（所以酒精不足 5% 最多 0 分，不可饮用）；没有乙酸乙酯 −2，每升少于 0.5 或多于 4 毫升 −1，1–2 毫升 +1；杂质 > 2% −1，≤ 1% +1。甲醇 > 0.1% 会危害公共安全。
- **两个标签**都能用贴标机修改：葡萄酒品质、酿造方式（纯酿 / 勾兑，勾兑售价 −15 个百分点，上层区客人不收）。
- **识破**：品质标高 ≥ 2 级，任何客人；高 1 级，上层区客人、游客和懂化验的客人；标纯酿但品质如实，懂化验的客人；实际 ≥ 4 级且标低一级，没人能识破；贸易板识破任何不实标签。被识破按实际品质的勾兑酒结算；没被识破卖出的不实标签累积欺诈罪名。
- 酿造与销售的说明收录在进阶化学手册里。

## 六、高级韧棉织物

金色的韧棉织物：孔径 150，耐久 1000，每天回满。和原版高级滤水器同源：凡是出售或掉落高级滤水器的地方，都会附带一块。

---

## 七、核废料后处理

把原版的核废料桶撬开，用化学手段把乏燃料后处理成钚，再把裂变产物固化封存。步骤也收录在进阶化学手册里。

1. **开桶**：用撬棍对准原版核废料桶，得到“打开的核废料”，里面装着 500 毫升乏燃料。打开的桶只能放在主物品栏、展示柜和里/外柜台。往外倒时只有钚萃取物和会蒸发的液体（水、酒精、乙酸乙酯等）能出来，放射性物质、酸和其他杂质都留在桶里。
2. **溶解**：倒入约 500 毫升浓酸，保持酸占桶内液体 98% 以上。桶里有会蒸发的液体时，衰变热让它保持在 70°C；乏燃料每次结算溶解 5%，一整桶得到 2 毫升溶解的钚和 50 毫升裂变产物。
3. **调整价态**：加入还原剂，用量约为钚的 10 倍（约 20 毫升）。
4. **萃取**：倒入乙酸乙酯，用量至少为裂变产物的 2 倍（约 100 毫升，建议一次加够），钚进入有机相成为钚萃取物。
5. **分离**：把桶倒进干净的容器，只有钚萃取物和会蒸发的液体会流出。
6. **提钚**：把倒出来的液体加热到 100°C 以上，蒸干水、乙酸乙酯和酒精，剩下纯钚。
7. **钚安瓿**：在化学制剂成型机里，把纯度 90% 以上的钚灌装成钚安瓿（每支 0.1 毫升）。把安瓿拖到任意饮料或葡萄酒上，变成价值 +50 的“核子饮料”。核子饮料不算违禁品，但只有上层区客人和成瘾者会买。
8. **固化**：倒尽桶内会蒸发的液体后，衰变热让桶升到 300°C 以上。此时加入水玻璃（或硅胶），用量与桶内放射性物质 1:1（约 50 毫升），裂变产物熔进玻璃，桶变成无害的“玻璃固化体”，可以丢弃。

**辐射病**：只要过夜时还有没处理完的打开的核废料，辐射病就加重 5%（最多 25%），夜间报告会用红字提示。辐射病按百分比降低卖出价格，和原版成瘾惩罚一样；注射真品免疫宁可以缓解 5%。

**废料掮客**：从第 7 天起，每 7 天来一次，出售 1-3 桶核废料，每桶 50。

## 八、啤酒

把酿好的酒液在化学制剂成型机里灌装成 2 升一桶的**桶装啤酒**，再一份一份倒出来卖。

- **灌装**：化学制剂成型机选“桶装啤酒”，需要 2000 毫升含酒精的液体。灌装啤酒时成型机不会混入杂质。
- **品质**（任一项落到哪一档，整桶就是哪一档）：

| 指标 | 上等 | 普通 | 完全不能喝 |
|---|---|---|---|
| 酒精（占总量） | 5-8% | 8-12% | 其余 |
| 活酵母 | 0.1-1% | 低于 0.1% 或 1-2% | 2% 以上 |
| 残糖 | ≤4% | 4-8% | 8% 以上 |
| 蛋白质 | ≤1% | 1-2% | 2% 以上 |
| 杂质（占“水+杂质”） | ≤1% | 1-2% | 2% 以上 |
| 甲醇 | — | — | 0.1% 以上（并危害公共安全） |

  杂质指水、酒精、酵母、糖、蛋白质以外的所有成分。用基础水兑一般是普通，优质水或纯水可以做到上等。
- **倒酒**：每次双击桶倒出一份。常温下是一罐**罐装啤酒**（约 333 毫升，一桶 6 罐）；桶在通电冰箱冷藏区冷到 2-8°C 后是一杯**扎啤**（500 毫升，一桶 4 杯）。冰箱冷藏区要扩容两次才放得下 4×4 的桶。剩下不够一份时桶就空了。
- **价格**：罐装 25、扎啤 50、整桶 150（按剩余量折算）；上等 +40%，完全不能喝一文不值。罐装放冰箱有原版冰镇加成。扎啤放过一夜会跑气，只值一罐的钱。
- **顾客**：啤酒算饮料、酒精，也算水；常见的买水顾客会买普通及以上的啤酒。
- **做法参考**：3 个营养果（约 750 克）搅碎、加约 1% 的酸加热水解、用等量碱中和，先兑水到约 1.2 升，加一包酿酒酵母发酵两晚，再兑水到 2 升，酒精约 6.6%。

## 九、空心灯与失能剂

- **作物**：空心灯，一年生，生长进度 24，每次收获 8 颗（按健康度分小、中、大），返还一颗种子。营养目标 320、容差 50、消耗 60，用水 100，污染敏感度 1.15。自带“辣素产出 III”和“污染水源 I”。退休农夫每次来店带两颗种子。
- **辣椒素**：打碎只得到辣椒素，没有水和蛋白质。“辣素产出”1 级每颗中号 0.5 毫升，按等级的 0.75 次方增长，天生 3 级约 1.14 毫升；小号 ×0.6，大号 ×1.5。
- **辣酯**：容器里有辣椒素和酒精、温度 ≥ 50°C 时，每次结算转化 5% 的辣椒素（至少 200 微升），每份消耗 0.1 份酒精。酒精少于辣椒素的 4 倍时，转化的部分里有 (4 − 酒精/辣椒素) ÷ 4 变成物理污染物（可以过滤）。酒精 78°C 沸腾，所以实际在 50–78°C 操作。辣椒素和辣酯都不挥发，净水器去不掉。
- **一小瓶失能剂**：用蒸发器蒸干酒精后，在化学制剂成型机里压制，每瓶 2000 微升，必须干燥。基础价 28，纯度分档同原版（和梦尘一样）。合法商品，带医疗品和武器类型。
- **谁会买**：上层区（包括私人安保承包商）和治安部的所有顾客；雇佣兵（`mercenaryBuyer`、`minorMerc`、`criminalBuyerMerc`）当普通商品买。革命军拒收时说革命军台词，下层区、黑市、游客、卡特尔、教会拒收时说下层区台词。
- **冲减罪名**：卖给上层区或治安部时，冲减量 = 成交价 × 纯度（0–1）× 5，先扣治安档案里数值最大的那项罪名，扣到 0 再扣下一项；所有罪名清零后，剩下的每满 100 点减 1 点调查进度（不足 100 舍去）。成交后买家按“记录本来就干净 / 罪名还有剩 / 罪名清零 / 调查也放缓”说不同台词。每次冲减都会写进 Player.log。
- **一次收获**：健康度 90 以上时 8 颗都是大号，约 13.7 毫升辣椒素，约 7 瓶药用级失能剂，冲减约 1600 点罪名。

## 十、电解食盐水

整条线：盐矿石 → 打碎得到食盐 → 加水成盐水 → 电解机每晚电解成碱、氢气、氯气 → 两罐气体混合 → 加热板加热成氯化氢 → 溶进水里成盐酸。合计 1 毫升食盐 + 0.3 毫升水 → 1 毫升碱 + 1 毫升酸，不带化学污染物和重金属。

- **盐矿石**：单价 15，3×2 格。打电话向 GP 矿业订购：6 块（1 天送到）或 12 块（2 天送到，累计花满 350 解锁），和金属矿石共用快递员、批发价和 5 天冷却。任何搅拌机都能打碎，每块得到 80 毫升食盐、4 毫升物理污染物、3 毫升矿物质（模组少时按原版损耗）。
- **电解机**：杰克逊博士出售，售价 400；玩家还没有时每次都卖，有了之后每次 25% 概率。自带两个空气罐和一张使用说明。界面：电池组、通用模组（同水培机的模组格）、盐水容器、产物 A（氢气）/ 产物 B（氯气）、便签。每晚耗电 24，电解 240 毫升食盐（× 性能），每 1 毫升食盐耗 0.3 毫升水，生成 1 毫升碱（留在容器里）和各 0.5 毫升氢气、氯气（产物 × 0.5–1.5，随质量 −100 到 +100 线性变化）；效率降低耗电。缺盐、缺水、没电或气罐满时停机。产物 A 只收空罐和纯氢气罐，产物 B 只收空罐和纯氯气罐。图标按盐水槽和两个产物槽有没有东西变化（8 种）。
- **气罐**：独立物品，不是液体容器，最多装 5 升，可以同时装氢气、氯气、氯化氢。单价 25，退休化学家每次来店 20% 概率出售。拖到另一个气罐上合并气体（装不下的留在原罐）；右键“放气”清空。有自己的温度，冷却很慢。
- **合成氯化氢**：混合气罐可以放到加热板上（加热板至少要 1 个化学模组才能超过 99°C），温度 ≥ 120°C 时每秒把 10%（至少 1 毫升）的氢气和氯气按 1:1 燃烧成两倍体积的氯化氢。
- **溶成盐酸**：把装着氯化氢的气罐拖到有水的液体容器上，溶成原版的“酸”，最多溶到酸占液体总量的 40%（500 毫升水最多 333 毫升酸），溶不下的留在罐里。容器里有碱时会被中和，别往刚电解完的碱里倒。

## 兼容性

- 新增液体：`dream_toxin`，以及 `vce_calcite`、`vce_quartz`、`vce_quicklime`、`vce_calcium_carbonate`、`vce_water_glass`、`vce_silica_gel`；`ia_yeast_wine`、`ia_yeast_super`、`ia_yeast_red`、`ia_methanol`、`ia_ice`、`ia_acetic_acid`、`ia_ethyl_acetate`。
- 新增物品：`ia.blended_wine`（勾兑葡萄酒）、`ia.kotton_fabric_advanced`（高级韧棉织物）、`ia.wine_making_notes`、`ia.wine_selling_notes`；新增两个植物基因 `production_starch`、`production_sugar`。
- 原版营养果的淀粉和糖改由基因产生（总量不变）；原版酵母可以溶解进容器；原版冰箱冷冻区会让水结冰。
- 新增物品：`dream_toxin.injector`（注射剂）、`dream_toxin.formula`（梦魔菌毒素配方）、`vce.ore_notes`（矿石化学笔记）；新增一条化学制剂成型机配方。
- 原版金属矿石增加了化学成分，可以被搅碎。
- 核废料后处理：新增液体 `nc_spent_fuel`、`nc_pu_dissolved`、`nc_fission_products`、`nc_pu_extract`、`nc_plutonium`；新增物品 `nc.opened_waste`、`nc.vitrified_waste`、`nc.pu_ampoule`、`nc.reprocessing_notes`；新增顾客“废料掮客”和一条化学制剂成型机配方；撬棍可以撬开原版核废料，原版免疫宁可以缓解辐射病。
- Harmony 补丁：`ChemicalHelper.HandleTemperatureReaction`（所有新反应）、`MachineFridge.CoolZoneContainers`（冷藏和冷冻标记）、`ChemicalProductHelper.GradeProduct`（只对本 Mod 的注射剂使用医用纯度表）、`StoreClientListSpec.CreateRetiredChemist`（出售进阶化学手册）、`MaterialDirectory.MetalOre`（矿石成分）、`ChemicalHelper.BlendInto`（矿石搅碎条件）。
- 其他 Mod 可以往进阶化学手册里加章节：在自己的本地化文件里写 `vce_handbook_title`（章节标题）和 `vce_handbook_line_1`、`vce_handbook_line_2`…（逐行内容，读到第一个缺失的键为止），无需写代码。
- 空心灯：新增液体 `vce_capsaicin`、`vce_lantern_ester`；新增物品 `vce_chili_seed`、`vce_chili`、`vce_incapacitant_vial`、`vce.lantern_notes`；新增基因 `production_vce_capsaicin` 和一条化学制剂成型机配方。Harmony 补丁：`StoreClient.IsClientRefusingItem`、`StoreClient.IsClientBuyingThisItem`（谁买失能剂）、`SecData.CheckSoldContraband`（冲减罪名，并把这笔交易的成交台词换成专属台词）、`PlayerStore.SellItemFromTable`、`PlayerStore.AcceptBarter`（还原没播出来的专属台词）。卖失能剂会直接改动治安档案里的罪名和调查进度。
- 电解食盐水：新增液体 `vce_salt`；新增物品 `vce_salt_ore`、`vce_gas_canister`、`vce_electrolyzer`、`vce.electrolysis_notes`。Harmony 补丁：`PhoneDialogList.MiningRegular`、`StoreClientListSupplier.CreateCourrierMining`（订购盐矿）、`MachineHeatingPlate.HeatingPlate`（加热板也收气罐）、`ItemContextHandler.SetButtonColor`（气罐右键显示“放气”）、`GeneralHelper.SetItemOwned`（买下电解机时里面的气罐和说明书一起归玩家）、`StoreClientListSpec.CreateRetiredChemist`（卖空气罐）。
- 免疫宁：`ConsumableHelper.InitImmunivax`，有辐射病时非哨兵开局也能注射。
- 不申请额外权限，不读写文件。
- 卸载前请先处理掉含有本 Mod 液体的容器、注射剂、钚安瓿、打开的核废料和配方便条。

## 开发者调试（调试面板的 Modding 分类）

- **Dream Toxin: test kit / pre-mixed bottle (2% acid) / formula note**：毒素测试用。
- **Ore Chem: ore kit**：2 块金属矿石、2 个空瓶、1 瓶酸、1 瓶碱。
- **Ore Chem: quicklime reagent / silica gel reagent**：现成的生石灰、硅胶试剂。
- **Ore Chem: notes**：一张矿石化学笔记。
- **Chem: advanced chemistry handbook**：一本进阶化学手册。
- **Starch Genes: test kit / starch disk / log nutrifruit sizes**：淀粉基因测试用。
- **Alcohol: ingredients kit / hydrolyzed mash / 9% wash / run one night**：工业酒精测试用（run one night 会让所有机器和容器多走一晚）。
- **Wine: flavour kit / blend base + ester / perfect blended wine / both notes**：勾兑葡萄酒测试用。
- **Filter: advanced Kotton fabric**：一块高级韧棉织物。
- **Nuclear: waste barrel + crowbar / reagent kit / plutonium ampoule / radiation +5% / merchant tomorrow**：核废料后处理测试用。
- **Beer: premium keg / 2 L wort to press (5.5%) / chill held keg**：啤酒测试用。
- **Lantern: 2 seeds + 3 chilis / capsaicin in 4x alcohol / 2 pharma-grade incapacitant vials**：空心灯测试用。
- **Salt: 3 salt ores**：3 块盐矿石。
- **Electrolysis: electrolyzer + battery + brine / run one night**：电解机测试用（带电池、气罐和说明书，外加一瓶 160 毫升食盐 + 100 毫升水的盐水；run one night 让所有电解机立刻跑一晚）。
- **Gas: empty / full hydrogen / full chlorine / hydrogen + chlorine canister**：各种气罐。

## 更新记录

- **1.7.0**：新增钚化学电池（杰克逊博士出售，能出普通电池的地方也约有十分之一机会出现；200 千焦，每拖入一支钚安瓿每晚自充 +2 千焦，最多 10 支）。快克因回头客重做：当天卖出纯度 90% 以上的快克因，第二天来一位快克因买家，预算连续天数算在店上。所有原版涡轮加速机都能给电解机加速。打开的核废料也能放在里柜台。扎啤和跑气扎啤贴图改为透视。

- **1.6.1**：修复快克因成盐时每次结算向上取整多扣酸，50 mL 生物碱 + 5 mL 酸反应完还剩约 137 µL 生物碱；现在严格按 10:1 反应。

- **1.6.0**：新增电解食盐水：GP 矿业电话订购盐矿石，任何搅拌机都能打碎成食盐；电解机（杰克逊博士出售，自带两个空气罐和使用说明）每晚把食盐水电解成碱、氢气和氯气；气罐可以合并气体、右键放气，放到加热板上加热到 120°C 以上把氢气和氯气烧成氯化氢，拖到有水的容器上溶成最多 40% 的盐酸；退休化学家偶尔出售空气罐；手册新增“电解食盐水笔记”。新柯的“生物碱产出”天生等级从 II 提到 III（存档里已有的新柯种子读档时一次性升级）。修复：得了辐射病后，非“哨兵”开局的玩家右键免疫宁无法注射（真品每支缓解 5%，假货无效并提示）；失能剂卖给上层区和治安部时专属台词不显示（现在顶替原版的成交台词，每笔都会说）；钚安瓿拖不进葡萄酒（原版酒瓶会拒绝不能往里灌的东西，现在对钚安瓿放行）。

- **1.5.0**：新作物空心灯（一年生实验室辣椒，自带“辣素产出 III”，退休农夫带种子）；辣椒素加 4 倍酒精在 50–78°C 加热成辣酯，蒸干后压成“一小瓶失能剂”（2 毫升，基础价 28，合法，医疗品兼武器）。只有上层区和治安部会买，雇佣兵当普通商品买，其他人拒收并说明原因；卖给上层区和治安部时冲减罪名，罪名清零后再减调查进度，买家有专属台词。手册新增“失能剂制取笔记”；化学家开局直接送一本进阶化学手册；新封面。

- **1.4.0**：新增啤酒：化学制剂成型机灌装 2 升桶装啤酒（不混入杂质），按酒精、活酵母、残糖、蛋白质、杂质分为上等/普通/完全不能喝三档；双击桶每次倒出一罐，冷藏到 2-8°C 后倒出扎啤；扎啤放一夜会跑气；啤酒也算水，常见买水顾客会买；手册新增“啤酒酿造笔记”和“啤酒品质表”；新封面。

- **1.3.1**：修复系统语言和游戏语言不一致时（如德语系统玩英文游戏、中文系统玩英文游戏），原版水质标签、液体名称和物品特征分类名停留在系统语言，导致水卖不出去、液体名显示错误或空白的问题。原因是 mod 加载时 Harmony 打补丁会提前触发原版这些文字的翻译（所有打补丁的 mod 都会这样），现在本 Mod 会在游戏语言设好后把它们重新翻译一遍；液体、配方、基因和酒标也改为在物品目录初始化时注册。日志里会写 language check 两行，便于排查。搅拌机：安装本 Mod 之前就存在于存档里的金属矿石，读档时补上可打碎的设定（以前会被搅拌机直接跳过）；模组数判断改用搅拌机自己计算的损耗值，更可靠；矿石没被打碎时会提示需要几个化学模组、这台现在有几个。修复进阶化学手册的背景、翻页和关闭按钮显示成蓝色方块的问题（游戏本体缺少纸张素材目录，现在由本 Mod 自带这些素材）。

- **1.3.0**：并入核废料后处理（原测试版 `nuclear_chem_test`，安装时会自动删除；测试版存档里的辐射病等级不会保留）：撬开核废料、浓酸溶解、还原剂调整价态、乙酸乙酯萃取钚、灌装钚安瓿做核子饮料、水玻璃固化裂变产物、辐射病、废料掮客。步骤收录在进阶化学手册里。修复：梦魔菌毒素注射剂被游戏判定为致命品，卖给会反馈的顾客后可能出现“害死了朋友”并扣声望和记危害罪（毒素毒性从 4.0 降到 0.1，一支 5 毫升注射剂远低于有毒线；残留的梦魇帽菇萃取液仍按原版毒性计算）；钚的毒性从 8 降到 5，一支钚安瓿不会让核子饮料变成有毒品；修复勾兑葡萄酒带着空酒瓶的“日用品”类型（读档时自动修正已有的勾兑酒）。酵母发酵速度提高到原来的 3 倍（每毫升酵母每晚 24 毫升糖），并新增模组设置“酵母发酵速度”；糖变酒精改为按体积 1:1（原来 1:0.5），营养果酿酒的产量翻倍。没有进阶化学手册时，杰克逊博士的商店也总会出售一本；上层区顾客拒绝勾兑酒、非上层区顾客拒绝核子饮料时会说明原因；打开的核废料可以摆进里/外售货区；手册里的百分比都写明了是占什么的比例（如“占液体总量1%的酸”）。

- **1.2.1**：所有“想要的”反应速率提高到每次结算至少 5%：梦魔菌毒素生成、室温下碱的苛化、浓碱中重金属沉淀原来都是 2%；冰箱降温速度默认提高到原版的 4 倍，可在模组设置里调整；修复干馏产生的甲醇让干馏时断时续的问题，甲醇现在直接以蒸气逸出；所有字条不再单独出售，合并为“进阶化学手册”，只要你还没有，退休化学家每次来店都会出售；勾兑酒酒精不足 5% 时最多只能评为不可饮用；其他 Mod 可以通过本地化键往手册里加章节。
- **1.2.0**：新增淀粉基因、工业酒精（酸水解、酵母过夜发酵、甲醇、冷冻浓缩）、乙酸乙酯与勾兑葡萄酒（双标签、品质与欺诈机制）、高级韧棉织物；成分分析仪显示温度；模组设置可调整水的沸腾速度；退休化学家出售两张勾兑酒便条。
- **1.1.1**：修复新开游戏时梦魔菌毒素、矿石液体和注射剂配方显示为代号（如 `injector_name`）的问题，之前只有读档时名称才会刷新。
- **1.1.0**：新增矿石化学：金属矿石可以在装满 4 个化学模组的搅拌机里搅碎；新增生石灰、硅胶等 6 种液体和配套反应，用于提纯酸和碱；冰箱冷冻区参与反应；新增“矿石化学笔记”，由退休化学家出售。
- **1.0.0**：首次发布，包含梦魔菌毒素。

---

## English

An expansion built on the vanilla chemistry system, without changing vanilla products or reactions.

**Dream Toxin**: mix Dream Cap Extract and Nightmare Cap Extract 1:1 with 0.5–5% acid and keep it refrigerated (2–8 C or in a powered fridge chill zone). It is heat-stable, so leftover extract can be burned off above 100 C. Press it into Dream Toxin Injectors in the Chem Finisher: a legal cosmetic medical product (base value 150) that is only worth anything at pharma grade (90%+). The retired chemist sells the formula.

**Ore chemistry**: metal ore can be crushed in a blender with all 4 chem modules into calcite, quartz sand and impurities (amounts vary per ore). Kotton fabric filters out only the coarse quartz; water filters catch all fine solids.

- **Purifying base**: calcine calcite (dry, 150 C+) into quicklime and add it to base (50%+). Quicklime causticizes chemical contaminant back into base plus calcium carbonate (faster at 80 C+); heavy metal settles out by itself. Filter with a water filter.
- **Purifying acid**: dissolve quartz in base at 120 C+ to get water glass, filter, then add acid to precipitate silica gel. In acid (80%+), silica gel drives chemical contaminant off as gas at 100 C+ (1 gel per 4 contaminant); freezing then removes heavy metal at the cost of an equal amount of acid. Filter with a water filter.
- All steps are in the **Advanced Chemistry Handbook**, which the retired chemist and Dr. Jackson sell whenever you don't own one.

**Starch genes**: nutrifruit's starch and sugar now come from innate Starch Production (rank 4) and Sugar Production (rank 1) genes with vanilla totals, so they can be extracted onto DNA disks and spliced into other crops.

**Industrial alcohol**: hydrolyse starch with acid at 90 C+, neutralise, cool, and dissolve yeast into the container; yeast grows and ferments overnight at 20-35 C. Microbes in a fermenting mash become methanol (boils at 64.7 C). Water freezes in the fridge freezer for freeze concentration up to 25%. The analyzer shows container temperature, and the mod settings can change the water boiling and yeast fermentation speeds (by default each mL of yeast ferments 24 mL of sugar into 24 mL of alcohol per night).

**Blended wine**: dry-distil plant matter into acetic acid, esterify it with alcohol into ethyl acetate, and press alcohol + water + ethyl acetate into a wine bottle in the Chem Finisher. Its quality follows vanilla wine tiers; it has two labeler-editable labels (quality and genuine/blended) and a set of rules for who sees through which lie. The Advanced Chemistry Handbook explains it all.

**Advanced Kotton fabric**: a gold Kotton fabric that regains all its durability every day, sold and found wherever the advanced water filter is.

**Beer**: the Chem Finisher fills 2 L of alcoholic liquid into a Beer Keg (no finisher impurity), graded premium, ordinary or undrinkable by alcohol (premium 5-8%), live yeast (0.1-1%), sugar (up to 4%), protein (up to 1%) and impurities (up to 1% of water + impurities; base water usually gives ordinary). Each double-click pours a can (6 per keg), or a draft beer (4 per keg) once the keg is chilled to 2-8 C in a powered fridge chill zone. Draft beer goes flat overnight. Beer counts as water, so regular water buyers take it.

**Nuclear waste reprocessing**: pry open vanilla nuclear waste with a crowbar (500 mL spent fuel, main inventory, showcase and inner/outer counters only). Dissolve it in about 500 mL of concentrated acid (keep acid above 98%; decay heat holds the barrel at 70 C), add reducing agent (about 10x the plutonium), then ethyl acetate (at least 2x the fission products). Only the plutonium extract and evaporative liquids pour out; boil them off above 100 C to get plutonium, and press 90%+ plutonium into Plutonium Ampoules in the Chem Finisher. An ampoule turns any drink or wine into a Nuclear drink worth +50, bought only by Upper Level clients and addicts. Once the barrel runs dry it heats past 300 C; add water glass (or silica gel) 1:1 with the radioactive contents to vitrify it into harmless waste. Every night with unprocessed opened waste adds 5% radiation sickness (max 25% sell penalty), eased by genuine Immunivax. A Waste Broker sells 1-3 barrels at 50 each every 7 days from day 7.

Other mods can add a handbook section with the localization keys `vce_handbook_title` and `vce_handbook_line_1`, `vce_handbook_line_2`, ... (read until the first missing key); no code needed.

No extra capabilities. Remove items containing this mod's liquids before uninstalling.
