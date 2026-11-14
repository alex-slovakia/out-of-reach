# CinematicReferenceTpbV01 — overená obrazová referencia

Dátum: 14. 11. 2026. Výstup je návrh, nie implementácia. Píše sa výlučne do tohto priečinka.

## Zdroj a rozsah overenia

[Brendan Jowett: I Spent 100 Hours Making a Game With Claude (this is cooked)](https://www.youtube.com/watch?v=tpbXZyRxWNY), publikované 29. 10. 2026; metadáta uvádzajú 22:22, video stream trvá 1342.266667 s. Kapitoly autora: 10 hodín **08:30–14:54**, 100 hodín **14:54–22:03**. Sú to autorove označenia a tvrdenia o čase vývoja, nie náš audit nákladov či práce AI.

Skutočne preskúmané: obrazová kópia 640 × 360 / 30 fps (formát 134, 51 588 202 bajtov), široký prehľad oboch kapitol každých 12 s a hustejšie sekvencie úvodu, tela, vstupu do vozidla, rebríka a plávania. Vybrané PNG zachovávajú celý pôvodný obraz vrátane autorovej webkamery. Kontaktný hárok pridáva iba časové popisky. Časy v `frames.json` zodpovedajú existujúcim PTS video streamu; presnosť 1/30 s, nie tvrdenie o vnútornom čase hry.

Webový fetch bol throttled; titulky cez yt-dlp zlyhali HTTP 429. Kópia je **bez audia**: nehodnotím reč, zvuk ani prepis. Žiadna nová závislosť. Licencia na prevzatie herných assetov nie je doložená; obrázky sú súkromné analytické referencie, neimportovať do hry. Nezverejňovať celú kópiu ani surové `source.info.json` (obsahuje dočasné stream URL).

Autorov popis uvádza Unreal Engine. Na 00:37 je stránka Unreal a záverečné kredity 21:54 uvádzajú Unreal Engine 5.8. To potvrdzuje **čo autor prezentuje**, nie nezávislé overenie enginu, verzie, pôvodu modelov alebo použitých AI modelov. OUT OF REACH zostáva Unity 6000.3.28f1.

## Osem vybraných záberov

Otvoriť `CONTACT-SHEET.jpg`; samostatné originálne snímky sú v `selected/`. Každý záznam má link a SHA-256 v `frames.json`.

| ID / čas | Priamo viditeľné | Použitie pre AURORA-7 / hranica záveru |
|---|---|---|
| F01 / [09:52.000](https://www.youtube.com/watch?v=tpbXZyRxWNY&t=592s) | Po tmavej leteckej sekvencii a čiernej 09:48–09:50 prvá osoba na pláži, poškodené lietadlo v zornom poli, málo textu. | Zatmenie má skončiť na čitateľnej príčine problému. Náš úvod ostane v kokpite; pláž ani lietadlo nepreberáme. Samotné video nedokazuje presný moment obnovenia inputu. |
| F02 / [10:21.200](https://www.youtube.com/watch?v=tpbXZyRxWNY&t=621s) | Pohľad nadol ukazuje rukavicu, rukáv, spodok trupu a nohu; sekvencia 10:18–10:22 ukazuje ďalšie pózy. | Telesná prítomnosť je overená. Celotelový rig, IK riešenie a výmena výstroja z toho nevyplývajú. |
| F03 / [15:42.000](https://www.youtube.com/watch?v=tpbXZyRxWNY&t=942s) | Veľký výhľad, štíhly názov naľavo, krátky zoznam menu. | Hierarchia a voľný priestor, nie kopírovanie typografie/logo artu. Toto je menu, nie dôkaz vhodnosti minimálneho HUD pre naše zdroje. |
| F04 / [16:22.000](https://www.youtube.com/watch?v=tpbXZyRxWNY&t=982s) | Naklonený kokpit s viditeľnými fyzickými prístrojmi a dažďom. Širšie 16:06–16:30 strieda vonkajšie lietadlo, interiér, náraz na vodu, čiernu a pláž. | Kontaktný bod a fyzická reakcia kamery. Zdrojový úvod NIE je súvislý záber z prvej osoby; náš návrh jedinej kamery je vedomá adaptácia. |
| F05 / [16:56.400](https://www.youtube.com/watch?v=tpbXZyRxWNY&t=1016s) | Malá výzva pri buggy; nasledovne 16:56.6 čierna a 16:56.7 pohľad za vozidlom. | Jedna lokálna výzva. Detailné nastupovanie rukou/nohou sa v kontrolovanom prechode neukazuje. Aj 10:39.0→10:39.1 prejde rýchlo za buggy. |
| F06 / [18:18.000](https://www.youtube.com/watch?v=tpbXZyRxWNY&t=1098s) | Dve ruky manipulujú so zbraňou a zásobníkom. | Preniesť rytmus uchopiť–zarovnať–zasunúť na vlastnú servisnú kazetu. Zbraň, boj ani zdrojové animácie nepreberať. |
| F07 / [20:03.000](https://www.youtube.com/watch?v=tpbXZyRxWNY&t=1203s) | Ruky na priečke rebríka. 20:02–20:08 strieda úchopy, 20:09 pohľad z plošiny. | Originálne úchopy madla počas existujúcej EVA trasy. 18:45–18:56 je schodisko, nie druhý rebrík. |
| F08 / [20:30.000](https://www.youtube.com/watch?v=tpbXZyRxWNY&t=1230s) | Podvodný pohľad, obe ruky v cykle záberu. 20:12–20:34 opakované pohyby, 20:35 prechod na hladinu, 20:36–20:45 iné pózy pri hladine. | Plynulý prechod medzi pózami a váha končatín. Žiadne plávanie vo vákuu; voľná ruka iba doznieva po úchope, pohyb po trase riadi Player. |

**Neoverené:** výmena oblečenia vybavením, presný algoritmus bočnej reakcie hlavy, full-body plavecký rig a komplexná animácia nástupu. Oblečené/gloved ruky v 10h a holé ruky v 100h sú rozdielne ukážky, nie dôkaz prepínania outfitu. Pohyb obrazu a rúk neodlišuje vstup myšou, sway, head bob a strih. Červené vývojové upozornenia v 100h nie sú dizajnová referencia UI.

## Originálny úvod: ruka na AURORA-7, náraz, návrat kontroly

Navrhovaný rozsah: **8 s od existujúceho Impact requestu**; pred ním zostáva hráčov normálny príchod ku `CockpitConsole` a existujúci cockpit check. Jeden kokpit, jedna kamera, bez novej scény/vozidla. `PlayerSpawn` (0, 0.05, 0.8; yaw 180) a `CockpitConsole` (0, 1.1, -1.72) pochádzajú z čítaného ShipVisualBuilder; nepremiestňovať hráča z ľubovoľnej interakčnej pozície na stoličku. Ochranné gesto smerovať na najbližší dosiahnuteľný okraj konzoly, inak použiť zdvih predlaktia bez falošného dotyku.

| Čas | Obraz / zvuk | Autorita |
|---|---|---|
| 0.0–0.5 s | Jeden krátky otras a dlaň pred očami/na dosiahnuteľnej hrane. | `CinematicRequested(Impact, SequenceId)` už znamená potvrdenú nehodu. Žiadny druhý damage trigger. |
| 0.5–1.3 s | Pohľad ustúpi o navrhovaných ≤2 cm, roll ≤1.5°, jeden tlmený pokles svetla. Kovový prenos cez trup, miestny alarm. | Čisto prezentačné limity na skúšku; reduced effects vypne pohyb aj zatmenie. |
| 1.3–2.1 s | Krátke tmavé prekrytie; jeden uvoľnený ľahký predmet pri paneli sa pohne. | Dekoratívny pohyb mimo kolízií a priechodu; žiadne nové projektily či zranenia. |
| 2.1–5.5 s | Ruka sa oprie/stiahne, červené núdzové svetlo a amber poškodený panel. Kamera sa vráti do zachytenej lokálnej pózy. | Trvalé svetlo poškodenia číta stav systému; nesmie byť prepisované snapshotom do zdravého vzhľadu. |
| 5.5–8.0 s | Pokojný obraz, jedna stručná výzva `Restore power`. Doznie kov, zostane ventilácia/dych. | Najprv obnoviť dočasné transformy/prekrytia, potom raz poslať matching `CinematicFinished`. Core uvoľní ovládanie. |

Za oknom len vzdialené hviezdy a primerane malá Zem, žiadna blízka zemská pologuľa. Predchádzajúca správa Mission Control môže byť výslovne archivovaná. Havária nesmie vytvoriť okamžitú novú odpoveď zo Zeme. NASA delay, epoch a outbound + processing + inbound zostávajú v jednej simulácii. Simulácia môže počas úvodu ďalej spotrebúvať zdroje; prezentačných 8 sekúnd nie je časový skok.

## Päť malých fyzických interakcií

1. **P0 — krátka páka pri PowerRepair** (−1.65, 1.15, 6.1): uchopiť, malý odpor, doraz, spätné uvoľnenie prstov; 0.6–0.9 s. Hra najprv prijme existujúci repair command; animácia len potvrdí úspech, nečaká na animation event pre účtovanie.
2. **P0 — servisná kazeta pri LifeSupportRepair** (1.65, 1.15, 8.1): druhá ruka podopiera, kazeta sa zarovná, zasunie a zacvakne; 0.9–1.3 s. Inšpirácia F06 bez zbrane. V aktuálnom API je repair synchrónny: pri zamietnutí sa kazeta nesmie vizuálne uzamknúť ani zmiznúť z inventára. Dlhý prerušiteľný repair by potreboval nové koordinované Core API a nie je súčasťou tohto návrhu.
3. **P1 — západka a madlo airlocku** (`AirlockInner`, `AirlockOuter`): rukavica krátko spočinie na madle, pohne sa vizuálna západka až po prijatom stave dverí. Nikdy neotvára gameplay gate animácia. Nový rebrík či nový vertikálny priechod zatiaľ netreba.
4. **P1 — krátke úchopy na existujúcej EVA trase** (`EvaStart` → `AntennaRepair` → `EvaReturn`): striedať dve pôvodné pózy rukavíc; voľná ruka/kolená jemne doznejú. Chodidlá a root stále riadi `TetheredRoute`/Player; žiadny root motion, plavecký ťah či nový 6DOF model. Modelár musí doložiť reálne dosiahnuteľné madlá; gesto nesmie držať vzduch.
5. **P1 — ochranný dotyk + jeden kontrolovaný predmet pri náraze**: zdieľať tú istú rukavicu/rig s úchopmi, nie nový kaskadérsky systém. Predmet má deterministický prezentačný pohyb v malom priestore; zvuk iba pri kontakte alebo v kabíne. V EVA neznie vzduchové svišťanie či výbuch za kamerou.

Telo/skafander: modelový autor dodá pôvodný spodok trupu/nohy a ruky, variant bežný odev/skafander. Codex zobrazuje podľa `IEvaStatus.IsSuitOn`, tesnenie/visor podľa `IsSuitSealed`; nie podľa tlačidla UI. Zmena outfitu, clipping pri maximálnom sklone, vlastné tiene a culling hlavy musia mať reálnu kamerovú skúšku. Bočný náklon je samostatný voliteľný vizuálny offset, nie zmena yaw/PitchDegrees v Player. Jeden vlastník každého pivotu.

## Konkrétne integračné kotvy a rozdelenie

Čítaná integračná vetva: `codex/i01-integration`, HEAD `af81a2130088225d198d8ad19b3fc6d526b5b452`. Integrátor na nej pracuje; toto nie je záruka čistého stromu. `source-file-hashes.json` identifikuje presne skontrolované súbory. Root workspace už obsahuje ICargoLoading, ale v čítanej integračnej Contracts kópii chýba; C05 sa nesmie vydávať za tu overenú integráciu.

- **Codex presentation/camera/audio:** existujúci `Scripts/Presentation/Cinematics/MissionCinematicPresenter.cs`, `Configure(IMissionEventBus, IGameState, ISimulationClock)`, `SkipActive()`, `SequenceStarted`, `SequenceEnded`, `CaptionChanged`. Dnes Impact trvá 4.5 s a presenter clampuje na 40 s. Návrh 8 s je pod čítaným štandardným Core fallbackom 45 s; integrátor musí overiť skutočnú konfigurovanú hodnotu. Animovať vyhradený `cameraOffsetPivot`, nie Player.ViewPivot ani spoločný planning framing pivot. Dočasné effectLights musia byť oddelené od trvalého stavu poškodenia. Audio už spúšťa composition (`power_down`, varovanie, Emergency mood); neduplikovať ho novým listenerom.
- **Claude Core/Player/Interaction:** `MissionDirector.TryCompleteCockpitCheck()` v Intro nastaví accident raz, request, systémové poškodenie a komunikáciu. `MissionSession.IsPlayerControlAllowed` závisí od `!Cinematics.IsWaiting`, pause a fázy. `FirstPersonController` vlastní yaw/pitch, controller a trasu. `IInteractable.TryInteract()` a `IRepairable.TryRepair(IResourceSystem)` sú existujúce synchrónne príkazy. Core timeout musí ukončiť aj predĺženú/rozbitú prezentáciu. Žiadne nové locomotion mechaniky v tomto balíku.
- **Claude/Fable model writer:** výlučne koordinovaný modelový scope `Visuals/Models/Aurora7/**`; pôvodné telo/skafander, rukavice, kazeta/páka/madlo, ich pivoty a animácie. Neprideľuje sa druhý writer. Žiadne kopírovanie zdrojového shooter modelu.
- **Jediný Claude/Fable UI writer:** celá `Assets/_OutOfReach/UI/**` podľa posledného Alexovho presunu, hoci starší root ARCHITECTURE uvádza Codex. Presný samostatný brief je `UI-HANDOFF.md`; zatiaľ nepredpokladať ACK pridelenia.
- **Root integrátor:** serialized bindings do jedného Bootstrap runtime/kamery/listenera, koordinácia modelov/UI a publikovanie vybraného balíka do súkromného issue #7. Tento chat nikomu nič neposielal.

Odporúčané poradie nezávislých úloh: (1) UI obrazový návrh od jediného autora; (2) skafander/telo podľa stavu; (3) jedna páka s úspech/zamietnutie; (4) osemsekundový úvod; (5) kazeta a EVA úchopy až po stabilnom I02. Každú implementáciu prevziať samostatne; žiadne automatické zlučovanie vetiev.

## Čo sa skutočne overilo a čo ešte nie

Overené tu: zdrojové metadáta, dekódovanie videa, časové PTS ôsmich vybraných snímok, vizuálna kontrola všetkých ôsmich a sekvencií, veľkosť/hash streamu, SHA-256 PNG, čítanie uvedených kontraktov a zdrojov. Žiadny Unity proces, build, playtest ani produkčná zmena. Žiadny screenshot nie je nový render OUT OF REACH.

Pri neskoršej implementácii musí integrátor overiť: normálny koniec/skip/disable/restart/pause/timeout bez zostávajúcej čiernej alebo input locku; presne jedno poškodenie a acknowledgement; jedna kamera/listener; reduced effects aj mute; outfit podľa autority; oprava s nedostatkom dielov bez úspešnej animácie; žiadne prekrytie dvoch hlasov; EVA bez animáciou otvorenej brány; komunikácia dodrží príčinný delay. Tieto kontroly zatiaľ NEBOLI vykonané pre navrhnuté zmeny.
