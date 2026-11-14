# Ktorý prompt, model a chat použiť

**Unity pre oboch: 6000.3.28f1.** Prompty sú pracovné zadania, nie automatické prepínače modelu. Model vyberte v aplikácii ešte pred odoslaním promptu. Ak odporúčaný model v účte nie je dostupný, oznámte náhradu; nepredstierajte, že sa spustil.

| Kto / chat | Model | Súbor | Kedy |
|---|---|---|---|
| Apoliak, hlavný Claude Code chat v jeho klone | **Claude Opus 5.5** | [Master prompt](../../APOLIAK-CLAUDE-MASTER-PROMPT.md) | Najprv, po prijatí pozvánky a dostupnom baseline |
| Ten istý Claude Code chat | **Claude Opus 5.5** | [C01 – základ simulácie](C01-foundation-opus-5-5.md) | Prvá implementácia; zastaviť na checkpoint |
| Ten istý Claude Code chat | **Claude Opus 5.5** | [C02 – interakcie a opravy](C02-interaction-repairs-opus-5-5.md) | Po overení C01 |
| Ten istý Claude Code chat | **Claude Opus 5.5** | [C03/C04 – EVA a finále](C03-C04-eva-communications-opus-5-5.md) | C03 až po I01; C04 až po C03 |
| Alex, Codex chat v samostatnom klone | **GPT-6 Astra, High** | [V01 – vizuálny základ](V01-visuals-astra-high.md) | Súbežne s C01, po baseline |
| Alex, ten istý Codex chat | **GPT-6 Astra, High** | [V02–V04 – filmové spracovanie](V02-V04-cinematics-astra-high.md) | Po V01, postupne podľa checkpointov |
| Samostatný Claude chat, iba čítanie | **Claude Fable 5.2** | [Kontrola kontraktov a vedy](review-contracts-science-fable-5-2.md) | Pred integráciou alebo pri ťažkom probléme |
| Výslovne poverený integrátor, samostatná integračná vetva | **GPT-6 Astra, High** | [Integrácia](integration-astra-high.md) | Až keď obaja odovzdajú výsledky a koordinujú merge |

Pre Fable ponechajte predvolené úsilie dostupné vo vašom rozhraní; pri konkrétnom nevyriešenom probléme ho možno zvýšiť. Nerobte z review chatu druhého súbežného zapisovateľa gameplay kódu. Astra/High je odporúčaná voľba z modelov dostupných v tomto Codex prostredí; presný názov v klientskom výbere sa môže líšiť.

Opus 5.5 je vhodný predvolený implementačný model; Fable 5.2 rezervujeme na náročný audit. Toto rozdelenie zodpovedá [odporúčaniu Anthropic](https://www.anthropic.com/claude-fable-and-mythos-5-2) používať Opus na bežné programovanie a Fable podľa náročnosti. [Opus 5.5 – oficiálny popis](https://www.anthropic.com/claude-opus-5-5). Overené 14. novembra 2026; dostupnosť závisí od účtu.

## Bezpečné súbežné spustenie

Apoliak má **vlastný klon**, Claude Code chat a vetvu `feature/survival-simulation`. Alex má vlastný klon, Codex chat a vetvu `feature/visuals-cinematics`. Obaja pracujú proti rovnakému overenému baseline. Nespúšťajte C01 a C02 naraz; zasahujú do rovnakých súborov. Review môže bežať súbežne, ale nesmie meniť zdrojové súbory. Nový pracovný chat musí najprv prečítať projektové pravidlá a aktuálny handoff.

Každý prompt končí konkrétnym odovzdaním. Ďalší sa spúšťa až po jeho teste/checkpointe. Samotný text promptu neoprávňuje na automatický merge ani na zmenu súborov druhého autora.
