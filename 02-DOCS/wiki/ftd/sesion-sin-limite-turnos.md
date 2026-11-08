---
author: Luis Lopez
branch: feat/match-recap
status: in-progress
---
# Sesión sin límite de turnos

## Intent
Mantener los consejos durante toda la partida sin pausar por un contador de solicitudes.

## Scope
- Eliminar `RequestLimit`, el modo de envío finito y sus controles de configuración.
- Mantener el contador de solicitudes solo como dato visible de la sesión.
- Mantener la pausa y cancelación manuales, las paradas por errores/estados no seguros y el cierre con recap cuando termina la disponibilidad de datos de la partida.
- Actualizar la documentación y la plantilla de configuración; los `RequestLimit` antiguos se ignoran al cargar y desaparecen al guardar.
- No cambiar la política de capturas, verificación de bots ni coste por petición; la sesión puede seguir llamando al modelo hasta que la persona la pause o ocurra una parada existente.

## Checklist
- [x] Eliminar la rama de pausa por límite y el sondeo especial posterior al límite; la sesión sigue hasta cancelar o detectar el fin/error.
- [x] Retirar RequestLimit y envío continuo de Settings/UI y conservar ajustes restantes.
- [x] Actualizar README, plantilla de configuración y documentación del recap.
- [x] Compilar la aplicación; no ejecutar pruebas salvo petición explícita.

## Evidence
- `CoachingSession.RunAsync` ya no recibe RequestLimit; su bucle sigue hasta cancelación, fin de datos confirmado por cuatro fallos o un error que detenga la sesión. El sondeo especial post-límite fue eliminado.
- `RequestLimit` y `ContinuousSending` ya no aparecen en el código de la aplicación. Los ajustes antiguos se ignoran al cargar y se eliminan cuando se guarda la configuración.
- README, `localsettings.example.json` y `recap-partida.md` describen una sesión sin límite de turnos; el contador visible de solicitudes se conserva.
- `rg` confirmó que el código de aplicación no tiene referencias a RequestLimit ni al sondeo post-límite.
- `dotnet build src/LoLCoach/LoLCoach.csproj --no-restore -o .rsc/build-output`: compilación correcta, 0 advertencias y 0 errores. El build a `bin` no pudo copiar la DLL porque el depurador de Visual Studio la tenía abierta; la salida separada compiló correctamente.
- No se ejecutaron pruebas automáticas ni una sesión de partida real.

## Next
Validar en una partida larga que los consejos continúan más allá de los límites que tenía la configuración.
