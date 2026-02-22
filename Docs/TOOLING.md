# Tooling Menus

By default, optional editor tooling is disabled to keep the Unity menu surface focused on the core workflow.

## Default behavior

Without additional scripting defines, only the core `GSP/Dev` and `GSP/Art` menu entries are available.

## Enable optional tooling

1. Open **Project Settings**.
2. Navigate to **Player**.
3. In **Scripting Define Symbols**, add `GSP_TOOLING`.

After adding `GSP_TOOLING`, optional tools compile and appear under `GSP/Tooling/*`.
