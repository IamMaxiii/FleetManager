# ts-map (código de terceros)

- **Origen:** https://github.com/dariowouters/ts-map, rama `master`, descargado el
  2026-10-07 (última actualización del repositorio: 2026-09-26).
- **Autor:** dariowouters y colaboradores. **Licencia:** MIT (archivo `LICENSE`).
- **Qué se usa:** solo la biblioteca `TsMap` (lee el mapa de los archivos del juego y
  lo dibuja), sin su aplicación de ventanas. Incluye `libdeflate.dll` (64 bits).
- **Cambios:** ninguno en el código. `TsMap.csproj` es nuestro, para compilarlo con
  .NET 10.
- Probado con Euro Truck Simulator 2 versión 1.61.1.1.
- **Aviso conocido (CA2022, silenciado):** `ZipEntry` lee de un `DeflateStream` sin
  comprobar cuántos bytes devuelve; en .NET moderno puede leer menos de lo pedido. Solo
  afecta a mods empaquetados en `.zip` (el juego base y los DLC usan otro formato). Si
  algún día se usan mods y el mapa sale mal, este es el primer sitio que mirar.
