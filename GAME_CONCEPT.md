# Merge Resto — Game Concept

*Working title. Unity 6.3 + URP, Web (CrazyGames) first, PC main target, mobile web second.*

## One-line pitch

Restore Grandma's run-down bakery-café by merging tools and ingredients, then watch it run itself and grow into a cozy food empire with cafés, a patisserie, a pizzeria and a factory.

## Pillars

1. **Cute and cozy.** Chibi characters, warm colors, soft humor, satisfying feedback on every tap.
2. **Run-down to beautiful.** Every new shop arrives dusty and broken; fixing it is the fun.
3. **Active merge, passive idle.** Merging is the hands-on part; the production chain earns while you watch or are away.
4. **Always a next goal.** A quest, an area progress bar and an affordable upgrade are always visible.

## Story and cast

- **Story:** Grandma's bakery-café has been closed for 20 years. You inherit it, dusty and broken, and bring it back for the Grand Opening. Later, Grandma's old friend Nonno asks you to revive his pizzeria across the street.
- **Grandma:** guide character in the tutorial, warm and encouraging.
- **Biscuit:** Grandma's fluffy dog, the game's mascot (icon, thumbnail, hint bubbles). Works across every business type.
- **Staff:** chibi humans (big heads, small bodies): baker, barista, cashier, cleaner, delivery rider, pizzaiolo.
- **Customers:** chibi humans with lots of outfit, hair and skin variety. Commuters, students, families.

*Character style is the current default (chibi humans + pet mascot); swap if we change direction.*

## Core loops

| Loop | What the player does | Time scale |
|---|---|---|
| **Merge** | Tap generators, merge items, deliver them to repair tasks | Seconds to minutes |
| **Production** | Tap to load supplies and serve, upgrade stations, hire managers to automate | Minutes |
| **Restore** | Finish areas, unlock new stations, raise coziness | Minutes to hours |
| **Expand** | Buy the next shop, open new business types on the world map | Days |

Coins come from the idle side and pay for upgrades, generators and new shops. Merge items pay for repairs and recipe boosts. Energy limits merge sessions and refills over time.

## Shop ladder

| # | Shop | New in this shop |
|---|---|---|
| 1 | **Grandma's Bakery** | Tutorial. Bread line, first repairs, first manager |
| 2 | **Corner Café** | Coffee line, dine-in seating, coziness score |
| 3 | **Patisserie** | Cakes, decorating station, special orders |
| 4 | **Nonno's Pizzeria** | New business type: pizza line, wood-fired oven, new merge chains |
| 5 | **Pizza Delivery Hub** | Scooters on the street, delivery routes and radius |
| 6 | **Train Station Food Court** | Bakery and pizza kiosks running side by side |
| 7 | **Central Kitchen Factory** | Conveyors, trucks supplying all other shops |

- Old shops keep earning idle income at a reduced rate. A world map switches between them.
- Locked shops are visible early as dusty teasers with a price goal.
- Owning both bakery and pizza businesses gives a global earnings bonus.
- Later expansions (ice cream parlor, ramen stand) are new districts, added as config data.
- Late game: a prestige "franchise" reset for a permanent multiplier.

## Production chains

Each step must look different, so the flow reads at a glance.

- **Bread:** flour storage → mixer (spinning bowl) → proofing cabinet (dough visibly rises) → oven (orange glow) → display case → cashier
- **Coffee:** bean sacks → grinder → espresso machine (steam) → barista → counter
- **Pastry and cake:** laminating table → oven → decorating station (icing, sprinkles) → display case
- **Pizza:** dough station → sauce station → topping table → wood-fired oven (flames) → slicing and boxing → counter or delivery

Every station shows a progress ring. Every room has the same panel: level and milestone bar, three stats (speed, workers, output), three upgrades with 5 levels each (work speed, output per batch, add machine), and a manager who automates the room.

## Recipes

- **Bakery:** loaf, baguette, croissant, cinnamon roll, donut, muffin, cupcake, cake slice, macarons
- **Café:** coffee, latte, hot chocolate, tea
- **Pizzeria:** margherita, pepperoni, veggie, four cheese, calzone, garlic bread

Recipes unlock with product milestones and have star levels that raise sale value.

## Merge system

- **Board:** a grid with locked cells (cardboard boxes), bubbled items and free items. Merging next to boxes opens them.
- **Chains:**
  - Repair: screwdriver → wrench → drill → toolkit
  - Cleaning: sponge → soap → mop → vacuum
  - Decor: paint can → wallpaper roll → flower pot → café sign
  - Bakery ingredients: wheat → flour → premium flour; eggs, butter, berries, cocoa → chocolate
  - Pizza ingredients: tomato → sauce → premium sauce; milk → mozzarella → aged cheese; basil, olives, pepperoni
- **Generators:** toolbox, cleaning closet, pantry crate, berry garden, coffee sack, vegetable garden, cheese cellar. Each has charges that refill on a timer and can be upgraded with coins.
- Repair, cleaning and decor chains are shared by every shop.

## Run-down and repair

Every repairable object has a state:

| State | Looks like | Gameplay |
|---|---|---|
| Covered | Dust sheet, planks and boxes around it | Not working; first task is "Remove cover" |
| Broken | Cracks, grime, faded colors, smoke or sparks | Not working; needs a repair task |
| Working | Clean, full color | Produces normally |
| Worn | Slowly gets a little dirty | Up to -20% speed, never more |
| Polished | Sparkles | Temporary speed bonus |

