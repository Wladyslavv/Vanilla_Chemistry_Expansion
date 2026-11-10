# 空心灯 / Hollow's Lantern — 设计记录

英文名取自 Outer Wilds 里绕碎空星（Brittle Hollow）运行的火山卫星 Hollow's Lantern。
英文物品名：Hollow's Lantern Seeds / Small Hollow's Lantern / Hollow's Lantern / Large Hollow's Lantern。

一种赛博朋克世界观下的实验室辣椒。提纯后的镇暴剂只能合法卖给上层区和治安部，下层区和革命军拒收；卖给前两者会冲减罪名和证据。

## 作物（已定）

| 项目 | 数值 |
|---|---|
| 生长进度 | 24 |
| 产量 | 8（按健康度分小/中/大） |
| 养分（目标/容差/消耗） | 320 / 50 / 60 |
| 污染敏感度 | 1.15 |
| 用水 | 100 |
| 收获后 | 返还种子，一年生 |
| 自带基因 | 辣素产出 ×3、污染水源（polluting）×1 |
| 种子来源 | 退休农夫，和新柯种子一样每次来店带 |
| 产出物 | 合法；提纯得太久，几乎不能当调料用 |

不做：复杂基因组、辣水副产物、徒手处理刺痛。

## 贴图（已定）

`art/chili/`，由 `DrawChili.cs` 生成：`vce_chili_seed`、`vce_chili_s`、`vce_chili`、`vce_chili_l`、`vce_chili_p`、`vce_chili_p2`。
紫红色辣身，深紫叶子，绿色萼片完全包住头部。

## 文案（草稿）

定稿后写进 `Localization/zh.csv`、`en.csv`。

| 用途 | 中文 | English |
|---|---|---|
| 作物名 | 空心灯 | Hollow's Lantern |
| 种子 | 空心灯种子 | Hollow's Lantern Seeds |
| 种子说明 | 实验室培育的辣椒种子，产量高，但很吃营养液。根系会弄脏种植水。 | A lab-bred chili seed. High-yielding but hungry for nutrients, and its roots foul the water. |
| 产出（小/中/大） | 小空心灯 / 空心灯 / 大空心灯 | Small Hollow's Lantern / Hollow's Lantern / Large Hollow's Lantern |
| 产出说明 | 实验室里提纯的辣椒。在酒精中加热会产出化学刺激剂，常用于高烈度的镇暴行动。 | A laboratory-refined chili. Heated in alcohol it gives off a chemical irritant, used in high-intensity riot control. |
| 产出底部小字 | 因为长时间的提纯，已经几乎无法当调味料使用了。不过空间站里大多数人反正也买不起调味料。 | Refined for so long it is barely usable as a seasoning anymore. Not that most people on the station could afford seasoning anyway. |

## 制取路线（已定方向，数值为草案）

1. **打碎**：空心灯用搅拌机打碎，只得到**辣椒素**，没有水和蛋白质。产量由“辣素产出”基因决定：1 级每颗中号 0.5 毫升，按等级的 0.75 次方增长，天生 3 级约 1.14 毫升（小号 ×0.6，大号 ×1.5）。8 颗中号约 9 毫升，够做约 5 瓶药用级失能剂。
2. **加酒精**：至少加辣椒素体积 4 倍的酒精。
3. **加热到 50°C 以上**：每次结算转化一部分辣椒素（草案：5%，至少 200 微升），每份辣椒素消耗 0.1 份酒精，生成 1 份**辣酯**。
   - 酒精不足 4 倍时，这次转化的一部分会变成**物理污染物**：比例 = (4 − 酒精/辣椒素) ÷ 4。例如只有 2 倍酒精时，一半变成污染物。
   - 酒精 78.37°C 沸腾（原版设定），超过这个温度酒精会被烧掉，倍率随之下降。所以实际操作窗口是 **50–78°C**。
   - 物理污染物颗粒大，可以过滤掉。
4. **蒸发器**：蒸掉酒精，剩下干燥的辣酯。没反应完的辣椒素会留下来，拉低纯度。
5. **化学制剂成型机**：压成**一小瓶失能剂**，要求干燥。

