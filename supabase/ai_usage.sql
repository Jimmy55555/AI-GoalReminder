-- AI 用量記帳：只有 generateContent 回 2xx 才算 1 次；countTokens 不計。
-- 在 Supabase SQL Editor 執行一次（或 supabase db query --remote）。

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
