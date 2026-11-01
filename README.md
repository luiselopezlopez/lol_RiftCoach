# Rift Coach

Aplicacion Windows en C# / .NET 10 para empezar a aprender League of Legends.

## Estado de esta primera version

La aplicacion arranca en Entrenamiento, carga la configuracion local, pregunta al modelo si esta disponible y, solo cuando responde, empieza a consultar la API de LoL cada cinco segundos. Al detectar una partida contra bots verificable, localiza la ventana del juego y envia automaticamente capturas completas y estadisticas propias para recibir consejos en streaming y voz espanola. No hay modo de simulacion ni ventana de Conexion.

Las capturas son **sin censura**. Durante la partida, cada respuesta completada dispara una captura y estadisticas nuevas para el siguiente envio, sin espera fija de cinco segundos. Inicio automatico y envio continuo estan activados por defecto; no se pide autorizacion adicional de imagen o coste. Se conserva la comprobacion de rivales bot, y PvP o modos no verificables no inician el envio.

Esta modalidad experimental se incorpora a peticion del usuario y no implica aprobacion de Riot. Riot incluye las aplicaciones que dictan decisiones entre sus casos no aprobados; jugar contra bots no garantiza una excepcion. Sigue siendo necesario revisar y registrar el alcance con Riot. El prompt de consejos mantiene los cambios del usuario. La aplicacion no controla el personaje, inyecta codigo, lee memoria ni intenta eludir Vanguard.

## Ejecutar

Requisitos: Windows con escritorio interactivo, SDK .NET 10 y acceso a NuGet para compilar.

```powershell
dotnet run --project src/LoLCoach/LoLCoach.csproj
```

Tambien se genera una aplicacion independiente del SDK:

```powershell
dotnet publish src/LoLCoach/LoLCoach.csproj -c Release -r win-x64 --self-contained true -o artifacts/app
```

Abre el ejecutable `LoLCoach.exe` de la carpeta de publicacion.

## Ventanas

La interfaz se compone de ventanas flotantes dentro de la ventana principal. Cada una se mueve arrastrando su barra de titulo, se redimensiona desde los bordes izquierdo, derecho, inferior o las esquinas inferiores, y se cierra con la X. El menu **Ventanas** permite volver a abrir cualquier ventana cerrada o restablecer la disposicion por defecto.

1. **Campeon y miniguia**: campeon elegido, miniguia de habilidades y estadisticas propias en partida.
2. **Entrenamiento**: historial de los ultimos 30 consejos.
3. **Campeones**: un campeon por columna en horizontal, con los rivales primero.
4. **Sesion, partida y depuracion**: Iniciar/Pausar y pestañas de sesion, datos de partida, depuracion de API y la captura de pantalla enviada.
5. **Recomendacion de campeon y posicion**: cuestionario. Se abre por defecto solo si no hay perfil guardado.

Posicion, tamaño, orden y estado abierto/cerrado de cada ventana se guardan en `Layout` dentro de `localsettings.json` al cerrar el programa y se restauran al abrirlo.

## Primer uso

1. Configura Endpoint, Model y ApiKey en el archivo local indicado abajo. No compartas claves por chat ni en el repositorio.
2. Abre la aplicacion. Envia al modelo una pregunta simple de texto, sin imagen, con un tiempo de espera maximo de 30 segundos. Si falla, muestra el error y no empieza a consultar LoL.
3. Cuando el modelo responde, espera la API local cada cinco segundos. Al aparecer una partida valida contra bots y una unica ventana visible de League of Legends, comienza el envio automaticamente. No hace falta pulsar Iniciar ni completar el cuestionario.
4. Partida muestra campeon propio, nivel, vida, porcentaje restante, oro, tiempo de partida y verificacion de rivales, con hora de actualizacion.
5. Depuracion separa las consultas a LoL y Azure OpenAI: hora, metodo, ruta, estado HTTP, tiempo hasta la respuesta y resultado. La respuesta a la pregunta inicial y la ruta local de configuracion tambien aparecen aqui.
6. Pausar cancela espera, captura/inferencia en curso y voz, pero no borra los mensajes del historial. Iniciar vuelve a cargar localsettings y repite la comprobacion inicial, manteniendo los mensajes de la ventana. Tu perfil sigue disponible como cuestionario opcional; no bloquea el flujo.

**Autoplay de voz en espanol** esta activado inicialmente si hay una voz SAPI compatible instalada. Lee automaticamente los consejos completos. Puedes silenciarlo, cambiar voz y volumen. Las voces naturales de Windows no siempre estan disponibles para System.Speech; no se elige ingles como sustituto. Autoplay no controla el personaje.

