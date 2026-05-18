# Duelyst Unit Design Reference Notes

This note records reusable reference observations from Duelyst for this project's unit-art interpretation. It is research only; the authoritative original story direction lives in `../40-content/world/summoned-world-story-framework.md`.

## Source Boundary

- Duelyst is used here as a visual and systemic reference because this project has imported unit sprites derived from its open-source asset ecosystem.
- Do not inherit Duelyst faction names, generals, lore, proper nouns, geography, politics, or card text.
- Treat the sprites as silhouettes and production constraints: armor shapes, creature types, animation tone, faction color language, and combat-role readability.

## Reference Sources

- Open-source repository: <https://github.com/open-duelyst/duelyst>
- Faction overview pages: <https://duelyst.fandom.com/wiki/Lyonar_Kingdoms>, <https://duelyst.fandom.com/wiki/Songhai_Empire>, <https://duelyst.fandom.com/wiki/Vetruvian_Imperium>, <https://duelyst.fandom.com/wiki/Abyssian_Host>, <https://duelyst.fandom.com/wiki/Magmar_Aspects>, <https://duelyst.fandom.com/wiki/Vanar_Kindred>

## Extracted Unit-Archetype Features

The project asset package currently contains 697 generated unit packages under the seven `assets/battle/units/` faction directories. The six dense prefix groups `f1` to `f6` are strong enough to support six original visual lineages.

| Local Prefix | Visual / Role Features To Reuse | Do Not Reuse |
| --- | --- | --- |
| `f1` | Radiant knights, guardians, archers, priests, lion/gryphon-like heraldry, disciplined defensive lines. | Lyonar names, holy kingdom canon, named generals. |
| `f2` | Agile duelists, fox/oni/asura motifs, martial artists, calligraphers, flame and shadow movement. | Songhai imperial identity, named clans, card-specific lore. |
| `f3` | Desert spirits, obelisks, dervish-like summons, scarabs, sand constructs, relic engineers. | Vetruvian empire canon, original desert mythology. |
| `f4` | Abyssal bodies, crawlers, blood/moon motifs, daemons, gates, swarm and sacrifice silhouettes. | Abyssian Host canon and named demon hierarchy. |
| `f5` | Dinosaurs, eggs, lava, primal beasts, hunters, ancient reptile or megafauna profiles. | Magmar aspect names and world history. |
| `f6` | Ice beasts, walls/barriers, crystal bodies, northern hunters, elk/rhino/wolf silhouettes. | Vanar nation identity and original winter pantheon. |

## Design Takeaways For This Project

- Use the six groups as visual grammar, not as one-to-one factions. A campaign force can recruit across groups when social, oath, threat, or site-state logic supports it.
- Preserve tactical readability: heavy lineholders, mobile assassins, structure/summon controllers, swarm/corruption threats, giant beasts, and ice-control defenders should remain visually distinct.
- Reframe faction identity as `召来者谱系` / summoned lineages, which fits the project's recruitment, officer social play, and strategic-map writeback better than importing fixed Duelyst nations.
- Boss and neutral packages should become cross-lineage anomalies, ancient site guardians, failed summons, mercenaries, or world-threat avatars rather than a seventh normal faction.
