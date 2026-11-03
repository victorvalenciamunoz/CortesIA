# Reglas (mínimas a propósito)

No ampliar sin permiso del autor.

## Cortes

Tres: el jugador y dos de IA (Norte y Sur). Cada una tiene **grano**, **oro**, **soldados** y **prestigio**.
Inicio: 10 grano, 5 oro, 3 soldados, 0 prestigio.

## Turno

1. **Diplomacia:** cada corte puede enviar hasta 2 mensajes a cada una de las otras. Pueden proponerse pactos.
2. **Acciones:** cada corte elige UNA acción, en secreto y a la vez.
3. **Resolución** (determinista, con semilla para el azar del combate).
4. **Crónica:** se registran los eventos del turno.

## Acciones

- **Cultivar:** +3 grano.
- **Comerciar(con X, doy A, pido B):** si X ha propuesto este turno un intercambio compatible, se ejecuta.
- **Reclutar:** −2 oro, +2 soldados.
- **Fortificar:** este turno la defensa cuenta doble.
- **Atacar(X):** fuerza = soldados (+ azar de ±1). Si gana el atacante: roba la mitad del grano y del oro de X,
  +2 prestigio. Ambos pierden 1 soldado (el perdedor, 2).

## Mantenimiento

Al final del turno, cada 2 soldados consumen 1 grano. Sin grano suficiente, desertan soldados hasta cuadrar.
Si una corte no tiene oro, tras cultivar gana +1 oro (para no bloquearse).

## Pactos

- **No agresión (N turnos)** y **comercio recurrente (N turnos)**.
- Los pactos se registran en el dominio como datos estructurados.
- Cumplir un pacto completo: +1 prestigio a cada parte. Romperlo (atacar durante una no agresión,
  no comerciar en un comercio recurrente): −3 prestigio y todas las cortes se enteran.

## Fin

Al terminar el turno 15 gana el mayor prestigio (desempate: grano + oro + soldados).
Una corte sin soldados y sin grano queda eliminada.