## Configuracion local

La aplicacion busca `localsettings.json` primero junto al ejecutable, despues en la carpeta de trabajo y despues en las carpetas superiores del ejecutable (por ejemplo, la raiz del proyecto). Si no encuentra ninguna de esas copias, usa `%LOCALAPPDATA%/LoLCoach/localsettings.json`. La ruta realmente utilizada aparece en Depuracion. No se ignora la configuracion del proyecto en favor de una copia antigua de AppData. `Model` es el nombre del despliegue que se envia a Azure, no necesariamente el nombre comercial del modelo. No hay un formulario de configuracion dentro de la aplicacion.

El formato esta en [localsettings.example.json](localsettings.example.json):

```json
{
	"Endpoint": "https://luisellmcontainer.openai.azure.com/openai/v1/",
	"Model": "gpt-6-luna",
	"ApiKey": "",
	"RequestLimit": 12,
	"Profile": null
}
```

Puedes rellenar `ApiKey` manualmente y reiniciar o pulsar Iniciar; en ese caso el secreto queda en texto claro en tu archivo local. Las claves previamente guardadas con formato `dpapi:...` siguen admitidas y se descifran para el usuario actual de Windows. La clave nunca se muestra ni se registra. Para borrarla, deja el campo vacio en el archivo; esto no la revoca en Azure.

`Profile` es opcional: puede omitirse, ser `null`, una cadena vacia `""` o un objeto de perfil guardado por el cuestionario. Un perfil vacio no bloquea la carga de Endpoint, Model o ApiKey. Otros valores de perfil incompatibles muestran un error de formato sin revelar credenciales.

Los anteriores `settings.json` y `credential.bin` se migran automaticamente a `localsettings.json`, conservando perfil y clave cifrada, y se retiran despues de guardar el nuevo archivo. Una configuracion local existente tiene prioridad. No se incluyen claves en el ejecutable ni en el repositorio; `localsettings*.json` esta ignorado por Git, excepto la plantilla sin secretos. Las ediciones manuales del archivo se cargan al reiniciar.

## Progresion de campeones

El selector de Tu perfil incluye los 173 campeones del catalogo [lol_coach_data.json](src/LoLCoach/lol_coach_data.json), version 16.21.1, incorporado como recurso del ejecutable. El archivo original suministrado no se modifica. Para actualizar el catalogo incluido hay que reemplazar esta copia y recompilar.

Cada consulta al modelo incluye solo la build objetivo, objetos iniciales, orden de habilidades y secuencia de niveles del campeon actual. El campeon verificado en partida prevalece sobre el perfil; el rol se obtiene de la posicion real o del perfil, con el rol principal del catalogo como alternativa. La build y el rol permanecen fijos durante esa sesion. Los objetos pendientes excluyen los ya comprados por su ID, y los objetos activos incluyen su tecla segun el slot del inventario.

Con los cuatro rangos de habilidades disponibles se calculan los puntos sin gastar y la siguiente mejora legal para campeones de progresion estandar, respetando desbloqueos, limites de rango y niveles de la R. Para Aphelios, Jayce, Elise, Nidalee, Karma y Udyr se envia la secuencia del catalogo sin deducir puntos o mejoras con reglas genericas. Los campos ausentes permanecen desconocidos. Se aceptan ordenes como `Mejora Q`, `Ataca Canon` y `Usa 3+Q`, sin automatizar teclas del juego.

El catalogo no contiene precios ni arboles de componentes: la compra asequible y los enfriamientos deben comprobarse en la tienda y los iconos visibles. No se inventan precios ni se garantiza que las builds suministradas sean optimas o validas para todas las situaciones. El contrato de respuestas sigue siendo una orden de una linea; los consejos separados descritos en el prompt todavia no se procesan.

Las ordenes de combate indican el objetivo y como atacar: `Ataca Garen con E+Q` o `Ataca Garen con 3+Q`. El combo se decide segun las habilidades aprendidas, los objetos equipados y su disponibilidad visible; el orden de subir habilidades no se interpreta como un combo de ataque. Si no hay habilidades disponibles con certeza, se indica `Ataca Garen con clic derecho`. Las torres y la retirada usan ordenes concretas como `Ataca Torre`, `Alejate de Torre` y `Huye de Garen`, sin recomendar ataques bajo una torre enemiga sin proteccion de una oleada aliada. Para distinguir torres aliadas de enemigas, el prompt usa el color de la barra de vida y del minimapa (azul aliada, rojo enemiga), quien recibe los disparos y `lado_mapa`, que indica el equipo del jugador (ORDER azul con base abajo a la izquierda, CHAOS rojo con base arriba a la derecha). Si la torre no se puede identificar, no se ordena atacarla.

