# Decisiones

Formato: contexto → decisión → alternativas descartadas.

## 001 — Empezar de cero en lugar de rescatar CivilizacionSim
- Contexto: en CivilizacionSim la IA era decorativa y el dominio (mapa, regiones, imágenes) acaparaba el esfuerzo.
- Decisión: proyecto nuevo con dominio mínimo; la IA es imprescindible en cada fase.
- Se conservan dos ideas: "la IA propone, el dominio dispone" y "el juego sigue aunque el modelo falle".

## 002 — Microsoft.Extensions.AI como abstracción
- Contexto: se quiere aprender conceptos, no atarse a un proveedor.
- Decisión: `IChatClient` / `IEmbeddingGenerator` para todo; proveedores intercambiables por configuración.

## 003 — Consola y sin base de datos
- Contexto: cualquier infraestructura que no enseñe IA es distracción.
- Decisión: consola (Spectre.Console) y ficheros JSON. Sin Aspire salvo, si acaso, su dashboard standalone para telemetría.
