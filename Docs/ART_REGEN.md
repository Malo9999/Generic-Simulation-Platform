# Art Pack Regeneration

Generated simulation art outputs are intentionally ignored in git.

## What is ignored
- `_References/`
- `Assets/Presentation/Packs/**/Generated/`
- `Assets/Presentation/Packs/**/Debug/`
- `Assets/Presentation/Packs/**/Blueprints/Generated/`
- `Assets/Presentation/Packs/**/ContentPack.asset`
- `Assets/Presentation/Packs/**/ContentPack.asset.meta`

## How to regenerate locally
1. Open Unity.
2. Run **New Simulation Pack** for the simulation.
3. Run **Build Pack** to regenerate Generated/Debug outputs and ContentPack assets.

After cloning the repo, pack visuals must be generated locally to restore full sim-specific art.

## One-time untrack helper
Run these commands once in your local checkout to remove already-tracked generated files:

```bash
git rm -r --cached _References
git rm -r --cached Assets/Presentation/Packs/**/Generated
git rm -r --cached Assets/Presentation/Packs/**/Debug
git rm -r --cached Assets/Presentation/Packs/**/Blueprints/Generated
git rm --cached Assets/Presentation/Packs/**/ContentPack.asset
git rm --cached Assets/Presentation/Packs/**/ContentPack.asset.meta
git add .gitignore
git commit -m "Ignore and untrack generated art pack outputs"
```
