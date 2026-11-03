# Visión

## Objetivo

Fijar los conceptos básicos de la programación con IA en .NET, de forma que al terminar haya:

1. **Una base de conocimiento propia** (`docs/conceptos/`), escrita por el autor, a la que volver.
2. **Un código de referencia** para cada concepto, pequeño y claro, reutilizable en proyectos futuros.
3. **Números reales** sobre lo que puede y no puede hacer cada modelo (local frente a nube).

## El juego (hilo conductor)

Tres cortes: la tuya y dos gobernadas por IA. Cada turno se negocia en lenguaje natural y luego cada corte actúa.
Las cortes de IA tienen carácter propio, recuerdan lo que les hiciste y deciden consultando el estado del juego
con herramientas. Un cronista narra la partida y responde preguntas sobre lo que ha pasado.

El juego es deliberadamente pequeño (ver REGLAS.md). Si en algún momento resulta divertido, mejor;
pero no es el objetivo.

## Lección aprendida de CivilizacionSim

Allí el juego (mapa, regiones, imágenes) se comió el aprendizaje y la IA quedó de adorno: si se quitaba,
el juego funcionaba igual. Aquí es al revés: cada concepto de IA entra porque el juego lo necesita,
y el juego no crece más allá de lo necesario.

## Conceptos a cubrir (una fase cada uno)

Cliente de chat y proveedores · salidas estructuradas · herramientas y bucle de agente · prompt de sistema,
memoria y contexto · conversación y extracción de acuerdos · seguridad (prompt injection) · embeddings y RAG ·
evaluaciones · comparación de modelos · MCP.
