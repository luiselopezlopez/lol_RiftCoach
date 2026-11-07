---
author: Luis Lopez
branch: feat/i18n-es-en-localization
status: in-progress
---
# Soporte multilingüe en castellano e inglés

## Intent
Permitir que una persona use Rift Coach en castellano (idioma actual) o inglés, con una selección persistente y coaching generado en el idioma elegido.

## Scope
- Incluye selector de idioma, persistencia y cambio visible durante la sesión.
- Incluye textos visibles, accesibles, estados, errores, cuestionario y contenido de coaching propio de la aplicación.
- Incluye instrucciones de idioma para el modelo, fichas de habilidades de Data Dragon y voz compatible con el idioma elegido.
- Excluye añadir idiomas distintos de `es` y `en`, y cambiar el idioma del cliente o de la API de League en ejecución.

## Checklist
- [ ] Añadir catálogo es/en y permitir el cambio dinámico; prueba: cambiar idioma actualiza ventanas abiertas.
- [ ] Guardar el idioma elegido junto a los ajustes; prueba: una preferencia se conserva al reiniciar y los ajustes antiguos mantienen castellano.
- [ ] Localizar la interfaz, accesibilidad, estados, errores y cuestionario; prueba: ambos idiomas muestran texto coherente en las ventanas principales y de ajustes.
- [ ] Localizar coaching generado, fichas de habilidad y voz; prueba: el prompt, Data Dragon y la voz usan el idioma elegido o un fallback explícito.
- [ ] Revisar expansión de texto y codificación; prueba: el inglés largo cabe en las ventanas y caracteres acentuados se conservan.

## Evidence
- Worktree limpio creado en `E:/experimentos/lol/.worktrees/LoLCoach-i18n-es-en-localization`, desde `origin/main`.
- Implementados selector y preferencia persistente, localización de interfaz y ajustes, prompt de coaching, Data Dragon y voz por idioma.
- La primera revisión independiente señaló tres textos incompletos en inglés; se localizaron la antigüedad del consejo, el valor de rivales y la pestaña del minimapa.
- La segunda revisión encontró una orden inglesa de regreso a base que el validador rechazaba y una cabecera de tiempo sin localizar; se añadieron variantes de regreso y la traducción de la cabecera.
- También se localizan ambos nombres de idioma, los atributos del campeón, el aviso legal y la cultura numérica según la selección.
- La tercera revisión independiente no encontró P1; señaló dos ligaduras dinámicas sin localizar («Tu campeón» y los valores de atributos), corregidas en XAML con el catálogo existente.
- `rsc risk` clasificó el cambio como tier 1 con una persona revisora. Se alcanzó el máximo de tres rondas de revisión; las dos correcciones finales quedan sin otra ronda independiente.
- No se ejecutaron pruebas ni compilación; esta petición no solicitó verificación ejecutable.

## Next
Revisar visualmente ambos idiomas y completar los criterios de aceptación cuando se autorice compilar y ejecutar la aplicación.
