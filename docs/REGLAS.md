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

## Aclaraciones (provisionales, a revisar al jugar)

Huecos de las reglas cerrados para que la resolución sea determinista.

**Orden del turno.** 1) Cultivar y Reclutar → 2) Comercio → 3) Combates → 4) Mantenimiento → 5) Pactos
(cumplidos o rotos) → 6) Eliminaciones y fin. Los reclutas del turno ya defienden; el grano recién cultivado se puede robar.

**Combate.**
- Cada bando tira por su cuenta −1, 0 o +1, con la misma probabilidad.
- Empate de fuerzas: gana el defensor.
- Fortificar: fuerza del defensor = soldados × 2 + tirada.
- Ataque mutuo: un solo combate y nadie fortificado. El ganador roba al perdedor; si empatan, ambos pierden 1 soldado y nadie roba.
- Varios combates en el mismo turno: uno detrás de otro, cada uno con el estado que deja el anterior, en orden sorteado con la semilla.
- "La mitad" se redondea hacia abajo.
- Para atacar hace falta al menos 1 soldado. Nadie baja de 0 soldados.

**Comercio.**
- Se ejecuta solo si las dos cortes eligen Comerciar entre sí con términos en espejo (yo doy 3 grano y pido 2 oro;
  tú das 2 oro y pides 3 grano). Gasta la acción de ambas; la diplomacia solo sirve para ponerse de acuerdo.
- Solo grano y oro, cantidades de 1 o más, un recurso distinto en cada lado.
- Si al ejecutarse falta el recurso, no se hace y queda registrado.

**Mantenimiento.**
- Grano consumido = soldados / 2 redondeado hacia arriba (3 soldados comen 2).
- El +1 de oro: si la corte cultiva este turno y tiene 0 de oro.

**Pactos y diplomacia.**
- En la Fase 0 los pactos se proponen como datos (tipo, N turnos y, en comercio, los términos). Quien los recibe
  acepta o rechaza en el momento; la respuesta no gasta mensaje. Hablan en orden fijo: jugador, Norte, Sur.
  El texto libre se guarda pero no tiene efecto.
- Un pacto aceptado en el turno T dura de T a T+N−1 y acaba como muy tarde en el turno 15.
- Atacar con no agresión: el ataque se hace; quien lo rompe pierde 3, el pacto se anula y se entera todo el mundo.
- Comercio recurrente: cada turno ambas partes juegan el Comerciar acordado. Quien no lo juega o no puede pagar lo rompe
  (si fallan los dos, lo rompen los dos).
- Como mucho un pacto de cada tipo por pareja a la vez. El prestigio puede ser negativo.

**Fin.**
- La eliminación se comprueba tras el mantenimiento. La corte eliminada no actúa, no se la puede atacar, sus pactos
  se anulan sin premio ni castigo y queda última. Si solo queda una viva, la partida termina.
- Empate también en el desempate: victoria compartida.

**Información.** Los recursos de todas las cortes son públicos. Las acciones se revelan tras resolver. Mensajes y
pactos solo los ven las partes; combates y rupturas de pacto, todo el mundo.
