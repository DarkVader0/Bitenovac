-- The schema table named by the variable "record" as a read_json columns argument, so a raw line
-- yields exactly the contract's columns and types whatever it holds: a field added later is
-- ignored, a missing one is null.
SELECT '{' || string_agg(format('''{}'': ''{}''', column_name, data_type), ', ' ORDER BY ordinal_position) || '}'
FROM information_schema.columns
WHERE table_name = 'schema_' || getvariable('record');
