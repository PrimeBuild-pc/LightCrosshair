# LightCrosshair 2.0 — Release checklist

## Done (2026-11-16)
- [x] Code reviewed in 3 rounds (REVIEW-1, REVIEW-2), merged on `v2`, 254 tests green.
- [x] Migration on a real v1.9 profile set (host and VM): crosshair kept, global visibility kept, v1 files untouched.
- [x] VM `NeuroTune-W11` (clean Windows 11, .NET 8.0.33 + 10.0.14 Desktop installed from local MSIs):
  installer 1.9 → profile → installer 2.0 over it (single uninstall entry 2.0.0, Start Menu shortcut → `LightCrosshair.exe`, both exes present) → Core and UI start → migration correct → uninstall removes the program folder, keeps `%AppData%` data. VM restored to `Clean-NeuroTune-Alpha2` afterwards.
- Not covered on the VM: interactive desktop (no logged-in user), first launch without .NET 10 installed, autostart entry.

## Prove in gioco (Lorenzo)
Usa la build da `releases/LightCrosshair-v2.0.0-win-x64/` (o l'installer `setup/Output/LightCrosshair-Setup-2.0.0.exe`). Segna ✅/❌ e una riga di note.

| # | Prova | Esito |
|---|---|---|
| 1 | Primo avvio dopo la 1.9: i tuoi mirini ci sono, "Mostra mirino" come lo avevi lasciato | |
| 2 | Mirino visibile sopra un gioco in finestra senza bordi, centrato, nitido | |
| 3 | Alt+X mostra/nasconde; Alt+C / Alt+V cambiano mirino; Alt+L apre e chiude le impostazioni | |
| 4 | Binding mouse: "Tieni premuto per mostrare" su Mouse5 e "per nascondere" su tasto destro, in gioco | |
| 5 | Profilo gioco: crea un profilo per un gioco, cambia mirino, entra/esci dal gioco → cambia e torna al predefinito | |
| 6 | Suggerimento: avvia un gioco noto senza profilo → notifica "Crea profilo" → apre la pagina Giochi | |
| 7 | "Mostra solo nei giochi configurati": sul desktop sparisce, nel gioco c'è | |
| 8 | Colori schermo per un gioco (gamma/vibrance AMD): si applicano in gioco e tornano normali uscendo | |
| 9 | Chiudi LightCrosshair dal Task Manager con i colori applicati → al riavvio i colori tornano normali | |
| 10 | Overlay FPS attivo in gioco: valori sensati, nessun calo percepibile | |
| 11 | Studio: "Mostra in gioco" aggiorna il mirino dal vivo mentre muovi gli slider | |
| 12 | Cambio lingua (es. italiano) senza riavviare | |
| 13 | Monitor: scegli il secondo monitor, poi scollega/ricollega → torna sul principale senza errori | |
| 14 | Avvio con Windows attivato → al riavvio del PC parte da solo | |

NVIDIA (limite FPS, Digital Vibrance): non verificabile su questo PC (AMD). Senza un tester NVIDIA resta "not yet verified on NVIDIA hardware" nel README.

## Release (after the game tests pass)
- [ ] Merge Cursor round 4 (U-17, U-18, screenshots) and Codex round 4 (README, banner, charts, changelog) into `v2`.
- [ ] Rebuild packages and installer from final `v2`; fill SHA256 in README; winget/choco manifests.
- [ ] Bring `main` up to `origin/main` (3 README commits), open PR `v2` → `main`, merge, tag `v2.0.0`, GitHub release (unsigned: SmartScreen warning expected).
- [ ] Note: the published 1.9 README lists a wrong SHA256 for `LightCrosshair-Setup-1.9.0.exe` (actual `D9AB6665…A46D`).
