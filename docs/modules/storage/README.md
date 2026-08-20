# Módulo: Almacenamiento de Objetos (Storage)

Capa de abstracción de almacenamiento de objetos (semántica S3) agnóstica del proveedor. Dos implementaciones concretas: **Local (sistema de archivos)** para desarrollo y **AWS S3** para producción, seleccionadas por `Storage:Provider`.

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
│  ├── S3ObjectStorageService      (implementación AWS S3)     │
│  ├── S3StorageOptions            (sección "Storage:S3")      │
│  └── DependencyInjection.AddObjectStorage (switch de provider)│
└─────────────────────────────────────────────────────────────┘
```

El flujo de dependencias respeta la Clean Architecture: `Application` solo define el contrato y los DTOs (sin tipos de AWS), `Infrastructure` provee la implementación registrada por DI según configuración.

## Contrato de la interfaz

| Método | Descripción | Comportamiento local | Comportamiento S3 |
|--------|-------------|----------------------|-------------------|
| `IsCloudStorage` | ¿El proveedor emite URLs firmadas reales? | `false` | `true` |
| `PutObjectAsync` | Almacena contenido bajo una clave | Escribe el archivo (sobrescribe), devuelve la ruta | `PutObjectAsync` al bucket |
| `GetObjectAsync` | Obtiene el contenido | Abre el archivo; `FileNotFoundException` si no existe | `GetObjectAsync`; 404 → `FileNotFoundException` |
| `HeadObjectAsync` | Obtiene metadatos | Metadatos o `null` | `GetObjectMetadataAsync`; 404 → `null` |
| `ListObjectsAsync` | Lista por prefijo | Recorre la raíz recursivamente, token de continuación | `ListObjectsV2Async` (paginación real por token) |
| `DeleteObjectAsync` | Elimina un objeto | No-op si no existe | `DeleteObjectAsync` (idempotente) |
| `DeleteObjectsAsync` | Elimina en lote | Elimina cada uno; tolera inexistentes | `DeleteObjectsAsync` en lotes de 1000 (límite S3) |
| `CopyObjectAsync` | Copia origen → destino | Copia y sobrescribe el destino | `CopyObjectAsync` intra-bucket; 404 → `FileNotFoundException` |
| `GetPreSignedUrlAsync` | URL firmada de lectura temporal | Devuelve la ruta absoluta del archivo (no hay URL real) | Presigned GET URL del bucket (SigV4) |
| `GetPreSignedUploadUrlAsync` | URL firmada de escritura (PUT) | URL del proxy del backend (`{publicBaseUrl}/api/v1/storage/{key}`) | Presigned PUT URL del bucket (SigV4, con Content-Type firmado) |

**Claves**: usan separador `/` (convención S3) y se interpretan como rutas relativas a la raíz/bucket. `clientes/123/documento.pdf` → `{RootPath}/clientes/123/documento.pdf` o `s3://{bucket}/clientes/123/documento.pdf`.

**Multipart upload**: deliberadamente fuera del contrato — es un detalle de implementación que la TransferUtility del SDK AWS maneja internamente.

## Decisiones de diseño

| Decisión | Razón |
|----------|-------|
| **Interfaz en Application, sin tipos AWS** | El dominio/Application no debe conocer al proveedor; el contrato es estable y testeable. |
| **Clave = ruta relativa** | El bucket/raíz es la frontera; los flujos no cambian al cambiar de proveedor. |
| **URLs firmadas en el proveedor** | Con S3 el navegador sube/descarga directo al bucket (`upload-intent` devuelve un presigned URL real); con Local el proxy del backend cubre el mismo contrato. El front no distingue. |
| **ContentType derivado de extensión** | Mapa MIME mínimo con fallback a `application/octet-stream`; el content type definitivo lo registra la metadata del documento (Media/Documents). |
| **Singleton** | Ambos proveedores son stateless-safe; el `AmazonS3Client` es thread-safe y está diseñado para reutilizarse. |
| **Credenciales por cadena por defecto del SDK** | IAM role de la instancia/tarea en producción; `AWS_PROFILE` o variables en local. Jamás Access Keys en código o configuración. |

## Configuración

Los `appsettings*.json` están gitignoreados; los defaults se aplican si falta la sección. Cada clave de la sección `Storage:S3` cae a la variable de entorno AWS estándar correspondiente si no está configurada (misma imagen para local y EC2/ECS).

```json
{
  "Storage": {
    "Provider": "S3",
    "SignatureKey": "clave-para-el-proxy-local-cambiar-en-prod",
    "Local": {
      "RootPath": "C:\\data\\coppaddresd-storage"
    },
    "S3": {
      "Region": "us-east-2",
      "Bucket": "cooppadresd-storage-prod",
      "BucketArn": "arn:aws:s3:::cooppadresd-storage-prod",
      "ObjectArn": "arn:aws:s3:::cooppadresd-storage-prod/*",
      "IamRole": "cooppadresd-ec2-s3-access-role"
    }
  }
}
```

