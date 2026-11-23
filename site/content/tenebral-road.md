---
summary: Tenebral bionics are the strongest parts in the mod, and they come at a price.
card: icon:BOR_Tenebrite,BOR_ComponentIII,BOR_Femtoclusters
---
Tenebral parts are self-forming organs and appendages, stronger than anything Solar or Kwazaari can build. They also come with a hunger.

## Bloodlust

Every tenebral implant feeds a need called bloodlust. It drains faster the more tenebral parts a pawn carries, and the only way to refill it is to kill people.

A pawn whose bloodlust runs dry is miserable. When they snap, they go berserk, whatever the break would have been. A berserk colonist with tenebral parts may not come back at all: the more parts they carry, the more likely they are to defect to an enemy faction.

{{ screenshot("tenebral-pawn", "A colonist with tenebral implants") }}

## The tenebral workshop

Tenebral technology has its own buildings:

- The **dark mirror** is where tenebral research is bargained for, through [dark pacts](research.html#dark-pacts). It is also the tenebral pod: once you have tenebral medicine, it heals tenebral pawns ten times faster than normal, holds them in stasis, and installs their parts.
- The **dark archotech altar** makes tenebral guns, apparel and parts, but only a pawn in tenebral psychosis can work it. Working it drains bloodlust, and the altar favors the far gone: the emptier a pawn's bloodlust, the deeper their psychosis and the more dark parts they carry, the faster they work. Their skills and work speed don't matter there.
- **Crystalline joints**, the component every tenebral recipe needs, are harvested at a butcher table from a fresh human corpse and a picocluster. Your colonists will not feel good about it.

{{ things("BOR_ComponentIII") }}

## Where it leads

A colonist in tenebral psychosis is heading somewhere, and where depends on whether they feed the bloodlust. One who keeps breaking brings your colony a choice, once, and there is no going back. One who keeps killing is changing in another way.

That's all this page says by default. Open the box below only if you want to know what's on the other side.

<details class="spoiler tier-umbral" markdown="1">
<summary>Spoilers: the two paths</summary>

**The Path of Light** leads to Orientis. Choosing it purges every tenebral part and every Umbral from the colony, and the stripped pawns become the Orientis xenotype. Orientis has no bionics at all. Its pawns meditate to build up meditation power, which runs their automated buildings, and they gain their enhancements by meditating at the blessing bench. Orientis buildings aren't built from the Architect menu: they're placed with an ability. The [Orientis page](orientis.html) has everything else.

**The Path of Dark** follows the crystal. Every kill by a psychotic pawn with at least one tenebral implant turns a little more of their brain to crystal, and the crystal dulls the mind by as much as it has taken. Once the brain is {{ value("HediffDef", "BOR_TenebralBreakCounter", "modExtensions/li[@Class='BeyondOurReach.DefModExtension_TenebralBreaks']/podThreshold", "pct") }} crystal, the dark mirror can transform the pawn into an Umbral. Getting there takes {{ value("HediffDef", "BOR_TenebralBreakCounter", "modExtensions/li[@Class='BeyondOurReach.DefModExtension_TenebralBreaks']/killsToPodThreshold") }} kills, minus {{ value("HediffDef", "BOR_TenebralBreakCounter", "modExtensions/li[@Class='BeyondOurReach.DefModExtension_TenebralBreaks']/killsSavedPerPart") }} for every tenebral part the pawn carries, and kills only count once they carry at least one. Parts count as they are now, so installing one moves the crystal on at once, and losing one takes it back. The crystal can't be removed, and it never stops: a pawn who keeps killing past that point ends up unable to stay awake, then dead. Umbrals take no surgery and no longer hunger for blood. They are the Dark Archotechs' instruments: each makes its own pacts at the dark mirror, and every pact paid grows the next part of its Umbral body, until Umbral parts have replaced all of it. Umbral buildings work only for Umbral pawns.

</details>
