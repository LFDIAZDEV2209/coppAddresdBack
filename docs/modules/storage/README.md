# Módulo: Almacenamiento de Objetos (Storage)

Capa de abstracción de almacenamiento de objetos (semántica S3) agnóstica del proveedor. Hoy la implementación concreta es **local (sistema de archivos)**, con un punto de extensión claro para AWS S3.

## Arquitectura

```
┌─────────────────────────────────────────────────────────────┐
│                        Application                            │
│  Interfaces                                                  │
│  └── IObjectStorageService        (contrato S3-like)         │
│  DTOs/Storage                                                │
│  ├── ObjectMetadata                                          │
│  └── ListObjectsResult                                       │
├─────────────────────────────────────────────────────────────┤
│                        Infrastructure                         │
│  ├── LocalObjectStorageService   (implementación local)      │
│  ├── LocalStorageOptions         (sección "Storage:Local")   │
│  └── DependencyInjection.AddObjectStorage (switch de provider)│
└─────────────────────────────────────────────────────────────┘
```

El flujo de dependencias respeta la Clean Architecture: `Application` solo define el contrato y los DTOs (sin tipos de AWS), `Infrastructure` provee la implementación registrada por DI según configuración.

## Contrato de la interfaz

| Método | Descripción | Comportamiento local |
|--------|-------------|----------------------|
| `PutObjectAsync` | Almacena contenido bajo una clave | Crea directorios, escribe el archivo (sobrescribe), devuelve la ruta absoluta |
| `GetObjectAsync` | Obtiene el contenido | Abre el archivo; `FileNotFoundException` si la clave no existe |
| `HeadObjectAsync` | Obtiene metadatos | Metadatos si existe, `null` si no |
| `ListObjectsAsync` | Lista por prefijo | Recorre la raíz recursivamente, filtra por prefijo, ordena por clave, soporta token de continuación |
| `DeleteObjectAsync` | Elimina un objeto | No-op si no existe |
| `DeleteObjectsAsync` | Elimina en lote | Elimina cada uno; tolera claves inexistentes |
| `CopyObjectAsync` | Copia origen → destino | Copia y sobrescribe el destino; `FileNotFoundException` si el origen no existe |
| `GetPreSignedUrlAsync` | URL firmada de acceso temporal | **Devuelve la ruta absoluta del archivo** (no hay URL real en local) |

**Claves**: usan separador `/` (convención S3) y se interpretan como rutas relativas a la raíz. `clientes/123/documento.pdf` → `{RootPath}/clientes/123/documento.pdf`.

**Multipart upload**: deliberadamente fuera del contrato — es un detalle de implementación que la TransferUtility del SDK AWS manejará internamente.

## Decisiones de diseño

| Decisión | Razón |
|----------|-------|
| **Interfaz en Application, sin tipos AWS** | El dominio/Application no debe conocer al proveedor; el contrato es estable y testeable. |
| **Clave = ruta relativa** | Reutiliza el filesystem sin tabla de metadatos; la raíz es el "bucket". |
| **Continuación por cursor** | Token = última clave devuelta; las claves se ordenan ordinalmente para paginación determinista. |
| **ETag = hash del last-write-time** | Estable por versión, cambia al sobrescribir, barato de calcular. |
| **ContentType derivado de extensión** | El filesystem no persiste el contentType recibido; mapa MIME mínimo con fallback a `application/octet-stream`. |
| **Singleton** | `LocalObjectStorageService` es stateless-safe (raíz inmutable, operaciones por llamada); `Directory.CreateDirectory` es idempotente y thread-safe. |

## Configuración

La configuración vive en código (los `appsettings*.json` están gitignoreados; los defaults se aplican si falta la sección).

```json
{
  "Storage": {
    "Provider": "Local",
    "Local": {
      "RootPath": "C:\\data\\coppaddresd-storage"
    }
  }
}
```

| Clave | Default | Descripción |
|-------|---------|-------------|
| `Storage:Provider` | `Local` | Proveedor activo. Hoy solo `Local`; `S3` lanza `InvalidOperationException` (pendiente de credenciales AWS). |
| `Storage:Local:RootPath` | `%TEMP%/coppaddresd-storage` | Directorio raíz donde se persisten los objetos. |

## Seguridad

- **Guardia de path traversal (hard requirement)**: cada clave se normaliza y se verifica que la ruta resuelta quede dentro de la raíz. Claves como `../escape.txt` lanzan `ArgumentException` y jamás escriben fuera del directorio raíz.
- **Nombres de dispositivo reservados (solo Windows)**: en NTFS, segmentos finales como `CON`, `NUL`, `PRN`, `AUX`, `COM1`–`COM9`, `LPT1`–`LPT9` (con o sin extensión) resuelven al namespace DOS del dispositivo y NO a un archivo de la raíz (`PutObjectAsync("NUL", ...)` no persiste nada). Estas claves, y cualquier clave con `:` (flujo de datos alternativo, p. ej. `archivo.txt:stream`), se rechazan con `ArgumentException` antes de cualquier E/S. En Linux esos nombres son archivos legítimos y no se rechazan.
- **Reparse points (symlinks/junctions)**: `Path.GetFullPath` es léxico pero las operaciones de archivo siguen symlinks; un junction bajo la raíz permitiría leer/escribir/borrar fuera del directorio configurado. Toda operación que toca el filesystem rechaza con `InvalidOperationException` cualquier ruta que atraviese un reparse point por debajo de la raíz, y `ListObjectsAsync` no enumera a través de ellos.
- **PutObject atómico**: la escritura va primero a un archivo temporal en el mismo directorio y luego se mueve (rename) sobre el destino. Si la copia del contenido falla a mitad de camino, la versión previa del objeto queda intacta y el temporal se elimina (semántica atómica de S3).
- Las claves vacías/nulas se rechazan en la resolución de ruta.
- El guard es independiente del provider: cualquier implementación futura (S3) debe mantener el contrato de claves relativas.

## Migrar a AWS S3 (futuro)

1. Agregar paquete `AWSSDK.S3` a `CoppAddresd.Infrastructure`.
2. Crear `S3ObjectStorageService : IObjectStorageService` en `Infrastructure/Services` usando el cliente S3 (los keys relativos se traducen a keys de bucket; `GetPreSignedUrlAsync` sí devuelve una URL real firmada con `GetPreSignedURLRequest`).
3. En `DependencyInjection.AddObjectStorage`, registrar el nuevo provider (punto de extensión ya marcado con comentario).
4. Cambiar `Storage:Provider` a `"S3"` y configurar credenciales (AWS SDK resolverá perfil/roles por el mecanismo estándar de cadena de credenciales).

**Sin cambios en Application ni en el código de negocio**: el contrato no cambia.

## Testing

```bash
dotnet test tests/CoppAddresd.UnitTests --filter "FullyQualifiedName~LocalObjectStorageService"
```

Cobertura: put (crear + sobrescribir + fallo a mitad de copia conservando la versión previa), get (contenido + key inexistente), head (existe + no existe), list (prefijo + token + sin seguimiento de reparse points), delete (individual + lote con claves ausentes), copy (sobrescritura de destino existente + origen inexistente + self-copy documentado), pre-signed URL (ruta absoluta), guardia de path traversal (keys maliciosas + verificación de que nunca se escribe fuera de la raíz), rechazo de nombres de dispositivo reservados/ADS (Windows) y rechazo de operaciones a través de reparse points. Cada prueba usa un directorio temporal fresco descartado al finalizar.
