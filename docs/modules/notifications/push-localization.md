# Idioma de notificaciones push y propiedad del dispositivo

Cada fila de `app.device_tokens` tiene un campo `language` (`es` o `en`). Aplica
la migración `20261106142633_AddDeviceTokenLanguage` antes de dirigir tráfico a
la nueva API. La columna añadida usa `es` por defecto para los registros existentes
y los nuevos registros de clientes anteriores; una actualización del mismo usuario
que omite el idioma conserva su preferencia. Los valores explícitos se recortan
y convierten a minúsculas; otros valores se rechazan (422). La base de datos
también exige uno de los dos valores admitidos.

La app del paciente envía su idioma actual al registrar el dispositivo y actualiza
el token existente cuando cambia el idioma o la cuenta, usando el JWT vigente.
Esta actualización no solicita permisos, añade escuchas ni vuelve a registrar el
dispositivo con el complemento nativo. Las solicitudes se serializan para impedir
que una actualización antigua de idioma prevalezca por llegar fuera de orden.
Al cerrar sesión se capturan el JWT y el token anteriores antes de borrar la
autenticación, y se encola un DELETE limitado al propietario. Cada solicitud HTTP
tiene un plazo de cancelación de tres segundos. Un fallo de red no permite retirar
una notificación del sistema ya entregada o encolada.

El registro usa un bloqueo transaccional consultivo de PostgreSQL derivado del
token opaco (que no se registra en los logs), elimina sus vínculos con otros
usuarios y actualiza o inserta el registro del usuario actual. Los registros
simultáneos de un mismo token físico terminan con un solo propietario; los tokens
diferentes se conservan como dispositivos independientes. La baja usa el mismo
bloqueo y solo elimina la fila del usuario autenticado, por lo que una limpieza
tardía de la cuenta anterior no puede eliminar el registro de una cuenta nueva.
Retira del tráfico las instancias antiguas de la API durante el despliegue: su
código de registro no participa en este protocolo de bloqueo.

Cuatro emisores de FCM resuelven el texto por dispositivo: el flujo genérico de
administración y controles del programa, telemedicina, gamificación y SOS. Todas
las variantes de idioma necesarias se preparan antes de la primera llamada a FCM.
Se conservan las filas canónicas de notificaciones y auditoría, los datos de
navegación, las prioridades, el tratamiento de tokens inválidos, la deduplicación
y la inyección única en el chat. La asignación del programa usa su aviso bilingüe
ya almacenado; el idioma del chat sigue la configuración existente y es
independiente del idioma de las notificaciones del dispositivo.

SOS, todas las plantillas actuales de XP y gamificación, y los mensajes incluidos
de controles del programa tienen texto inglés revisado disponible localmente.
Las transacciones de SOS y XP nunca esperan un modelo. Los textos configurados o
personalizados de los flujos genéricos y de telemedicina usan el servicio compartido
`IContentTranslationService` una vez por lote de fuentes necesario. Una traducción
no disponible o incompleta no puede provocar un envío silencioso en español a un
token configurado en inglés. Se mantiene el comportamiento de errores y reintentos
del emisor. Las nuevas plantillas de gamificación deben incorporar su texto
revisado antes de poder enviarse a dispositivos configurados en inglés.

El envío masivo, el envío de prueba y la vista previa de tests de salud prefieren
las plantillas redactadas en inglés. Si falta el inglés, se genera a partir de la
plantilla redactada sin renderizar y de las etiquetas de tests e indicadores que
esta utiliza, conservando los códigos de indicadores. Antes de interpolar se
comprueba el conjunto de marcadores y sus repeticiones. No se envían nombres de
pacientes, documentos, teléfonos ni destinatarios al traductor de plantillas.
Un envío en inglés sin plantilla ni mensaje redactado de regla falla explícitamente,
en lugar de traducir una alerta del paciente ya renderizada. Se registran el idioma
y el cuerpo efectivamente enviados; las plantillas fuente se conservan.

La validación usa sustitutos de FCM, SMS y la entrega de mensajes de comunidad.
Las pruebas de migración y propiedad en PostgreSQL crean y eliminan bases
temporales con nombres únicos; nunca migran la base indicada en la conexión de
entrada. La secuencia Up/Down/Up conserva los tokens existentes y exige los valores
de idioma admitidos. Eliminar la columna de idioma al revertir la migración
descarta sus preferencias; al reaplicarla se usa español por defecto. Estas pruebas
no acreditan entregas reales de FCM ni diálogos nativos de notificaciones.
