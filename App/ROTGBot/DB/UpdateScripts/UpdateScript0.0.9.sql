alter table public.user
add column if not exists number integer;

with maxnum as (select max(number) as n from public.user),
a as
(
	select maxnum.n + row_number() over(order by lastsenddate) as num, id from public.user, maxnum where number is null
)
update public.user u set number = a.num 
from a
where u.id = a.id;