---
author: Luis Lopez
branch: feat/voz-preguntas
status: in-progress
---
# Preguntas por voz durante la partida

## Intent
Durante una sesión de entrenamiento, el jugador puede pedir ayuda hablando. La app reconoce frases que empiezan por «LoL» y agrega el texto restante al siguiente paquete enviado al modelo.

## Scope
- Activar y detener la escucha desde la interfaz durante una sesión.
- Reconocer voz localmente en el idioma de la app; no guardar ni enviar audio.
- Aceptar solo frases cuyo primer término sea «LoL», ignorando mayúsculas y puntuación inicial; agregar la pregunta reconocida a una sola solicitud posterior.
- Mostrar el estado de escucha y cualquier error de reconocimiento.
- Mantener las restricciones actuales de tipos de partida y envío de observaciones.

## Checklist
- [ ] Control accesible para activar/desactivar el reconocimiento durante la sesión; probar inicio, parada y limpieza al finalizar.
- [ ] El reconocedor entrega solo las frases con palabra clave y la pregunta se adjunta al siguiente paquete; comprobar frase aceptada, frase ignorada y consumo único.
- [x] La solicitud al modelo identifica claramente la pregunta hablada sin incluir audio; revisar el contenido construido.
- [x] Compilar la aplicación y registrar el resultado. No ejecutar pruebas automatizadas salvo petición explícita.

## Evidence
`dotnet build LoLCoach.slnx`: correcto, 0 advertencias y 0 errores. `rsc risk`: tier 1. Revisión independiente final: pass. Dos hallazgos de ciclo de vida se corrigieron y cerraron en el ledger. No se ejecutaron pruebas automatizadas ni una partida real; faltan la validación del micrófono y la prueba del envío durante una partida.

## Next
Validar el idioma y el micrófono desde el worktree en una sesión de práctica; comprobar la palabra clave, el siguiente envío, el apagado de escucha y el error ante desconexión del micrófono.
