alter table public.user 
add column if not exists isblocked boolean not nul default false;