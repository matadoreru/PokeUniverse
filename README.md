# 🌌 Poké Universe

![Status](https://img.shields.io/badge/Status-In%20Development-orange)
![Engine](https://img.shields.io/badge/Engine-Unity-black)
![Multiplayer](https://img.shields.io/badge/Netcode-NGO-blue)
![License](https://img.shields.io/badge/License-MIT-green)

**Poké Universe** es un proyecto *fan-made* multijugador desarrollado en Unity. El objetivo es crear una colección de minijuegos sociales ambientados en el universo Pokémon para jugar con amigos a través de Steam.

> 🚧 **Estado Actual:** El proyecto se encuentra en fase de desarrollo (Alpha). Actualmente cuenta con un modo de juego jugable: **"Impostor"**.

## 🛠️ Tecnologías Utilizadas

Este proyecto sirve como demostración técnica de varias herramientas avanzadas de Unity:

* **[Unity 2022/2023]**: Motor principal.
* **Unity Netcode for GameObjects (NGO):** Lógica de red y sincronización de estado.
* **Facepunch.Steamworks:** Wrapper de C# para la API de Steam (Lobbies, Avatares, P2P).
---

## 🚀 Instalación y Uso (Para Desarrolladores)

1.  **Requisitos:**
    * Unity Hub y Unity [2022.3.62f3].
    * Cuenta de Steam (necesaria para la conexión P2P).

2.  **Clonar el repositorio:**
    ```bash
    git clone [https://github.com/tu-usuario/poke-universe.git](https://github.com/tu-usuario/poke-universe.git)
    ```

3.  **Configuración:**
    * Abre el proyecto en Unity.
    * Asegúrate de tener **Steam abierto** en tu PC.
    * El proyecto utiliza el `AppID 480` (Spacewar) para pruebas de desarrollo.

4.  **Jugar:**
    * Abre la escena `MainMenu`.
    * Dale al Play.
    * Hostea una partida y envia el código de la sala a los otros jugadores.
      
4.  **Build:**
    * ¡Descarga la build (carpeta con el .exe) del juego en el apartado de Releases si no quieres clonar el repositorio!
    * También puedes hacer la build manualmente clonando el repositorio con tu Unity...
---

## ⚖️ Aviso Legal / Legal Disclaimer

This is a non-profit fan game created for educational and entertainment purposes only. No copyright infringement is intended.

**Intellectual Property:**
Pokémon, Pokémon character names, and related assets are trademarks and copyrights of Nintendo, Creatures Inc., and GAME FREAK inc.

**License:**
* The **source code** (C# scripts, logic) of this project is licensed under the **MIT License** (see LICENSE file).
* The **assets** (sprites, audio, 3D models, textures...) originating from the Pokémon franchise are **NOT** covered by this license and remain the property of their respective owners.

This project is **not** affiliated with, endorsed, sponsored, or specifically approved by Nintendo or The Pokémon Company.

---

## 🗺️ Roadmap / Futuro
- [x] Minijuego 1: Impostor.
- [ ] Refactor con multiples escenas
- [ ] Mejoras visuales en la UI (Animaciones, Feedback).
- [ ] Minijuego 2:
