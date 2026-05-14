# BalloonParty

A colorful arcade game built with Unity. Pop balloons, chain combos, and level up through increasingly challenging waves.

## Stack

| Library | Role |
|---|---|
| [VContainer](https://vcontainer.hadashikick.jp/) | Dependency injection |
| [UniRx](https://github.com/neuecc/UniRx) | Reactive state |
| [MessagePipe](https://github.com/Cysharp/MessagePipe) | Pub/sub messaging |
| [UniTask](https://github.com/Cysharp/UniTask) | Async |
| [DOTween](http://dotween.demigiant.com/) | Tweening |
| [NaughtyAttributes](https://github.com/dbrizov/NaughtyAttributes) | Inspector utilities |
| [TextMeshPro](https://docs.unity3d.com/Packages/com.unity.textmeshpro@latest) | UI text rendering |

## Project structure

All gameplay code lives in `Assets/Source/`. Each feature folder contains a `README.md` describing its architecture and interactions. See `Assets/Source/README.md` for the full code style guide.
