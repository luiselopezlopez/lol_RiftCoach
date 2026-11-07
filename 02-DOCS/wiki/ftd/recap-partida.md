---
author: Luis Lopez
branch: feat/match-recap
status: in-progress
---
# Recap de partida

## Intent
Al terminar una partida de práctica, mostrar en Entrenamiento un análisis amplio del desempeño, la comparación con los demás jugadores disponibles y recomendaciones concretas para la siguiente partida.

## Scope
- Detectar la pérdida sostenida de datos de una partida verificada, continuar si la API se recupera y no generar el recap al pausar o perder la verificación de bots.
- Al alcanzar `RequestLimit`, detener consejos y capturas enviadas, pero seguir leyendo estadísticas locales verificadas hasta detectar el cierre o cancelar.
- Mantener en memoria muestras resumidas de la plantilla durante la sesión para comparar progreso y resultado final sin conservar capturas en disco.
- Pedir al modelo un recap con estadísticas propias, comparación por equipo y partida, tendencias observadas, fortalezas que conviene repetir, errores o riesgos respaldados por datos y lecciones prácticas.
- Mostrar el recap completo en la ventana Entrenamiento, separado de los consejos breves y conservado hasta la siguiente partida o el cierre de la app.
- Marcar los datos no disponibles y no inferir victoria, acciones seguidas ni decisiones invisibles.
- Mantener el alcance de entrenamiento contra bots que ya tiene la app. El recap añade una solicitud al modelo al final de la partida.

## Checklist
- [ ] Definir un historial acotado de la partida; prueba: guarda muestras regulares con un umbral de 60 segundos, más la última muestra si es más reciente, y consejos completados, sin identidades ni capturas.
- [ ] Detectar la pérdida sostenida de datos; prueba: cuatro fallos seguidos activan el recap, una lectura recuperada continúa la sesión, y pausa o pérdida de verificación no lo activan.
- [ ] Acumular estadísticas comparables por jugador a lo largo de la partida; prueba: el resumen conserva marcas temporales y valores finales, y omite campos ausentes.
- [ ] Generar el análisis final a partir de los datos capturados; prueba: el contrato incluye comparación con aliados y rivales, aciertos repetibles, prioridades de mejora y límites de evidencia.
- [ ] Mostrar el recap largo en Entrenamiento; prueba: el texto no comparte los límites ni el descarte de los consejos cortos y sigue legible en el panel.
- [x] Compilar la aplicación; prueba: `dotnet build src/LoLCoach/LoLCoach.csproj` termina correctamente.

## Evidence
- `GameSnapshot` ya recoge nivel, K/D/A, súbditos, visión, campeones, equipos e inventario de hasta diez jugadores.
- `MatchRecapRecorder` conserva como máximo 120 muestras regulares con un umbral de 60 segundos, además de la última muestra disponible, y 30 consejos completos validados; no guarda Riot IDs ni capturas.
- `dotnet build src/LoLCoach/LoLCoach.csproj`: compilación correcta, 0 advertencias y 0 errores.
- `rsc risk`: tier 1 para el primer cambio de código. La revisión encontró una posible retención sin límite de caracteres; se validó cada consejo con `AdviceValidator`, la revisión posterior aprobó y el hallazgo quedó registrado como fixed. El verificador independiente no pudo reconstruir el texto previo porque el archivo aún no estaba versionado.
- `CoachingSession` vuelve a consultar cuatro veces con pausas de dos segundos; si una lectura se recupera, continúa. Tras cuatro fallos genera el informe con la señal `LiveClientDataUnavailableAfterRetries`.
- Si se alcanza `RequestLimit`, la app deja de enviar consejos y capturas, pero consulta solo estadísticas verificadas cada cinco segundos hasta cuatro fallos consecutivos; permite cancelar durante esa espera.
- El prompt recibe una descripción localizada desde un enum cerrado; no recibe texto de fin libre ni afirma que la partida o el resultado estén confirmados.
- Entrenamiento muestra el recap expandido, en un área desplazable separada. Lo conserva al reintentar y lo limpia cuando el tiempo de una partida nueva vuelve a empezar.
- `README.md` documenta el fin provisional, el muestreo, la retención en memoria, la privacidad y el coste adicional.
- `rsc risk`: tier 1 para `informe-modelo` y `fin-api`. Las revisiones independientes aprobaron ambos cambios. Se corrigieron dos hallazgos del prompt/contrato y quedaron registrados como fixed; un verificador calificó la exposición del texto libre como no probada en una ruta activa.
- `dotnet build src/LoLCoach/LoLCoach.csproj`: compilación correcta, 0 advertencias y 0 errores después de integrar la generación del recap.
- `rsc risk`: tier 1 para `panel-entrenamiento` y `limite-y-documentacion`. Las revisiones aprobaron la vista, la persistencia, la espera tras `RequestLimit` y la descripción del muestreo.
- `git diff --check`: sin errores de formato.
- Las pruebas automáticas no se ejecutaron.
- No se generó un recap con un modelo real ni se hizo una prueba visual/interactiva en una partida; esas comprobaciones quedan pendientes.
- Riot documenta `eventdata` como una lista de eventos. Su ejemplo incluye `GameStart`, torres y objetivos, pero no muestra un evento de fin ni el resultado: https://developer.riotgames.com/docs/lol
- Por eso, una caída de Live Client Data no basta para afirmar que acabó la partida. El recap informa que la sesión de datos terminó, sin inventar el resultado.

## Next
Validar con una partida real que la API local deja de responder al terminar y que el informe compara las estadísticas observadas de los aliados y rivales disponibles.