En cada turno el prompt pide observar el minimapa para decidir ayudas, defensa de torres y objetivos de jungla antes de atacar subditos por rutina. Puede indicar `Apoya Bot`, `Apoya Mid`, `Ve a Dragon`, `Ataca Dragon`, `Ve a Azul` o `Ataca Gromp` solo con evidencia visible, una ruta viable y sin abandonar una defensa urgente. La orden generica `Farmea` se rechaza. El detalle de imagen `high` mejora el tratamiento de iconos pequenos y puede aumentar tokens, coste y latencia; no recupera informacion que ya se perdio al comprimir o reducir la captura. No se deducen posiciones ocultas, campamentos vivos o reapariciones solo por el tiempo de partida. Estas reglas y el contrato se prueban con transporte controlado; la calidad de las decisiones sobre un minimapa real requiere evaluacion en partida.

## Fase, rivales y miniguia

El contexto de cada orden incluye una fase tactica estimada con minutos y nivel: early mientras el tiempo es menor de 14 minutos y el nivel menor de 9; mid a partir de cualquiera de esos umbrales; late desde 28 minutos con nivel 14 o superior, o desde 35 minutos aunque el campeon vaya retrasado. Si faltan ambos datos, la fase es desconocida. No es una medida exacta del estado global: el prompt debe adaptar intercambios, apoyo, objetivos y posicion al rol, habilidades aprendidas, objetos y ventaja de los rivales.

Al detectar una partida verificada se prepara una miniguia de unas 180 palabras (se rechazan respuestas desbordadas de mas de 320 palabras o 3200 caracteres), antes de comenzar las capturas e instrucciones. Aparece en un panel separado y describe el papel del campeon, pasiva, Q/W/E/R, cuando usarlas, un combo sencillo, una precaucion frente a los enemigos y el plan de early/mid/late. Las descripciones se obtienen de Data Dragon es_ES en la version del catalogo de progresion y se guardan en cache. Si faltan, el modelo debe indicar la limitacion sin inventar habilidades. La ficha disponible se reutiliza como contexto de las siguientes ordenes.

La guia tiene un plazo total de 30 segundos y supone una consulta adicional de pago al modelo por inicio de sesion, aparte de la comprobacion de disponibilidad y del contador de capturas. Si falla o agota el plazo, se muestra el aviso y continua el entrenamiento con una captura fresca. Pausar cancela tambien la guia. No se lee automaticamente en voz para evitar interrumpir las instrucciones de combate. Las compras, mejoras, combos y restricciones de bots/PvP se mantienen.

## Practicar contra bots

Cada consulta lee `activeplayer`, `gamestats` y `playerlist`, localiza al jugador humano por Riot ID, identifica su equipo y exige que todos los rivales declaren `isBot=true`. Puede haber aliados humanos, como en cooperativo contra IA. Una partida sin rivales o con datos ambiguos no se admite. Una respuesta HTTP por si sola no equivale a verificacion de bots.

Si la partida ya estaba abierta, empieza despues de que responda la pregunta inicial. Si la API no esta disponible o no se pueden verificar los bots, sigue esperando sin enviar capturas a Azure. Si faltan campos durante la sesion, aparece un rival humano, se cierra/minimiza el juego, no llega una captura nueva o falla la conexion, se detiene y descarta el consejo en curso.

Cada arranque permite una sesion: al terminar, fallar o alcanzar el limite si no es continuo, se desactiva. Para otra partida, pulsa Iniciar o vuelve a activar Inicio automatico. No se reinicia el presupuesto ni se lanza otra sesion en bucle. La ventana del juego se detecta por su proceso, sin seleccionar otra aplicacion ni capturar el escritorio.

Los Riot IDs, nombres de invocador y la lista completa de jugadores no se incluyen en el JSON enviado, pero los nombres, mensajes y demas datos visibles en la captura completa **si pueden enviarse en la imagen**. Azure recibe un JPEG de hasta 1280 pixeles de ancho con detalle `high`, datos propios filtrados, el plan del campeon, la fase estimada y una lista limitada de rivales: nombre del campeon, nivel, K/D/A, estado muerto y nombres de objetos. No se envian identidades humanas, posiciones, vida ni enfriamientos enemigos desde la API. Los campos ausentes permanecen desconocidos. La guia inicial recibe estos datos limitados y las descripciones publicas de las habilidades, sin captura. La verificacion de bots no implica aprobacion de Riot ni deteccion instantanea de cambios entre muestreos.