辣酯（`vce_lantern_ester`，英文 Lantern Ester）：不挥发，净水器去不掉，是失能剂的有效成分。
这是本 mod 自造的化合物名，不对应现实中的任何镇暴剂。

## 一小瓶失能剂（已定）

全部以原版梦尘为参照（数值取自游戏代码 `ChemFinisherRecipe`、`MedsItemDirectory.DreamDust`）：

| 项目 | 梦尘（原版） | 一小瓶失能剂 |
|---|---|---|
| 成型体积 | 2000 微升，要求干燥 | **2000 微升**，要求干燥 |
| 基础价 | 18 | **28** |
| 违禁等级 | 2 级（中级溢价 +70%） | 不是违禁品，没有溢价 |
| 街头标准时实际价 | 18 × 1.70 = 30.6 | 28（约为梦尘的 92%，略低） |
| 纯度门槛 | 原版通用纯度分档 | 同梦尘，直接用原版分档 |
| 物品类型 | 医疗品、违禁药物 | **医疗品（`MEDICAL`）+ 武器（`WEAPON`）** |

不是违禁品，所以有武器类型也不会算成武器走私（原版只有带违禁标签的物品才记走私罪名）。

原版纯度分档（`ChemicalProductHelper.GradeProduct`），两者共用：

| 纯度 | 档位 | 价格修正 |
|---|---|---|
| ≥ 99% | 实验室纯 | +135% |
| ≥ 90% | 药用级 | +85% |
| ≥ 80% | 接近药用 | +40% |
| ≥ 60% | 街头标准 | 0 |
| ≥ 45% | 掺假货 | −45% |
| ≥ 30% | 垃圾货 | −75% |
| 更低 | 淀粉货 | −95% |

## 售卖与冲减证据（已定）

只有**上层区**（`FACTION_UPPER_LEVEL`）、**治安部**（`FACTION_SECURITY`）和**雇佣军**买，其他都拒收。

**雇佣军**没有自己的派系，按顾客 identifier 认：`mercenaryBuyer`、`minorMerc`（挂下层区）和 `criminalBuyerMerc`（挂黑市）。这三种原本就收武器，`criminalBuyerMerc` 还收医疗品，所以靠物品类型就会买。雇佣军按普通商品成交：**没有特殊对话，不冲减证据，也不影响调查进度**。他们虽然挂在下层区，但不说下层区的拒收台词。另外两种雇佣军（`merc_gun_buyer` 订枪、`merc_gun_seller` 卖枪）不收普通商品，不受影响。

**革命军**（`FACTION_REVOLUTION`）拒收时说革命军台词；**下层区**（`FACTION_LOWER_LEVEL`）以及**黑市、游客、卡特尔、教会**（`FACTION_BLACK_MARKET`、`FACTION_TOURIST`、`FACTION_CARTEL`、`FACTION_CHURCH`）拒收时都说下层区那句台词（沿用 `RefusalLines.cs` 的做法，把货放上柜台时就说）。文案见下面“对话文案”。

卖给上层区和治安部时：

1. **证据减少量** = 成交价 × 纯度 × 5。纯度按小数算（90% 记 0.9），结果取整。
   例：药用级 90%，成交价 28 × 1.85 ≈ 52，减少 52 × 0.9 × 5 = 234 点证据。
   - “证据”指治安档案里各项罪名的累计值（`SecData.crimeAmount`，合计就是 `GetTotalCrimeValue()`）。
   - 扣减顺序：先扣当前数值最大的那项罪名，扣到 0 后再扣剩下最大的那项，依次类推，直到减少量用完或所有罪名都为 0。
2. **溢出部分减调查进度**：证据减到零后还没用完的部分，每满 100 点减 1 点调查进度（`SecData.evidenceLevel`，0–100），不足 100 的部分舍去，调查进度最低到 0。
   例：证据只剩 50，这次要减 234，溢出 184，调查进度减 1。
