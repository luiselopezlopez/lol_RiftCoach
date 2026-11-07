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
- Añadir junto al título de Entrenamiento una acción para indicar manualmente el fin de partida y generar el recap con las muestras ya reunidas; debe cancelar el consejo en curso, detener la sesión y no afirmar un resultado que los datos no muestran.
- Mantener consejos y capturas durante toda la partida, sin detenerse por un contador de turnos; detenerse solo al pausar, ante un error/estado no verificable o al detectar el fin de los datos de partida.
- Mantener en memoria muestras resumidas de la plantilla durante la sesión para comparar progreso y resultado final sin conservar capturas en disco.
- Pedir al modelo un recap con estadísticas propias, comparación por equipo y partida, tendencias observadas, fortalezas que conviene repetir, errores o riesgos respaldados por datos y lecciones prácticas.
- Mostrar el recap completo en la ventana Entrenamiento, separado de los consejos breves y conservado hasta la siguiente partida o el cierre de la app.
- Marcar los datos no disponibles y no inferir victoria, acciones seguidas ni decisiones invisibles.
- Mantener el alcance de entrenamiento contra bots que ya tiene la app. El recap añade una solicitud al modelo al final de la partida.

## Checklist
- [ ] Definir un historial acotado de la partida; prueba: guarda muestras regulares con un umbral de 60 segundos, más la última muestra si es más reciente, y consejos completados, sin identidades ni capturas.
- [ ] Detectar la pérdida sostenida de datos; prueba: cuatro fallos seguidos activan el recap, una lectura recuperada continúa la sesión, y pausa o pérdida de verificación no lo activan.
- [x] Añadir el botón «Fin de partida» junto al título de Entrenamiento; permanece desactivado hasta recibir datos, cancela un consejo en curso y genera el recap manual sin cambiar la pausa normal.
- [ ] Validar en la app y en una partida que el botón ocupa la cabecera, detiene la sesión y muestra el recap generado.
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
- La sesión ya no tiene `RequestLimit`: el flujo normal sigue solicitando consejos hasta que la persona lo pausa o la API local deja de responder en cuatro comprobaciones seguidas; entonces se intenta el recap.
- El prompt recibe una descripción localizada desde un enum cerrado; no recibe texto de fin libre ni afirma que la partida o el resultado estén confirmados.
- Entrenamiento muestra el recap expandido, en un área desplazable separada. Lo conserva al reintentar y lo limpia cuando el tiempo de una partida nueva vuelve a empezar.
- `README.md` documenta el fin provisional, el muestreo, la retención en memoria, la privacidad y el coste adicional.
- El botón de la cabecera de Entrenamiento usa una señal separada de Pausar; cierra una solicitud de consejo en curso y pasa al recap las muestras ya registradas con la evidencia `PlayerIndicatedMatchEnded`.
- Tres rondas de revisión independiente no encontraron hallazgos; `dotnet build src/LoLCoach/LoLCoach.csproj --no-restore -o .rsc/build-output` terminó con 0 advertencias y 0 errores.
- La cabecera reserva una columna fija para cerrar el panel, coloca «Fin de partida» junto al título y permite que el título se recorte si falta espacio.
- README describe la acción manual y aclara que una señal humana no verifica el resultado.
- `rsc risk`: tier 1 para `informe-modelo` y `fin-api`. Las revisiones independientes aprobaron ambos cambios. Se corrigieron dos hallazgos del prompt/contrato y quedaron registrados como fixed; un verificador calificó la exposición del texto libre como no probada en una ruta activa.
- `dotnet build src/LoLCoach/LoLCoach.csproj`: compilación correcta, 0 advertencias y 0 errores después de integrar la generación del recap.
- `rsc risk`: tier 1 para `panel-entrenamiento` y `limite-y-documentacion`. Las revisiones aprobaron la vista, la persistencia y la descripción del muestreo. La espera especial posterior a `RequestLimit` quedó retirada por `sesion-sin-limite-turnos`; el flujo normal ya continúa hasta cancelación o fin de datos.
- `git diff --check`: sin errores de formato.
- Las pruebas automáticas no se ejecutaron.
- No se generó un recap con un modelo real ni se hizo una prueba visual/interactiva en una partida; esas comprobaciones quedan pendientes.
- Riot documenta `eventdata` como una lista de eventos. Su ejemplo incluye `GameStart`, torres y objetivos, pero no muestra un evento de fin ni el resultado: https://developer.riotgames.com/docs/lol
- Por eso, una caída de Live Client Data no basta para afirmar que acabó la partida. El recap informa que la sesión de datos terminó, sin inventar el resultado.

## Next
Validar visualmente el botón y su uso durante una partida real; confirmar también el recap automático al perderse los datos y la comparación de estadísticas disponibles.
