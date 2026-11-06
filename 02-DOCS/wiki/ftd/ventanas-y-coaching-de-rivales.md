---
author: Luis Lopez
branch: feat/separate-guide-and-tactical-coaching
status: in-progress
---
# Ventanas y coaching de rivales

## Intent

Separar la información del campeón y la miniguía, conservar la disposición en cada estado de la ventana principal y dar consejos de línea y combate con datos actuales de la partida.

## Scope

- Separar Campeón y Miniguía en paneles movibles independientes, con controles de ventana propios.
- Guardar y restaurar disposiciones independientes para la ventana principal normal y maximizada.
- Detectar al rival de la misma posición cuando los datos del cliente local lo identifiquen. Usar ese emparejamiento en la estrategia de la miniguía; indicar cuando falten datos en vez de adivinar.
- Mantener la verificación actual de partidas contra bots. Orientar el combate solo sobre campeones que el modelo pueda identificar en la captura actual. Ajustar intercambios, retirada, objetivo y uso de habilidades al contexto visible.
- Mantener el uso de la ventana del juego, las estadísticas sin identidades de jugador en el JSON y las restricciones contra control del personaje, lectura de memoria o elusión de Vanguard.

## Checklist

- [ ] Campeón y Miniguía se abren, cierran, mueven y redimensionan por separado; el menú Ventanas las controla por separado.
- [ ] La disposición normal y la maximizada se guardan por separado y se restauran al cambiar de estado y al reiniciar.
- [ ] El contexto identifica el rival de la misma posición con datos disponibles y la miniguía describe cómo jugar el emparejamiento sin inventar datos ausentes.
- [ ] La verificación de bots y el bloqueo de PvP se conservan sin cambios.
- [ ] Los consejos durante la partida incluyen una acción y una indicación táctica breve basada en rivales identificables en la captura actual.
- [x] Compilación completada; las pruebas automáticas quedan pendientes si no se solicitan.

## Evidence

Implementación aplicada en `MainWindow`, `FloatingPanel`, `SettingsService`, `DashboardViewModel`, `MatchTacticalContext` y `FoundryCoachingService`. El esquema de configuración mantiene compatibilidad con el antiguo `Layout`. `BotMatchVerifier` y `ProductPolicy` siguen limitando el análisis a bots verificados. `dotnet build src/LoLCoach/LoLCoach.csproj --no-restore` terminó correctamente (0 advertencias, 0 errores); `git diff --check` terminó con código 0. No se ejecutaron pruebas ni una validación manual de la interfaz.

## Next

Queda pendiente validar manualmente el movimiento, restauración y comportamiento de los consejos durante una partida verificada contra bots.