3. **特殊对话**：成交后弹出专门的对话，上层区和治安部各一套，按这次冲减的结果分四种情况，文案见下面“对话文案”。
   - 实现：专属台词**顶替原版的成交台词**，播放顺序是“专属台词 → 原版后续对话（还要别的吗 / 告别）”，每一笔失能剂交易都会说。原版每位顾客的成交台词只播一次，之后直接接后续对话；所以每笔都临时换上一句新的成交台词，带上原版的“接后续对话”动作，开始播放时立刻还原。
   - 只顶替普通顾客的成交台词（随机通用句、后面不接别的对话、不带动作）；剧情顾客保持原版，这一笔不说专属台词。没能播出来的（当时有更重要的对话）会在下一笔交易前还原。
   - 1.5.0 的做法是在原版对话开始后再插一句，会打断原版的对话链，实测台词没有显示，1.6.0 改成现在的做法。

## 对话文案（草稿）

定稿后写进 `Localization/zh.csv`、`en.csv`。都是客人说的一句话，和原版、本 mod 现有拒收台词一样短。

### 成交（上层区、治安部）

一次成交只说一句，按冲减结果选：

| 情况 | 判定 |
|---|---|
| 无事可减 | 卖之前罪名合计为 0，调查进度也是 0 |
| 证据还有剩 | 扣完后罪名合计仍大于 0 |
| 证据清零 | 罪名扣到 0，调查进度没变（溢出不足 100，或调查进度本来就是 0） |
| 调查也放缓 | 罪名扣到 0，调查进度至少减了 1 |

| key | 中文 | English |
|---|---|---|
| `lantern_sale_upper_clean` | 货不错。你的记录也挺干净，继续保持。 | Good stock. Your record's clean, too. Keep it that way. |
| `lantern_sale_upper_partial` | 庄园的保安一直缺这个。我会跟治安部的朋友提一句你的名字。 | Our estate guards have been short on these. I'll mention your name to a friend at Security. |
| `lantern_sale_upper_cleared` | 我打了个电话。你在治安部的档案，现在干干净净。 | I made a call. Your file at Security is clean now. |
| `lantern_sale_upper_slowed` | 档案清了，调查那边我也让人放慢些。上层区记得谁帮过它。 | Your file is clean, and I've asked them to slow the investigation. The Upper Level remembers who helps it. |
| `lantern_sale_security_clean` | 装备科会喜欢这批货的。你一直很守规矩，这样很好。 | Supply will like this batch. You've always kept your nose clean. Good. |
| `lantern_sale_security_partial` | 这批货我们收了。你档案里有几页，好像找不到了。 | We'll take this batch. A few pages of your file seem to have gone missing. |
| `lantern_sale_security_cleared` | 你的档案？我们这儿没有你的档案。 | Your file? We don't have a file on you. |
| `lantern_sale_security_slowed` | 档案没了，调查组那边我也去打个招呼。下批货还找你。 | No file, and I'll have a word with the investigators. We'll come to you for the next batch. |

### 拒收（`refuse_lantern_lower` 用于下层区、黑市、游客、卡特尔、教会；`refuse_lantern_revolution` 只用于革命军）

| key | 中文 | English |
|---|---|---|
| `refuse_lantern_lower` | 这玩意儿是拿来往我们脸上喷的，你还想让我买回家？ | That's the stuff they spray in our faces. And you want me to take it home? |
| `refuse_lantern_revolution` | 上周广场上喷我们的就是这个。你最好想清楚自己站哪边。 | This is what they sprayed on us in the square last week. Better figure out whose side you're on. |

注意命名：游戏代码里的 `evidenceLevel` 实际是界面上的调查进度（满 100 派人上门），而本设计里说的“证据”是罪名累计值 `crimeAmount`，不要搞混。

## 待定

- 辣椒素、辣酯在游戏里的显示名是否还要再改
- 要不要出大瓶
- 冲减要不要设每日上限
- 私人安保承包商（`privateSecurityContractor`）属于上层区，收武器，所以会买失能剂并按上层区结算（冲减证据、说上层区成交台词）。要不要给他单独的台词
- 对话文案定稿
- 手册章节
