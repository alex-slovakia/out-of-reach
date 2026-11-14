# Apoliak: začni tu

Verejný odkaz na `apoliak.online` smeruje na [Apoliak7777](https://github.com/Apoliak7777). Na tento účet bola odoslaná pozvánka s právom zapisovať do súkromného repozitára.

1. Prihlás sa do správneho účtu a prijmi [pozvánku do repozitára](https://github.com/alex-slovakia/out-of-reach/invitations).
2. Nainštaluj/selectuj **Unity 6000.3.28f1** v Unity Hub, vrátane Windows Build Support. Starší prípravný text s `6000.3.26f1` neplatí.
3. Over, že existuje `main`: `git ls-remote https://github.com/alex-slovakia/out-of-reach.git refs/heads/main`. Ak nič nevráti, baseline ešte nie je publikovaný; nezakladaj vlastný Unity projekt ani konkurenčný prvý commit.
4. Naklonuj projekt a vytvor svoju vetvu:

```powershell
git lfs install
git clone https://github.com/alex-slovakia/out-of-reach.git
cd out-of-reach
git lfs pull
git switch -c feature/survival-simulation
git rev-parse HEAD
```

Ak vetva už existuje, prejdi na ňu bez prepisovania jej histórie. Zapíš si baseline SHA a porovnaj ho s Alexovým handoffom.

5. Otvor koreň projektu v Claude Code a vyber **Claude Opus 5.5**. Vlož celý obsah [master promptu](../../APOLIAK-CLAUDE-MASTER-PROMPT.md). Potom postupuj podľa [výberu promptov](../prompts/README.md); prvá implementácia je C01.
6. V Unity Hub zvoľ **Add project from disk** a otvor klon editorom **6000.3.28f1**. Počkaj na import balíkov. Prázdny Bootstrap je integračný základ, nie hotová hra; pôvodný SampleScene zostáva zachovaný.

## Tvoja zodpovednosť

Claude vlastní Core, Simulation, Interaction, Player, Inventory a Tests v `Assets/_OutOfReach/`. Codex vlastní vizuály, prezentáciu, UI, audio a vizuálne prefaby/scény. Zdieľané rozhrania sa nemenia bez koordinácie. Presná mapa je v [ARCHITECTURE.md](../../ARCHITECTURE.md).

Nerob samostatný HUD ani druhý vizuálny svet. Poskytni dátový model, testy a komponenty, ktoré integrátor napojí na Codexove vizuály. Testovacie scény/prefaby môžu byť pod tvojím `Tests/` adresárom. Nepresúvaj ich do cudzieho adresára.

## Odovzdanie po každej úlohe

Pošli Alexovi ID úlohy, branch/commit, zoznam súborov, presné výsledky testov, potrebné Inspector prepojenia a známe chyby. Nehlás úspešný build, ak si ho nespustil. Po koordinácii pushni svoju feature vetvu; nemerguj automaticky do main. Pri chýbajúcom kontrakte popíš potrebnú zmenu a pokračuj v nezávislej práci.

NASA dáta sú už pripravené ako offline referencia v `docs/data/horizons/`; runtime použitie musí implementovať Claude. Aktuálny dataset nie je presná trajektória AURORA-7. Prečítaj `NASA-DATA.md`, najmä smer vývoja vzdialenosti a oneskorenie odpovede.
