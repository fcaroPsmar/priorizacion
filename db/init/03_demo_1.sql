-- Datos demo (opcional). Se ejecuta la primera vez que se crea el volumen de Postgres.

insert into convocatoria (codigo, nombre, activa, acceso_desde, acceso_hasta)
values ('DEMO-2026', 'Convocatoria demo 2026', true, now() - interval '1 day', now() + interval '7 days')
on conflict (codigo) do nothing;

with c as (
  select id from convocatoria where codigo = 'DEMO-2026'
)
insert into aspirante (convocatoria_id, email, nombre)
select c.id, 'demo@hospitalmar.cat', 'Aspirante Demo'
from c
on conflict (convocatoria_id, email) do nothing;

with a as (
  select id from aspirante where email = 'demo@hospitalmar.cat'
)
insert into aspirante_token (aspirante_id, codigo, expira_en)
select a.id, 'DEMO1234', now() + interval '30 days'
from a
on conflict (codigo) do nothing;

-- Plazas
with c as (select id from convocatoria where codigo = 'DEMO-2026')
insert into plaza (convocatoria_id, base, posicion, centro)
select c.id, 'DEMO-BASE', '1', 'Centre A' from c on conflict do nothing;
with c as (select id from convocatoria where codigo = 'DEMO-2026')
insert into plaza (convocatoria_id, base, posicion, centro)
select c.id, 'DEMO-BASE', '2', 'Centre B' from c on conflict do nothing;
with c as (select id from convocatoria where codigo = 'DEMO-2026')
insert into plaza (convocatoria_id, base, posicion, centro)
select c.id, 'DEMO-BASE', '3', 'Centre C' from c on conflict do nothing;
with c as (select id from convocatoria where codigo = 'DEMO-2026')
insert into plaza (convocatoria_id, base, posicion, centro)
select c.id, 'DEMO-BASE', '4', 'Centre D' from c on conflict do nothing;
with c as (select id from convocatoria where codigo = 'DEMO-2026')
insert into plaza (convocatoria_id, base, posicion, centro)
select c.id, 'DEMO-BASE', '5', 'Centre E' from c on conflict do nothing;

-- Asignación + orden por defecto
with a as (select id from aspirante where email='demo@hospitalmar.cat'),
     p as (select id, row_number() over (order by posicion::int) as rn from plaza where base='DEMO-BASE')
insert into aspirante_plaza (aspirante_id, plaza_id, orden_defecto)
select a.id, p.id, p.rn
from a cross join p
on conflict (aspirante_id, plaza_id) do nothing;