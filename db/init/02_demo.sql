
-- =========================================================
-- SEED DEMO AZURE POSTGRES
-- 1 convocatoria
-- 3 aspirantes
-- 200 plazas
-- =========================================================

-- =========================
-- IDS FIJOS
-- =========================
-- Convocatoria
-- aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa

-- Aspirantes
-- bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb
-- cccccccc-cccc-cccc-cccc-cccccccccccc
-- dddddddd-dddd-dddd-dddd-dddddddddddd

-- =========================
-- CONVOCATORIA
-- =========================
insert into convocatoria (
  id, codigo, nombre, activa,
  acceso_desde, acceso_hasta
)
values (
  'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
  'DEMO-2026',
  'Convocatoria DEMO 2026 – Stress 200 plazas',
  true,
  now() - interval '1 day',
  now() + interval '15 days'
)
on conflict (codigo) do nothing;

-- =========================
-- ASPIRANTES (3)
-- =========================
insert into aspirante (id, convocatoria_id, email, nombre)
values
  ('bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 'demo1@hospitalmar.cat', 'Aspirante Demo 1'),
  ('cccccccc-cccc-cccc-cccc-cccccccccccc', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 'demo2@hospitalmar.cat', 'Aspirante Demo 2'),
  ('dddddddd-dddd-dddd-dddd-dddddddddddd', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 'demo3@hospitalmar.cat', 'Aspirante Demo 3')
on conflict (convocatoria_id, email) do nothing;

-- =========================
-- TOKENS
-- =========================
insert into aspirante_token (
  id, aspirante_id, codigo, expira_en
)
values
  ('11111111-0000-0000-0000-000000000001', 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', 'TOKEN-DEMO-1', now() + interval '15 days'),
  ('11111111-0000-0000-0000-000000000002', 'cccccccc-cccc-cccc-cccc-cccccccccccc', 'TOKEN-DEMO-2', now() + interval '15 days'),
  ('11111111-0000-0000-0000-000000000003', 'dddddddd-dddd-dddd-dddd-dddddddddddd', 'TOKEN-DEMO-3', now() + interval '15 days')
on conflict (codigo) do nothing;

-- =========================
-- PLAZAS (200)
-- =========================
insert into plaza (
  id,
  convocatoria_id,
  base,
  posicion,
  centro,
  descripcion,
  activa
)
select
  ('eeeeeeee-0000-0000-0000-' || lpad(i::text, 12, '0'))::uuid,
  'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
  'A1',
  'P' || lpad(i::text, 3, '0'),
  'Hospital del Mar',
  'Plaza demo número ' || i,
  true
from generate_series(1, 200) as i
on conflict (convocatoria_id, base, posicion) do nothing;

-- =========================
-- ASPIRANTE_PLAZA (600 filas)
-- =========================
insert into aspirante_plaza (
  id,
  aspirante_id,
  plaza_id,
  orden_defecto,
  orden_usuario,
  bloqueada
)
select
  ('ffffffff-0000-0000-0000-' || lpad(row_number() over()::text, 12, '0'))::uuid,
  a.id,
  p.id,
  row_number() over (partition by a.id order by p.posicion),
  null,
  false
from aspirante a
join plaza p on p.convocatoria_id = a.convocatoria_id
where a.convocatoria_id = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'
on conflict (aspirante_id, plaza_id) do nothing;
