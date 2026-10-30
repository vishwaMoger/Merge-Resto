<p align="center">
  <img width="100%" src="https://capsule-render.vercel.app/api?type=waving&color=0:FF9A8B,50:FF6A88,100:FFD86F&height=280&section=header&text=%F0%9F%A5%90%20MERGE%20RESTO&fontSize=72&fontColor=ffffff&fontAlignY=38&stroke=ffffff&strokeWidth=1&desc=Restore%20Grandma%27s%20Bakery%20%E2%80%A2%20Build%20a%20Cozy%20Food%20Empire&descSize=22&descAlignY=60&animation=twinkling" alt="Merge Resto" />
</p>

<p align="center">
  <img src="https://readme-typing-svg.demolab.com?font=Fredoka&weight=600&size=24&duration=2800&pause=700&color=FF6A88&center=true&vCenter=true&width=760&lines=%F0%9F%94%A7+Merge+tools.+%F0%9F%A7%B9+Fix+the+shop.+%F0%9F%8D%9E+Bake+the+bread.;%F0%9F%91%A9%E2%80%8D%F0%9F%8D%B3+Hire+managers.+%F0%9F%92%B0+Earn+while+you+sleep.;%F0%9F%8D%95+Bakery+%E2%86%92+Caf%C3%A9+%E2%86%92+Pizzeria+%E2%86%92+Factory!" alt="tagline" />
</p>

<p align="center">
  <img src="https://img.shields.io/badge/Unity-6.3-000000?style=for-the-badge&logo=unity&logoColor=white" />
  <img src="https://img.shields.io/badge/Render-URP-FF6A88?style=for-the-badge&logo=unity&logoColor=white" />
  <img src="https://img.shields.io/badge/C%23-Gameplay-512BD4?style=for-the-badge&logo=dotnet&logoColor=white" />
  <img src="https://img.shields.io/badge/Platform-Web%20%7C%20PC%20%7C%20Mobile-FF9A8B?style=for-the-badge&logo=webgl&logoColor=white" />
  <img src="https://img.shields.io/badge/Genre-Merge%20%2B%20Idle%20Tycoon-FFD86F?style=for-the-badge&logoColor=black" />
  <img src="https://img.shields.io/badge/Status-In%20Development-2ea44f?style=for-the-badge" />
</p>

---

<h2 align="center">🍰 THE PITCH</h2>

<table align="center"><tr><td align="center">
<br/>
<b>Grandma's bakery-café has been closed for 20 years.</b><br/>
You inherit it — dusty, broken and boarded up.<br/><br/>
<b>Merge</b> tools and ingredients to repair it, <b>bake</b> your way to the Grand Opening,<br/>
then <b>expand</b> into a café, patisserie, pizzeria, delivery hub and a full food factory.<br/><br/>
<sub>🐶 With <b>Biscuit</b>, Grandma's fluffy dog and the game's mascot, cheering you on.</sub>
<br/><br/>
</td></tr></table>

---

<p align="center">
  <img width="100%" src="docs/readme/pillars.svg" alt="Design pillars: Cute and Cozy, Run-down to Wow, Merge plus Idle, Always a Next Goal" />
</p>

---

<h2 align="center">🔁 CORE LOOPS</h2>

```mermaid
flowchart LR
    M["🔀 MERGE<br/>tap generators<br/>merge items"]:::merge --> R["🔧 RESTORE<br/>repair stations<br/>raise coziness"]:::restore
    R --> P["🍞 PRODUCE<br/>stations · workers<br/>customers"]:::produce
    P --> C(("💰 COINS")):::coins
    C --> U["⬆️ UPGRADE<br/>speed · output<br/>managers"]:::produce
    U --> P
    C --> E["🗺️ EXPAND<br/>buy the next shop"]:::expand
    E --> M
    classDef merge fill:#FF6A88,stroke:#fff,color:#fff
    classDef restore fill:#FF9A8B,stroke:#fff,color:#fff
    classDef produce fill:#F7B267,stroke:#fff,color:#000
    classDef coins fill:#FFD86F,stroke:#fff,color:#000
    classDef expand fill:#9B5DE5,stroke:#fff,color:#fff
```

