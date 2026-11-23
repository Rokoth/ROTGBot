alter table public.user
add column if not exists isblocked boolean not null default false;

alter table public.user
add column if not exists number int;

update public.user u
set number = s.num 
from(

	select row_number()  over(order by lastsenddate)  as num,
	id
	from  public.user  where number is null

) s
where u.id = s.id and u.number is null;