El endpoint inicial es el facilitado por el usuario. `gpt-6-luna` se trata como nombre de despliegue, no como una afirmacion de que exista un modelo publico con ese nombre. No se ha comprobado su disponibilidad ni capacidad sin tu credencial. La prueba detecta rechazos de autenticacion, modelo, imagen y streaming. No hay un agente alojado ni recursos Azure nuevos.

## Cadencia, coste y voz

- La deteccion previa de partida sigue consultando LoL cada cinco segundos. Una vez iniciada, el ciclo es secuencial: captura y estadisticas nuevas, envio, respuesta completa y siguiente captura inmediata. No hay temporizador de envio, precapturas pendientes ni peticiones simultaneas. No es un flujo de video.
- Las respuestas de mas de 10 segundos se cancelan/descartan para evitar consejos caducados. Pausar detiene solicitudes y voz; los mensajes ya recibidos permanecen visibles y los borradores incompletos se marcan como no vigentes. Iniciar crea una sesion nueva con su contador nuevo, sin borrar el historial.
- Cada respuesta tiene una entrada con hora y estado. Sus fragmentos actualizan esa misma entrada; el siguiente consejo aparece debajo. La vista se desplaza automaticamente al mensaje mas reciente. El historial se conserva en memoria con los ultimos 30 mensajes (los mas antiguos se eliminan), sin escritura en disco; al cerrar la aplicacion se pierde. Esas mismas 30 ultimas ordenes completas se envian al modelo como contexto en cada peticion.
- Solo las respuestas completas, de una linea y hasta 30 palabras se pueden leer en voz alta. El validador de formato no verifica la exactitud ni sustituye una revision humana o de Riot.
- La inferencia dispone de hasta 2048 tokens de generacion para evitar truncados del modelo, aunque el consejo visible sigue limitado a 30 palabras. Las respuestas truncadas o filtradas muestran un motivo especifico. El maximo de tokens no implica que se consuman siempre todos y no garantiza que cualquier modelo complete la respuesta dentro del plazo de frescura.
- El autoplay de voz lee cada consejo final nuevo, sin el anterior enfriamiento de 10 segundos. No acumula una cola: cancela la frase previa cuando llega otra valida. Evita repeticiones exactas consecutivas y Pausar silencia inmediatamente.
- La frecuencia y el consumo dependen ahora del tiempo de captura y respuesta del modelo; pueden superar la antigua cadencia de doce llamadas por minuto. **Envio continuo** no se detiene por numero de solicitudes: dura hasta pausar, cerrar, finalizar la partida o encontrar un error/estado no verificable. Si se desactiva, se usa RequestLimit de localsettings, entre 1 y 720. La pregunta inicial es una solicitud de pago aparte del contador de capturas.
- El precio depende del despliegue, tokens de entrada/imagen/salida y region. Esta version muestra numero de solicitudes, tamano de imagen y latencia de frase completa, **no factura ni estima euros**. Cancelar puede generar cargos. Se usa el control de gasto externo configurado por el usuario; la aplicacion no aplica un limite monetario.
- Los budgets de Azure Cost Management normalmente generan alertas; por si solos no detienen el servicio al alcanzar el importe. Si necesitas un corte efectivo, debes configurar y comprobar un mecanismo de bloqueo/automatizacion independiente. La aplicacion no ha verificado ese mecanismo ni cambia tu suscripcion.
- Ante 429 o un error de Azure se detiene la sesion, sin reintentos automaticos ni acumulacion. Espera el periodo indicado por tu servicio antes de volver a iniciarla.

## Diagnostico y privacidad

La captura usa Windows.Graphics.Capture y Direct3D, con indicador del sistema. No captura todo el escritorio ni guarda imagenes. Una ventana minimizada/cerrada o una captura caducada detiene el envio. HDR y configuraciones de HUD distintas requieren mas validacion.

Depuracion conserva como maximo 200 registros en memoria por API. El tiempo HTTP es hasta recibir los encabezados/respuesta inicial; la latencia de la frase completa se muestra en Entrenamiento. No se registran cabeceras, API keys, parametros de consulta, cuerpos completos, capturas ni listas de jugadores. El boton de papelera limpia ambas listas. Los fallos antes de una consulta, por ejemplo falta de ApiKey, se muestran como estado, no como una respuesta HTTP ficticia.

