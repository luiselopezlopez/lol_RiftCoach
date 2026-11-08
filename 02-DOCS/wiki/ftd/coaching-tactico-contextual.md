---
author: Luis Lopez
branch: feat/match-recap
status: in-progress
---
# Coaching táctico contextual

## Intent
Hacer que el coach emita tácticas solo cuando añadan una decisión útil, valore a todo el equipo rival —sobre todo en mid y late— y recomiende rotaciones o campamentos de jungla cuando convengan.

## Scope
- Omitir la segunda línea táctica en situaciones rutinarias, sin cambios útiles o cuando repita una recomendación reciente.
- Elegir amenazas entre todos los rivales conocidos y visibles, sin asumir que el rival de línea es siempre el más relevante.
- En mid/late, considerar y nombrar a los rivales relevantes de toda la composición durante peleas de equipo y objetivos; admitir varios nombres en la táctica.
- Añadir la posición/rol conocida de cada rival al contexto del modelo y distinguir el rol declarado de su ubicación actual.
- Recomendar apoyo a otra línea o farmear un campamento solo cuando rol, urgencia, oleada, seguridad, distancia y evidencia disponible lo justifiquen.
- No inventar ubicaciones, disponibilidad de campamentos, habilidades o enfriamientos; no ordenar atacar a un rival oculto.
- Mantener la guía de emparejamiento directo limitada al rival confirmado de la misma posición.

## Checklist
- [ ] Confirmar en una sesión real que la línea táctica se omite en turnos rutinarios y aparece solo ante decisiones concretas.
- [ ] Confirmar en mid/late que el informe táctico usa y nombra rivales relevantes de otras posiciones.
- [ ] Confirmar en una sesión real recomendaciones oportunas de apoyo a otras líneas y campamentos, sin desviar la ruta del jungla aliado.
- [x] Compilar la aplicación; registrar si las pruebas no se ejecutaron.

## Evidence
- `CoachingPrompt.txt` pide una segunda línea cuando aparece cualquier enemigo y prioriza explícitamente la amenaza más cercana o el rival de línea.
- Las reglas de `FoundryCoachingService` también asocian enemigos visibles con una táctica, aunque el validador ya permite una sola línea.
- `MatchTacticalContext.Enemies` entrega todos los rivales, pero `EnemyTactics` omite su posición/rol.
- El prompt ya permite apoyo a otra línea y campamentos visibles para jungla; restringe campamentos de jungla para campeones de línea.
- `EnemyTactics` ahora incluye la posición normalizada de todos los rivales. El prompt de coaching y las reglas del servicio permiten omitir la línea táctica, valorar toda la composición y mencionar varios rivales relevantes en mid/late.
- El contrato táctico permite hasta 30 palabras para incluir amenazas múltiples; siguen vigentes los límites de objetivos visibles y de evidencia de campamentos.
- `dotnet build src/LoLCoach/LoLCoach.csproj --no-restore`: compilación correcta, 0 advertencias y 0 errores.
- `git diff --check`: sin errores de formato.
- `rsc risk`: tier 1. La revisión independiente de ronda 1 aprobó el comportamiento y encontró una discrepancia de privacidad en el README; el hallazgo se confirmó y se corrigieron dos frases. La ronda 3 aprobó el diff restante; el ledger registra 8 hallazgos fijados y 0 abiertos.
- No se ejecutaron pruebas automáticas ni una partida real; la verificación de comportamiento queda pendiente.

## Next
Validar en una partida real que la táctica solo aparece ante decisiones útiles, nombra rivales relevantes fuera de línea en mid/late y recomienda apoyo o campamentos sin desviar la ruta del jungla aliado.
