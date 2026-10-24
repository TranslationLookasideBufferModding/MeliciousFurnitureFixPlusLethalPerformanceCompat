# Changelog

## 0.1.0

- Initial test build of MF x LethalPerformance Compat.
- Replaces the original fire-exit lifecycle helpers with per-landing registration on host and clients.
- Preserves the furniture's network parent and requests an extra interior fire exit through LethalLevelLoader.
- Uses a fresh scene scan for v81 exit-ID assignment when the ship exit is present.
- Pairs the ship and interior explicitly, clears previous-round references and respects direction/storage/orbit conditions.
- Fixes the ship exit staying at "Wait Until Next Landing" on DawnLib interiors such as Habitat.
