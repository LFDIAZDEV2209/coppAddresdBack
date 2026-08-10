# Triggers — Registro

Reglas del proyecto (skill `database`): **no crear triggers salvo razón arquitectónica válida.**

## Checklist antes de usar un trigger

1. ¿Se resuelve en Application/Domain?
2. ¿Con evento de dominio (MediatR)?
3. ¿Con proceso background (job/SQS)?
4. ¿Efectos secundarios ocultos?
5. ¿Bloqueos potenciales (locks por UPDATE masivo)?
6. ¿Impacto en transacciones (el trigger corre dentro de la transacción del escritor)?
7. ¿Rendimiento (coste en cada escritura)?
8. ¿Debugging (comportamiento no visible en app)?
9. ¿Comportamiento inesperado con EF Core (filas modificadas fuera del tracking, valores no reflejados en entidades, `SaveChanges` sin efecto visible)?

Si pasa el checklist (justificación documentada y probada), registrar:

## Registro

| Trigger | Tabla | Evento | Qué hace | Por qué es necesario | Alternativas descartadas | Pruebas | Riesgos |
|---|---|---|---|---|---|---|---|
| — | — | — | — | — | — | — | — |

_(Vacío por esqueleto — es el estado esperado.)_
