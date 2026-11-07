---
author: Luis Lopez
branch: main
status: in-progress
---
# Recap de partida

## Intent
Al terminar una partida de práctica, mostrar en Entrenamiento un análisis amplio del desempeño, la comparación con los otros nueve jugadores y recomendaciones concretas para la siguiente partida.

## Scope
- Detectar el fin de una partida verificada contra bots y no confundirlo con pausar, perder la verificación, cerrar la app o un fallo temporal.
- Mantener en memoria muestras resumidas de la plantilla durante la sesión para comparar progreso y resultado final sin conservar capturas en disco.
- Pedir al modelo un recap con estadísticas propias, comparación por equipo y partida, tendencias observadas, fortalezas que conviene repetir, errores o riesgos respaldados por datos y lecciones prácticas.
- Mostrar el recap completo en la ventana Entrenamiento, separado de los consejos breves y conservado hasta la siguiente partida o el cierre de la app.
- Marcar los datos no disponibles y no inferir victoria, acciones seguidas ni decisiones invisibles.
- Mantener el alcance de entrenamiento contra bots que ya tiene la app. El recap añade una solicitud al modelo al final de la partida.

## Checklist
- [ ] Detectar el fin real de la partida; prueba: una partida terminada activa el recap y pausa, error temporal y pérdida de verificación no lo activan.
- [ ] Acumular y resumir estadísticas comparables por jugador a lo largo de la partida; prueba: el resumen conserva marcas temporales y valores finales, y omite campos ausentes.
- [ ] Generar el análisis final a partir de los datos capturados; prueba: el contrato incluye comparación con aliados y rivales, aciertos repetibles, prioridades de mejora y límites de evidencia.
- [ ] Mostrar el recap largo en Entrenamiento; prueba: el texto no comparte los límites ni el descarte de los consejos cortos y sigue legible en el panel.
- [ ] Compilar la aplicación; prueba: `dotnet build src/LoLCoach/LoLCoach.csproj` termina correctamente.

## Evidence
- `GameSnapshot` ya recoge nivel, K/D/A, súbditos, visión, campeones, equipos e inventario de hasta diez jugadores.
- `CoachingSession` termina tanto por límite como por error y cancelación; la causa del fin de partida todavía no está diferenciada.
- Entrenamiento conserva consejos cortos en memoria y usa un `ItemsControl`; no dispone de una vista para un informe largo.
- Riot documenta `eventdata` como una lista de eventos. Su ejemplo incluye `GameStart`, torres y objetivos, pero no muestra un evento de fin ni el resultado: https://developer.riotgames.com/docs/lol
- Por eso, una caída de Live Client Data no basta para afirmar que acabó la partida. El detector necesita otra señal verificable o debe presentar el resultado como recap provisional de la sesión interrumpida.
- Ningún archivo de código se ha modificado.

## Next
Elegir la rama de trabajo según la política local y fijar el criterio de fin de partida antes de implementar el detector.