| Loop | What you do | Time scale |
|:--|:--|:--|
| 🔀 **Merge** | Tap generators, merge items, deliver them to repair tasks | seconds → minutes |
| 🍞 **Production** | Load supplies, serve, upgrade stations, hire managers to automate | minutes |
| 🔧 **Restore** | Finish areas, unlock stations, raise coziness | minutes → hours |
| 🗺️ **Expand** | Buy the next shop, open new business types on the world map | days |

---

<h2 align="center">🏪 THE SHOP LADDER</h2>

```mermaid
flowchart LR
    A["🥖 Grandma's<br/>Bakery"]:::s1 --> B["☕ Corner<br/>Café"]:::s2 --> C["🎂 Patisserie"]:::s3 --> D["🍕 Nonno's<br/>Pizzeria"]:::s4 --> E["🛵 Delivery<br/>Hub"]:::s5 --> F["🚉 Station<br/>Food Court"]:::s6 --> G["🏭 Central<br/>Kitchen Factory"]:::s7
    classDef s1 fill:#FFD86F,color:#000,stroke:#fff
    classDef s2 fill:#F7B267,color:#000,stroke:#fff
    classDef s3 fill:#FF9A8B,color:#fff,stroke:#fff
    classDef s4 fill:#FF6A88,color:#fff,stroke:#fff
    classDef s5 fill:#E0559A,color:#fff,stroke:#fff
    classDef s6 fill:#9B5DE5,color:#fff,stroke:#fff
    classDef s7 fill:#5E4AE3,color:#fff,stroke:#fff
```

<p align="center"><sub>Old shops keep earning idle income · locked shops tease you as dusty silhouettes · late-game <b>franchise prestige</b> for a permanent multiplier</sub></p>

---

<h2 align="center">⚙️ PRODUCTION CHAINS</h2>

```mermaid
flowchart LR
    subgraph BREAD["🍞 BREAD"]
      direction LR
      f1[Flour] --> f2[Mixer] --> f3[Proofing] --> f4[Oven 🔥] --> f5[Display] --> f6[Cashier 💰]
    end
    subgraph COFFEE["☕ COFFEE"]
      direction LR
      c1[Beans] --> c2[Grinder] --> c3[Espresso ♨️] --> c4[Barista] --> c5[Counter 💰]
    end
    subgraph PIZZA["🍕 PIZZA"]
      direction LR
      p1[Dough] --> p2[Sauce] --> p3[Toppings] --> p4[Wood Oven 🔥] --> p5[Box] --> p6[Deliver 🛵]
    end
```

<p align="center"><sub>Every station has a progress ring · 3 upgrades × 5 levels (speed · output · extra machine) · a manager to automate it</sub></p>

---

<p align="center">
  <img width="100%" src="docs/readme/merge-chains.svg" alt="Merge chains: repair, cleaning, decor, bakery and pizza item tiers" />
</p>

---

<h2 align="center">🏚️ ➜ 🏡 THE REPAIR MOMENT</h2>

```mermaid
stateDiagram-v2
    direction LR
    Covered: 📦 Covered
    Broken: 💥 Broken
    Working: ✅ Working
    Worn: 🌫️ Worn
    Polished: ✨ Polished
    [*] --> Covered
    Covered --> Broken: remove cover
    Broken --> Working: merge-repair
    Working --> Worn: slowly gets dirty
    Worn --> Polished: clean up
    Polished --> Working: bonus ends
```

<p align="center">
<i>Camera glides over → dust puff → sheet flies off → object bounces → colour sweeps back from grey → sparkles & rewards burst out → a worker starts using it.</i>
</p>

---

<p align="center">
  <img width="100%" src="docs/readme/shaders.svg" alt="Custom URP shaders: toon lighting, outlines, colour sweep, rim light, wear, sway, bloom, tilt-shift, particles" />
