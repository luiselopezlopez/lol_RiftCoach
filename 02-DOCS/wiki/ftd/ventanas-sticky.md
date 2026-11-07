---
author: Luis Lopez
branch: feat/sticky-panels
status: in-progress
---
# Encaje magnético de paneles

## Intent
Hacer que los paneles flotantes se atraigan al acercar sus bordes, para formar una disposición alineada sin ajustar posiciones a mano.

## Scope
- Al mover un panel, encajarlo con los bordes y alineaciones de los otros paneles abiertos cuando estén a 12 DIP o menos.
- Atraer también los bordes del área de trabajo.
- Excluir el ajuste durante el redimensionado y las ventanas nativas fuera del área de trabajo.

## Checklist
- [ ] Añadir el encaje durante el movimiento; prueba: los bordes cercanos quedan alineados y los paneles siguen dentro del área alcanzable.
- [x] Compilar la aplicación; `dotnet build src/LoLCoach/LoLCoach.csproj` terminó correctamente.

## Evidence
- Confirmado que `FloatingPanel` gestiona arrastre, límites y eventos de cambio de diseño.
- La rama `feat/sticky-panels` se creó desde `main` en `.worktrees/feat-sticky-panels`.
- `dotnet build src/LoLCoach/LoLCoach.csproj`: 0 advertencias y 0 errores.
- `git diff --check`: sin errores de formato.
- `rsc risk`: tier 1.
- La primera revisión detectó que los movimientos cortos repetidos no soltaban el ajuste; se corrigió acumulando el desplazamiento del arrastre.
- La segunda revisión aprobó el diff con una nota: falta comprobar el comportamiento visual manualmente.
- La comprobación interactiva del encaje y las pruebas automáticas no se ejecutaron.

## Next
Comprobar manualmente el ajuste entre paneles, las esquinas y la liberación tras varios movimientos cortos.