- **Objects to restore:** dusty oven, broken espresso machine, cracked display glass, wobbly tables, faded awning, dead plants, boarded windows, flickering neon sign.
- **Repair moment:** camera glides over, dust puff, sheet flies off, the object bounces and color sweeps back from grey, sparkles and rewards fly out, a worker starts using it.
- **Fairness:** wear only builds while playing, never as an offline penalty. At most one breakdown at a time. Any repair can be skipped with coins.

## Bakery-café mechanics

- **Morning rush:** a regular wave of extra customers paying bonus coins.
- **Coziness score:** decor and restored areas raise customer tips.
- **Special orders:** for example "3 cupcakes and a latte in 2 minutes" for a big reward.
- **Seasonal events (later):** gingerbread at Christmas, pumpkin pie in autumn.

## Look and shaders

Target: cute, cozy and clean, at least as polished as Kitty Cat Burger Restaurant, built from KayKit models (world), Kenney (UI and sound) and custom chibi characters.

| Effect | Purpose | Notes |
|---|---|---|
| Toon lighting | 2–3 soft light bands instead of realistic shading | Custom URP Shader Graph lit shader |
| Tinted shadows | Shadows lean warm purple, not grey, so it feels painted | Part of the toon shader |
| Thin black outlines | Silhouettes and inner edges | Screen-space edge detection as a URP renderer feature; inverted hull as the low-quality fallback |
| Rim light | Characters pop against the background | Fresnel term in the toon shader |
| Wear / grime | Drives Covered, Broken and Worn visuals from one 0–1 value | Desaturate, dirt tint and grime texture blend |
| Color sweep | Grey-to-color reveal on repair | Animated wear value with a radial mask |
| Decals | Cracks, stains, scorch marks on walls and floors | URP decals, fade out on renovation |
| Vertex sway | Plants and awnings gently move | Vertex offset in Shader Graph |
| Squash and stretch | Bouncy cute feedback on taps, repairs and spawns | Tweens on transform scale |
| Bloom | Only on oven glow, lamps, neon signs | URP post-processing |
| Color grading | One warm, slightly saturated look across the game | URP post-processing |
| Tilt-shift | Slight blur at top and bottom so shops look like dioramas | Optional on PC High |
| Blob shadows | Soft shadows under characters and props | Cheaper than shadow maps, suits the style |
| Particles | Steam, flour puffs, sparkles, coin bursts, oven flames | Standard particle system (VFX Graph does not run on the web) |

**Quality presets:** *PC High* uses everything plus real-time shadows. *Mobile/Low* uses baked shadows, cheaper outlines, no tilt-shift and lower render resolution.

**UI:** rounded chunky buttons, thin dark outlines, flat fills, a bold rounded font, matching the world.

## Onboarding: the Grand Opening

About 8 minutes of guided play with a 0/6 progress bar, then contextual hints over the first 30–60 minutes.

1. **Arrival (30 s):** camera pans over the dusty shop; first tap within 10 seconds.
2. **First repair (2 min):** teaches merging; the counter's dust sheet flies off.
3. **First sale (2 min):** fix the oven, follow a worker through the chain, serve a customer.
4. **First upgrade (1 min):** the oven's progress ring visibly speeds up.
5. **Second station (2 min):** the chain grows, the queue builds, notifications appear.
6. **Opening day (1 min):** quests introduced, lights warm up, the neon sign turns on, reward chest.

Rules: do, don't read; at most two lines per bubble; no waiting or energy cost in the tutorial; dialogue is skippable; UI unlocks gradually; resume after reload.

## Targets

| Metric | Target | Main levers |
|---|---|---|
| Gameplay conversion | 80%+ | Small first download, no title screen, first tap within 10 s, correct SDK play-start event |
| Average playtime | 10+ min | No dead moments in the first 20 min, no energy wall early, first automation at 15–20 min |
| Day-1 retention | 15–20% | Offline earnings, cloud saves, end-of-session teasers, daily rewards, visible unfinished areas |

Check CrazyGames' current developer docs for exact metric definitions and thresholds.

## Technical foundations (from day one)

- Simulation separate from visuals, so slow devices show less animation but never earn less.
- All content in config data: shops, rooms, chains, recipes, costs, timers, tutorial steps.
- Versioned save format with offline time from timestamps; CrazyGames cloud saves.
- One Addressables group per shop, loaded on purchase.
- Web build settings: Brotli, high code stripping, minimal exceptions, compressed textures, stripped shader variants, target frame rate -1.
- Analytics events for every tutorial step and key moment.
- Test real web builds on CrazyGames early and weekly.

## Build order

1. Project setup: Web target, CrazyGames SDK, build-size settings
2. The look: KayKit import, isometric camera, toon shader, outlines, one polished room
3. Core idle loop: stations, workers, customers, coins, saves, offline earnings
4. Run-down system: object states, wear shader, dust sheet and debris kit, repair animation
5. Merge board: grid, chains, generators, energy, repair tasks
6. Grand Opening tutorial
7. Progression: quests, managers, shops 2–3, world map with locked teasers
8. Pizzeria and later shops

## Open questions

- Final game name (search CrazyGames before choosing)
- Confirm character direction: chibi humans + Biscuit the dog
- Which KayKit packs to use, and who makes the custom characters
