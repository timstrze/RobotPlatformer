# Robot Platformer (Unity 3D)

Unity project using URP, the Input System, Cinemachine, and Starter Assets-style third-person gameplay with star collectibles.

## Documentation

- **[Architecture and codebase map](docs/ARCHITECTURE.md)** — scenes, scripts, data flow, and conventions. **Start here** for onboarding or AI-assisted work.
- **Contributors:** When you change gameplay flow, scenes, Build Settings, or anything covered in the architecture doc, update [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) in the same change; adjust [README.md](README.md) if opening instructions or the entry scene change.

## Opening the project

1. Install a compatible **Unity Editor** version (match the version this folder was created with, or migrate via Unity Hub).
2. Add the project in **Unity Hub** → **Open** → select this directory.
3. Open `Assets/Scenes/Level1_Scene.unity` for the first level (play from Build Settings uses scene index 0).
4. Let the Editor finish importing packages (including **glTFast** for `.glb` models). If the ground patrol enemy still uses a placeholder capsule, run **Tools → Bad Guy → Integrate Steel Sentinel into BadGuy_GroundPatrol** once — see [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).

## Build

Use **File → Build Settings** to choose platform and build. Only scenes listed there are included in player builds.
