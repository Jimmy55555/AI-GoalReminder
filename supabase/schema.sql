-- 在 Supabase SQL Editor 執行一次。資料表放在 public，但用 goal_reminder_ 前綴隔離。

create table if not exists public.goal_reminder_goals (
    id uuid primary key default gen_random_uuid(),
    owner_id uuid not null default auth.uid(),
    title text not null,
    description text not null default '',
    is_in_progress boolean not null default false,
    target_date date,
    skills jsonb not null default '[]'::jsonb,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now()
);

create table if not exists public.goal_reminder_progress (
    goal_id uuid primary key references public.goal_reminder_goals(id) on delete cascade,
    owner_id uuid not null default auth.uid(),
    is_in_progress boolean not null default false,
    updated_at timestamptz not null default now()
);

create table if not exists public.goal_reminder_email_schedule (
    owner_id uuid primary key default auth.uid(),
    enabled boolean not null default true,
    send_time text not null default '08:00',
    frequency text not null default 'Daily',
    weekly_day integer not null default 1,
    monthly_day integer not null default 1,
    recipient text not null default '',
    ai_enabled boolean not null default false,
    updated_at timestamptz not null default now()
);

create table if not exists public.goal_reminder_email_send_log (
    owner_id uuid not null,
    sent_on date not null,
    created_at timestamptz not null default now(),
    primary key (owner_id, sent_on)
);

alter table public.goal_reminder_goals enable row level security;
alter table public.goal_reminder_progress enable row level security;
alter table public.goal_reminder_email_schedule enable row level security;
alter table public.goal_reminder_email_send_log enable row level security;

drop policy if exists goal_reminder_goals_owner on public.goal_reminder_goals;
create policy goal_reminder_goals_owner
    on public.goal_reminder_goals for all to authenticated
    using (owner_id = auth.uid())
    with check (owner_id = auth.uid());

drop policy if exists goal_reminder_progress_owner on public.goal_reminder_progress;
create policy goal_reminder_progress_owner
    on public.goal_reminder_progress for all to authenticated
    using (owner_id = auth.uid())
    with check (owner_id = auth.uid());

drop policy if exists goal_reminder_email_schedule_owner on public.goal_reminder_email_schedule;
create policy goal_reminder_email_schedule_owner
    on public.goal_reminder_email_schedule for all to authenticated
    using (owner_id = auth.uid())
    with check (owner_id = auth.uid());

drop policy if exists goal_reminder_email_send_log_owner on public.goal_reminder_email_send_log;
create policy goal_reminder_email_send_log_owner
    on public.goal_reminder_email_send_log for all to authenticated
    using (owner_id = auth.uid())
    with check (owner_id = auth.uid());

grant usage on schema public to authenticated;
grant select, insert, update, delete on
    public.goal_reminder_goals,
    public.goal_reminder_progress,
    public.goal_reminder_email_schedule,
    public.goal_reminder_email_send_log
    to authenticated;

-- AI 用量記帳（只有 service role 可讀寫；一般使用者不能碰）
create table if not exists public.goal_reminder_ai_usage (
    quota_day date primary key,
    used integer not null default 0,
    quota_limit integer not null default 20,
    updated_at timestamptz not null default now()
);

alter table public.goal_reminder_ai_usage enable row level security;

grant usage on schema public to service_role;
grant select, insert, update on public.goal_reminder_ai_usage to service_role;

create or replace function public.goal_reminder_ai_usage_bump(p_day date)
returns void
language plpgsql
security definer
set search_path = public
as $$
begin
    insert into public.goal_reminder_ai_usage (quota_day, used, quota_limit)
    values (p_day, 1, 20)
    on conflict (quota_day) do update
        set used = public.goal_reminder_ai_usage.used + 1,
            updated_at = now();
end;
$$;

revoke all on function public.goal_reminder_ai_usage_bump(date) from public, anon, authenticated;
grant execute on function public.goal_reminder_ai_usage_bump(date) to service_role;
