# Dein Ablauf mit GitHub und Thunderstore

Das ZIP enthält ein lokal initialisiertes Git-Repository auf `main`, aber keinen Remote, Commit oder Tag. Es ändert weder deinen globalen Git-Namen noch deine Anmeldung.

1. ZIP in dein gewünschtes dauerhaftes Verzeichnis entpacken. Der innere Ordner `MliciosFurnitureFixPlus` ist das Repository.
2. README und Changelog bearbeiten. `package/icon.png` ist ein austauschbares 256×256-Starticon. In `package/manifest.json` bei `website_url` deine künftige GitHub-Repo-URL eintragen. Den Teamnamen wählst du beim Thunderstore-Upload, nicht im Manifest.
3. Die fertig gebaute Test-DLL lässt sich mit `Install-Test.cmd` ins gewählte Profil kopieren. Alle Mitspieler benötigen die identische DLL. LethalPerformance eingeschaltet lassen und docs/TESTPLAN.md prüfen.
4. Für eigene Codeänderungen `local.settings.example.json` als `local.settings.json` kopieren und Pfade eintragen. `Test.cmd` baut und testet mit vorhandenen lokalen Werkzeugen. `local.settings.json` ist von Git und vom Quellarchiv ausgeschlossen.
5. `Release.cmd` erzeugt das Thunderstore-ZIP in `artifacts/releases/`. Solange der Code unverändert ist, kann es den beiliegenden Testnachweis verwenden. Nach Codeänderungen zuerst `Test.cmd` ausführen. README/Icon/Manifest-URL dürfen ohne erneuten Laufzeittest bearbeitet werden.
6. Nach erfolgreichem Lobby-Test ein leeres öffentliches GitHub-Repo erstellen, ohne automatisch erzeugte README/Lizenz. Danach im lokalen Repo-Terminal:

```powershell
git add .
git commit -m "Initial release of MliciosFurniture Fix+"
git remote add origin DEINE_GITHUB_REPO_URL
git push -u origin main
git tag -a v0.1.1 -m "Version 0.1.1"
git push origin v0.1.1
```

Falls du vor dem Upload lieber Version 1.0.0 möchtest:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/Set-Version.ps1 -Version 1.0.0
```

Anschließend CHANGELOG anpassen, `Test.cmd`, `Release.cmd`, committen und den passenden Tag `v1.0.0` verwenden. Ein Tag muss genau den Quellstand des hochgeladenen Pakets bezeichnen.

7. GitHub: Releases → neuen Release erstellen, vorhandenen Tag auswählen, Changelog als Beschreibung, **das fertige Paket-ZIP aus artifacts/releases** anhängen und veröffentlichen.
8. Thunderstore: dasselbe Paket-ZIP hochladen, dein Team und die Community Lethal Company auswählen. Passende Kategorien sind Mods, BepInEx und Tweaks & Quality Of Life; wegen der Erstellung mit AI auch AI Generated. Nach Veröffentlichung können alle die Mod über den Modmanager installieren und sie kommt beim normalen Modpack-Export als Abhängigkeit mit.
9. Nach Installation der veröffentlichten Version den lokalen Ordner `MliciosFurnitureFixPlus-LocalTest` entfernen, damit die DLL nicht doppelt liegt.

Für spätere Änderungen optional eine Arbeitsbranch anlegen, z. B. `git switch -c fix/description`, dann Änderungen und Tests, zurück auf main zusammenführen, Versionsnummer/Changelog aktualisieren, Paket bauen, committen/pushen, neuen Tag und Release erstellen. Das gesamte Git-Repo-ZIP wird nicht als Mod-ZIP auf Thunderstore hochgeladen.

`artifacts/` ist absichtlich nicht in Git: GitHub enthält Quellcode und Tests, der Release-Anhang die gebaute DLL. Spiel- und Original-Mod-DLLs sowie private Dateien sind nicht Bestandteil des Repos.
