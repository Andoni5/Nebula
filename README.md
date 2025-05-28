# 🌌 Nebula

![Unity](https://img.shields.io/badge/Unity-6000.0.38f1-blue?logo=unity)
![Plataforma: Android](https://img.shields.io/badge/Plataforma-Android-green?logo=android)
![Licencia: MIT](https://img.shields.io/badge/Licencia-MIT-yellow)

**Nebula** es un _endless‑runner_ 2D para Android orientado a partidas rápidas y sin conexión. Controla a un intrépido astronauta con jet‑pack, recolecta monedas, esquiva obstáculos generados proceduralmente y supera los retos diarios mientras compites por tu mejor marca de distancia.

> _«Diseñado para esos dos minutos de espera en el metro.»_

---

## 🗺 Tabla de contenidos

1. [Características](#-características)
2. [Requisitos mínimos](#-requisitos-mínimos)
3. [Instalación](#-instalación)
4. [Compilación de la APK](#-compilación-de-la-apk)
5. [Controles](#-controles)
6. [Estructura del repositorio](#-estructura-del-repositorio)
7. [Descripción de scripts](#-descripción-de-scripts)
8. [Roadmap](#-roadmap)
9. [Contribuir](#-contribuir)
10. [Licencia](#-licencia)
11. [Contacto](#contacto)

---

## ✨ Características

- **Modo offline completo** – 100 % jugable sin conexión.
- **Obstáculos procedurales** – Cada partida es única; la dificultad escala de forma dinámica.
- **Monedas y cosméticos** – Compra _skins_ para tu player.
- **Retos diarios** – Recompensas que fomentan el regreso.
- **Inicio de sesión opcional** – Supabase + PostgreSQL 15.8 para guardar progreso en la nube.
- **Estadísticas locales** – Historial de récords y distancia recorrida.

---

## 📋 Requisitos mínimos

|           | Android 8.0 (API 26) 64‑bit |
|-----------|-----------------------------|
| **CPU**   | ARMv8 (2 GB RAM)            |
| **GPU**   | Compatible con OpenGL ES 3.0|
| **Pantalla** | 1280 × 720 px o superior |

---

## 🚀 Instalación

```bash
# Clona el repositorio
git clone https://github.com/Andoni5/Nebula.git
cd Nebula

# Abre el proyecto con Unity Hub (versión recomendada: LTS 6000.0.38f1)
```

> **Tip**: Asegúrate de que todos los _assets_ y _scripts_ se hayan importado correctamente antes de compilar.

---

## 📱 Compilación de la APK

1. Abre **File > Build Settings…**
2. Selecciona **Android** y pulsa **Switch Platform**.
3. Clic en **Build** o **Build & Run**.

> También puedes descargar la APK más reciente desde la pestaña **Releases** del repositorio.

---

## 🎮 Controles

| Acción                | Entrada                     |
|-----------------------|-----------------------------|
| Elevar al personaje   | Tocar la pantalla           |
| Pausa                 | Botón ⏸️ (esquina superior) |

---

## 🗂 Estructura del repositorio

```
Nebula/
│
├─ Assets/        # Códigos C#, prefabs, escenas
├─ SQL/           # Scripts de base de datos (Supabase)
└─LICENSE         # Licencia MIT
```

---

## 🔧 Descripción de scripts

### Autenticación y sesión
| Script        | Función |
|---------------|---------|
| `AuthManager.cs` | Maneja la autenticación con Supabase, renovación de tokens y persistencia de sesión. |
| `AuthDAO.cs`  | Realiza peticiones HTTP para _login_, registro y _refresh_ de token. |
| `LoginUITest.cs` | Controla la UI de _login_ / _auto-login_. |
| `OfflineLoginHandler.cs` | Modo offline mediante caché local. |
| `UsersDTO.cs` | DTO que modela al usuario (UUID, nombre, email, _timestamps_) usado tanto por Supabase como por la app. |

### UI principal y menú
| Script            | Función |
|-------------------|---------|
| `NebulaMenuUI.cs` | UI principal: skins, misiones, ajustes, logros y entrada al juego. |
| `MissionButton.cs`| Representa visualmente un reto diario. |

### Juego principal
| Script                     | Función |
|----------------------------|---------|
| `MouseController.cs`       | Movimiento, recolección, colisiones, fin de partida. |
| `FrameByFrameAnimator.cs`  | Animación por estados (correr, saltar, morir). |
| `GeneratorScript.cs`       | Genera salas y obstáculos infinitos. |
| `WarningAsteroid.cs`       | Obstáculo “asteroide”: muestra un icono ⚠️ durante `warningDuration` y luego se lanza a gran velocidad desde el borde derecho. |
| `WarningAsteroidSpawner.cs`| Crea el asteroide cuando el jugador permanece pegado al suelo o al techo el tiempo suficiente, con intervalos aleatorios.|

### Personalización y progreso
| Script                               | Función |
|--------------------------------------|---------|
| `SkinManager.cs`                     | Gestiona la _skin_ activa y sincronización. |
| `InventoryRepo.cs` / `InventoryDAO.cs` | Inventario de objetos cosméticos. |
| `CosmeticsDAO.cs`                    | Acceso a BD de ítems cosméticos. |
| `CosmeticItemDTO.cs`                 | DTO de tienda: nombre, descripción, precio, rareza y fechas de creación/actualización. |
| `InventoryDTO.cs`                    | Relación usuario-ítem con marca temporal de adquisición. |

### Estadísticas del jugador
| Script                                     | Función |
|--------------------------------------------|---------|
| `PlayerStatsRepo.cs` / `PlayerStatsDAO.cs` | Distancia, monedas, retos, etc. |
| `CompletedChallengesDAO.cs`                | Retos diarios completados. |
| `DailyChallengesDAO.cs`                    | Retos diarios activos. |
| `PlayerStatsDTO.cs`                        | DTO con récords de sesión, acumulados y skin activa. |
| `DailyChallengeDTO.cs`                     | Describe cada reto diario: fecha, objetivo y recompensa. |
| `CompletedChallengeDTO.cs`                 | Marca un reto completado y si la recompensa fue reclamada. |

### Utilidades
| Script      | Función |
|-------------|---------|
| `JsonTable.cs` | Persistencia local (JSON) para modo offline. |


---

## 🛤 Roadmap

- [ ] Compatibilidad iOS.
- [ ] Integración con Google Play Games para _leaderboards_.

---

## 🤝 Contribuir

¡Todas las _pull requests_ son bienvenidas!  
1. Haz un _fork_ del proyecto.  
2. Crea tu rama (`git checkout -b feature/nueva-funcionalidad`).  
3. Realiza _commit_ de tus cambios (`git commit -m 'Añade nueva funcionalidad'`).  
4. Haz _push_ a la rama (`git push origin feature/nueva-funcionalidad`).  
5. Abre un **Pull Request**.

> Asegúrate de seguir la guía de estilo C# de Unity y de probar en modo _offline_ y en línea.

---

## 📜 Licencia

Este proyecto se distribuye bajo la licencia MIT.  
Consulta el archivo [LICENSE](LICENSE) para más información.

---

## 📫 Contacto

Desarrollador principal – **Andoni Blasco**  
- GitHub: [@Andoni5](https://github.com/Andoni5)  
- Correo: _andoni.blasco.oyon@gmail.com_

¡Gracias por probar Nebula! 😊