</p>

---

<h2 align="center">🎓 ONBOARDING — THE GRAND OPENING</h2>

```mermaid
journey
    title First 8 minutes
    section Arrival
      Camera pans over the dusty shop: 5: Player
      First tap within 10 seconds: 5: Player
    section Learn
      First repair teaches merging: 5: Player
      Fix the oven and make a first sale: 5: Player
      First upgrade speeds up the oven: 4: Player
    section Celebrate
      Second station joins the chain: 4: Player
      Neon sign lights up, reward chest: 5: Player
```

---

<h2 align="center">🛠️ UNDER THE HOOD</h2>

```mermaid
flowchart TB
    subgraph SIM["🧠 Sim — pure logic, no visuals"]
      ShopSim --- ShopState
      MergeBoard --- MergeDefinitions
      ShopDefinitions --- DailyRewards
    end
    subgraph GAME["🎮 Game"]
      GameController --- SaveStore
      InputController --- AudioDirector
      AdService --- Platform
      Views --- GameUI[UI] --- World
    end
    subgraph LOOK["✨ Look"]
      ToonLookSettings --- BouncyWalker
      Spinner --- LoopMover
    end
    SIM ==> GAME ==> LOOK
```

- 🧠 **Simulation is separate from visuals.** Slow devices show less animation but never earn less.
- 📦 **Data-driven:** shops, rooms, chains, recipes, costs, timers and tutorial steps all live in config.
- 💾 **Versioned saves** with offline earnings from timestamps and cloud saves.
- 🌐 **Web-first build:** Brotli, high code stripping, compressed textures and stripped shader variants.
- 📈 **Pacing simulator:** an automated pacing report checks every upgrade gap, renovation and expansion time.
- 📊 **Analytics:** events for every tutorial step and key moment.

---

<h2 align="center">🎯 TARGETS</h2>

<p align="center">
  <img src="https://img.shields.io/badge/Gameplay%20Conversion-80%25%2B-FF6A88?style=for-the-badge" />
  <img src="https://img.shields.io/badge/Avg%20Playtime-10%2B%20min-F7B267?style=for-the-badge" />
  <img src="https://img.shields.io/badge/Day--1%20Retention-15--20%25-9B5DE5?style=for-the-badge" />
</p>

---

<h2 align="center">🗺️ ROADMAP</h2>

- [x] 🌐 Project setup: web target, platform SDK, build-size settings
- [x] 🎨 The look: isometric camera, toon shader, outlines, polished room
- [x] 🍞 Core idle loop: stations, workers, customers, coins, saves, offline earnings
- [ ] 🏚️ Run-down system: object states, wear shader, repair animation
- [ ] 🔀 Merge board: grid, chains, generators, energy, repair tasks
- [ ] 🎓 Grand Opening tutorial
- [ ] 🗺️ Progression: quests, managers, shops 2–3, world map
- [ ] 🍕 Nonno's Pizzeria and beyond

---

<h2 align="center">🚀 RUN IT</h2>

```bash
git clone https://github.com/vishwaMoger/Merge-Resto.git
```

1. Open the folder in **Unity 6000.3** (Unity 6.3) with **URP**.
2. Open the main scene in `Assets/Scenes`.
3. Press ▶️ **Play**.

📄 Full design: [`GAME_CONCEPT.md`](GAME_CONCEPT.md) · 📈 Pacing: [`Docs/PacingReport.md`](Docs/PacingReport.md)

---

<p align="center">
  <b>Made with ❤️ and lots of 🥐 by <a href="https://github.com/vishwaMoger">Vishwa Moger</a></b><br/>
  <sub>Game Developer · 3+ years · Bangalore, India</sub>
</p>

<img width="100%" src="https://capsule-render.vercel.app/api?type=waving&color=0:FFD86F,50:FF6A88,100:FF9A8B&height=120&section=footer" />
