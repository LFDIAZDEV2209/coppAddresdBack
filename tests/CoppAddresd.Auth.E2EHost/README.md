# Activación por SMS sin correo

Este host verifica la app con Auth, Identity y PostgreSQL reales. Sustituye solo
el cliente de red de Twilio. No envía SMS, correo ni llamadas reales.

Requiere una base local cuyo nombre empiece por `copp_review_`, con los esquemas
de Auth y API migrados. Los cuatro pacientes de prueba usan los documentos
`QA-OTP-NO-EMAIL`, `QA-OTP-EXPIRED`, `QA-OTP-WRONG`, `QA-OTP-BAD-EMAIL`, el país
`1` y los números ficticios `2025550101` a `2025550104`. Todos carecen de correo
salvo el último, que usa `not-an-email` para comprobar que la validación sigue
activa. La aplicación `app` debe existir y estar activa en Auth.

La configuración se suministra por variables de entorno o archivo privado,
nunca en Git: `ConnectionStrings__DefaultConnection`, `Jwt__Secret`,
`Jwt__Issuer`, `SkipDatabaseInitialization=true`, `COP_E2E_OUTPUT` (bajo `/tmp`)
y las opciones `Twilio__*` con valores exclusivamente sintéticos de prueba.

```sh
dotnet run --project tests/CoppAddresd.Auth.E2EHost
```

El host solo escucha en `127.0.0.1:5125`. Con la app en Vite (`5173`), desde la
raíz del backend:

```sh
COP_E2E_OUTPUT=/tmp/mi-evidencia \
COP_E2E_DATABASE=copp_review_mi_base \
COP_E2E_AUTH_URL=http://127.0.0.1:5125 \
node tests/CoppAddresd.Auth.E2EHost/activation.mjs
```

El script necesita Playwright instalado en el repositorio hermano de la app
(`COP_E2E_APP_DIR` permite otra ubicación) y el contenedor PostgreSQL local
`copp-qa-postgres-20261013` en el contexto Docker `default`. Reinicia únicamente
los usuarios sintéticos vinculados a los cuatro documentos dentro de esa base.
Los códigos `123456`, `000000` y `999999` simulan aprobación, verificación
consumida/vencida y rechazo. Nunca se aceptan en la integración de producción.