| Clave | Default | Descripción |
|-------|---------|-------------|
| `Storage:Provider` | `Local` | Proveedor activo: `Local` o `S3` (valor distinto lanza `InvalidOperationException` en el arranque de DI). |
| `Storage:Local:RootPath` | `%TEMP%/coppaddresd-storage` | Directorio raíz donde se persisten los objetos. |
| `Storage:S3:Region` | env `AWS_REGION` | Región del bucket. Obligatoria con provider S3. |
| `Storage:S3:Bucket` | env `AWS_S3_BUCKET` | Nombre del bucket. Obligatoria con provider S3 (falta → error en la construcción del servicio). |
| `Storage:S3:BucketArn` | env `AWS_S3_BUCKET_ARN` | ARN del bucket (documentación/políticas IAM). |
| `Storage:S3:ObjectArn` | env `AWS_S3_OBJECT_ARN` | ARN de los objetos (políticas IAM). |
| `Storage:S3:IamRole` | env `AWS_S3_IAM_ROLE` | Nombre del role IAM con acceso al bucket (referencia; el SDK lo asume automáticamente en EC2/ECS). |
| `Storage:S3:ServiceUrl` | env `AWS_S3_ENDPOINT` | Endpoint compatible (LocalStack/MinIO) para desarrollo. Vacío en producción. |

## Seguridad

- **Guardia de path traversal (provider Local, hard requirement)**: cada clave se normaliza y se verifica que la ruta resuelta quede dentro de la raíz. Claves como `../escape.txt` lanzan `ArgumentException` y jamás escriben fuera del directorio raíz.
- **Nombres de dispositivo reservados (solo Windows)**: claves con segmentos finales `CON`, `NUL`, `PRN`, `AUX`, `COM1`–`COM9`, `LPT1`–`LPT9`, o con `:` (ADS), se rechazan con `ArgumentException` antes de cualquier E/S.
- **Reparse points (provider Local)**: toda operación que toca el filesystem rechaza rutas que atraviesen un reparse point bajo la raíz; `ListObjectsAsync` no enumera a través de ellos.
- **PutObject atómico (provider Local)**: escritura a temporal + rename sobre el destino (semántica atómica de S3).
- **S3**: los presigned URLs están limitados a la clave específica y expiran (PUT de subida 15 min; GET de descarga 15 min); el bucket no es público (el acceso pasa por SigV4 firmado). El IAM role solo otorga acceso a este bucket (`cooppadresd-ec2-s3-access-role`).
- Las claves vacías/nulas se rechazan en la resolución de ruta.

## AWS S3 (implementado)

El provider S3 (`AWSSDK.S3` v4) se registra cuando `Storage:Provider=S3`:

1. El SDK resuelve credenciales por la cadena por defecto (IAM role EC2/ECS, perfil AWS CLI, variables `AWS_ACCESS_KEY_ID`/`AWS_SECRET_ACCESS_KEY` para desarrollo). Nunca se configuran Access Keys en el repo.
2. Los endpoints de subida (`POST /api/v1/media/upload-intent`, `POST /api/v1/documents/upload-intent`) devuelven un **presigned PUT URL real del bucket**: el navegador sube directo a S3 (el `Authorization: Bearer` solo se adjunta cuando la URL apunta al backend; un header de autorización rompería la firma SigV4).
3. Las descargas (`GET /api/v1/storage/sign`, `GET /api/v1/documents/{id}/download`) devuelven un **presigned GET URL** del bucket.
4. El proxy `PUT/GET /api/v1/storage/{key}` sigue funcionando para flujos servidor-a-servidor (p. ej. ingest de documentos de agentes): el backend escribe/lee en S3 con su rol.
5. Para desarrollo local contra un bucket de prueba se puede apuntar `Storage:S3:ServiceUrl` a LocalStack/MinIO.

**Sin cambios en Application ni en el código de negocio**: el contrato no cambia; solo se ramifica en los controladores con `IsCloudStorage` para elegir la URL firmada correcta.

## Testing

```bash
dotnet test tests/CoppAddresd.UnitTests --filter "FullyQualifiedName~LocalObjectStorageService"
```

Cobertura local: put (crear + sobrescribir + fallo a mitad de copia conservando la versión previa), get (contenido + key inexistente), head (existe + no existe), list (prefijo + token + sin seguimiento de reparse points), delete (individual + lote con claves ausentes), copy (sobrescritura de destino existente + origen inexistente), pre-signed URL (ruta absoluta), guardia de path traversal (keys maliciosas), rechazo de nombres de dispositivo reservados/ADS (Windows) y rechazo de operaciones a través de reparse points. Cada prueba usa un directorio temporal fresco descartado al finalizar.

El provider S3 no tiene tests unitarios de red: se valida en integración real contra el bucket (los contratos de error —404 → `FileNotFoundException`/`null`— se mantienen idénticos al proveedor local).