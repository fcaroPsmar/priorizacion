create extension if not exists pgcrypto;

create table if not exists convocatoria (
  id uuid primary key default gen_random_uuid(),
  codigo text not null unique,
  nombre text not null,
  fecha_inicio timestamptz null,
  fecha_fin timestamptz null,
  activa boolean not null default true,
  acceso_desde timestamptz null,
  acceso_hasta timestamptz null,
  creado_en timestamptz not null default now()
);

create table if not exists aspirante (
  id uuid primary key default gen_random_uuid(),
  convocatoria_id uuid not null references convocatoria(id) on delete cascade,
  email text not null,
  nombre text null,
  dni_nie text null,
  num_empleat int null,
  dni_nie_emmascarat text null,
  primer_cognom text null,
  segon_cognom text null,
  nom text null,
  torn_y text null,
  experiencia numeric null,
  unnamed_9 numeric null,
  barem_personal numeric null,
  unnamed_11 numeric null,
  qualificacio numeric null,
  unnamed_13 numeric null,
  total numeric null,
  ficher_aspirant text null,
  pond_exp numeric null,
  pond_barem numeric null,
  prova_competencial numeric null,
  pond_prova numeric null,
  enviado_en timestamptz null,
  creado_en timestamptz not null default now(),
  unique (convocatoria_id, email)
);

alter table aspirante add column if not exists dni_nie text null;
alter table aspirante add column if not exists num_empleat int null;
alter table aspirante add column if not exists dni_nie_emmascarat text null;
alter table aspirante add column if not exists primer_cognom text null;
alter table aspirante add column if not exists segon_cognom text null;
alter table aspirante add column if not exists nom text null;
alter table aspirante add column if not exists torn_y text null;
alter table aspirante add column if not exists experiencia numeric null;
alter table aspirante add column if not exists unnamed_9 numeric null;
alter table aspirante add column if not exists barem_personal numeric null;
alter table aspirante add column if not exists unnamed_11 numeric null;
alter table aspirante add column if not exists qualificacio numeric null;
alter table aspirante add column if not exists unnamed_13 numeric null;
alter table aspirante add column if not exists total numeric null;
alter table aspirante add column if not exists ficher_aspirant text null;
alter table aspirante add column if not exists pond_exp numeric null;
alter table aspirante add column if not exists pond_barem numeric null;
alter table aspirante add column if not exists prova_competencial numeric null;
alter table aspirante add column if not exists pond_prova numeric null;

create table if not exists aspirante_token (
  id uuid primary key default gen_random_uuid(),
  aspirante_id uuid not null references aspirante(id) on delete cascade,
  codigo text not null unique,
  creado_en timestamptz not null default now(),
  expira_en timestamptz not null,
  revocado_en timestamptz null,
  ultimo_acceso_en timestamptz null,
  intentos_fallidos int not null default 0
);

create table if not exists plaza (
  id uuid primary key default gen_random_uuid(),
  convocatoria_id uuid not null references convocatoria(id) on delete cascade,
  base text not null,
  posicion text not null,
  centro text null,
  descripcion text null,
  activa boolean not null default true,
  creado_en timestamptz not null default now(),
  unique (convocatoria_id, base, posicion)
);

create table if not exists aspirante_plaza (
  id uuid primary key default gen_random_uuid(),
  aspirante_id uuid not null references aspirante(id) on delete cascade,
  plaza_id uuid not null references plaza(id) on delete cascade,
  orden_defecto int not null,
  orden_usuario int null,
  bloqueada boolean not null default false,
  creado_en timestamptz not null default now(),
  modificado_en timestamptz not null default now(),
  unique (aspirante_id, plaza_id)
);

create index if not exists ix_aspirante_plaza_orden_final
  on aspirante_plaza (aspirante_id, (coalesce(orden_usuario, orden_defecto)));

create or replace function set_modificado_en()
returns trigger as $$
begin
  new.modificado_en = now();
  return new;
end;
$$ language plpgsql;

drop trigger if exists trg_set_modificado_en on aspirante_plaza;
create trigger trg_set_modificado_en
before update on aspirante_plaza
for each row
execute function set_modificado_en();

create or replace view vw_priorizacion_lista as
select
  c.codigo as convocatoria,
  (p.base || '-' || p.posicion) as titulo,
  p.centro as centre,
  a.email as email,
  ap.id as id,
  coalesce(ap.orden_usuario, ap.orden_defecto) as orden,
  ap.orden_defecto,
  ap.orden_usuario,
  ap.bloqueada,
  a.enviado_en,
  ap.modificado_en
from aspirante_plaza ap
join aspirante a on a.id = ap.aspirante_id
join plaza p on p.id = ap.plaza_id
join convocatoria c on c.id = a.convocatoria_id;

create or replace view vw_tokens_validos as
select
  t.codigo,
  t.aspirante_id,
  a.convocatoria_id,
  t.expira_en,
  t.revocado_en,
  a.enviado_en,
  c.acceso_desde,
  c.acceso_hasta,
  c.activa
from aspirante_token t
join aspirante a on a.id = t.aspirante_id
join convocatoria c on c.id = a.convocatoria_id
where
  t.revocado_en is null
  and now() <= t.expira_en
  and (a.enviado_en is null)
  and (c.activa = true)
  and (c.acceso_desde is null or now() >= c.acceso_desde)
  and (c.acceso_hasta is null or now() <= c.acceso_hasta);
