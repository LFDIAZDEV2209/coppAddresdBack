# Validación Fase 1 — organización de Bienestar

Rama `org/f1-coppAddresdBack`, base `a9fd51f`. Sin integrar `origin/dev`.

## 1.1 — Confirmación del pisado

La hipótesis 5 de `DIAGNOSTICO.md` §7 pasa a hecho: ambas activaciones públicas
(por fecha y por semana perfecta) reemplazan la preedición de una semana bloqueada.
La escritura real del ERP guarda un jueves de 77 puntos; al activar queda en 10.
Dos pruebas PostgreSQL pasan afirmando este defecto, con migraciones en BD efímeras.
Se habilita la corrección 1.2.

Validación: `dotnet build -c Release` sin errores; integración `ActivacionPor`: 2/2.
Debug bloqueado por servicios locales en ejecución; Release evita detenerlos.

## 1.2 — Conservación al activar

Las dos regresiones fallaron con 1.1 (2 fallos) y pasan tras conservar snapshots
no vacíos en ambas activaciones. Las semanas vacías mantienen la copia de plantilla.
La combinación por origen y actualización del resto de días corresponde a 1.5.
Validación: build Release y pruebas de activación PostgreSQL.