La captura conserva toda la ventana del juego, sin mascaras ni censura. Puede incluir chat, nombres, marcadores y notificaciones. Revisa la vista y evita mostrar informacion privada antes de iniciar la practica. El modelo recibe toda la imagen visible, redimensionada y comprimida como JPEG, pero no dispone de datos ocultos del juego. Se le indica que ignore instrucciones presentes en el chat y que no repita datos personales; esto no anonimiza la imagen ni garantiza que no los reconozca.

La escucha consulta `activeplayer`, `gamestats` y `playerlist` en `https://127.0.0.1:2999/liveclientdata/`. Solo esta disponible durante una partida y no necesita Riot API key. La app descarga el certificado raiz publicado por Riot por HTTPS y lo usa en un cliente HTTP dedicado conservando la validacion de nombre. No desactiva TLS globalmente ni modifica el almacen de certificados de Windows. No utiliza LCU.

La API no ofrece un estado completo del mapa, posiciones ocultas ni enfriamientos fiables de enemigos. Se normaliza una lista permitida de campos propios; los campos ausentes permanecen desconocidos. La conexion real con LoL no se ha verificado si no hay partida abierta.

Los datos se guardan bajo `%LOCALAPPDATA%/LoLCoach`: `localsettings.json` con configuracion, perfil y clave, ademas del catalogo/retratos/fichas de habilidades publicos. El codigo no registra claves ni guarda capturas. No hay telemetria externa. Las imagenes y estadisticas enviadas a Azure estan sujetas a la configuracion de privacidad y retencion de tu servicio. El catalogo y los retratos se descargan de Riot al mostrar resultados del cuestionario; la ficha de habilidades se descarga al preparar la miniguia si no esta en cache.

## Verificacion

```powershell
dotnet test LoLCoach.slnx
```

Las pruebas no usan claves reales ni llaman a Azure. Incluyen 6561 perfiles posibles, transporte Azure controlado, configuracion/migracion/cifrado, filtro JSON, bots/PvP/datos ambiguos, pixeles sin censura, captura fresca/negra, reloj falso para sesion continua y deteccion, orden de arranque y registros HTTP sin secretos. Se verifica que el siguiente paquete se captura inmediatamente tras la respuesta sin avanzar un temporizador, y que solo hay una inferencia activa. DashboardTests comprueba historial retenido tras pausa/reinicio, limite de 30 mensajes, autodesplazamiento, arranque, datos, depuracion y voz. Requiere una sesion grafica Windows activa; sus PNG solo muestran datos de prueba.

Para verificar ademas los retratos reales de Data Dragon en la prueba de escritorio:

```powershell
$env:LOLCOACH_TEST_ASSETS = '1'
dotnet test tests/LoLCoach.Tests/LoLCoach.Tests.csproj --filter FullyQualifiedName~DashboardTests
Remove-Item Env:LOLCOACH_TEST_ASSETS
```

Pendiente: validar el despliegue real con credencial introducida por el usuario, piloto de 30 minutos, sesion real de LoL y distintas escalas DPI/HDR/monitores. La evaluacion humana de consejos y la revision de Riot siguen pendientes; la modalidad experimental no debe presentarse como aprobada. Este prototipo no garantiza recomendaciones correctas ni sustituye al jugador.

## Revision de Riot

Descripcion de alcance para someter a revision: herramienta educativa en espanol para principiantes, con un cuestionario local; observa captura del cliente y estadisticas propias de Live Client Data en ciclos secuenciales ligados a la respuesta del modelo y muestra consejos en historial, ventana separada y voz, sin automatizar el personaje ni acceder a datos ocultos. Debe describirse expresamente la IA, los datos enviados a Azure, las restricciones de modo y los ejemplos exactos de consejos. No se presentara como aprobada hasta obtener una respuesta favorable para ese alcance.

- https://developer.riotgames.com/docs/lol
- https://developer.riotgames.com/policies/general
- https://learn.microsoft.com/en-us/azure/foundry/openai/how-to/gpt-with-vision

Rift Coach isn't endorsed by Riot Games and doesn't reflect the views or opinions of Riot Games or anyone officially involved in producing or managing Riot Games properties. Riot Games, and all associated properties are trademarks or registered trademarks of Riot Games, Inc.