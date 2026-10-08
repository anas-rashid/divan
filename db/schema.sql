-- Divan PostgreSQL schema. Ids and URLs come from divan-data (stable across syncs).
-- Search: poems.search_text holds Urdu-normalised text (see api/src/urdu.ts); the pg_trgm
-- index makes substring search (ILIKE '%word%') fast without a language dictionary.

CREATE EXTENSION IF NOT EXISTS pg_trgm;

CREATE TABLE IF NOT EXISTS poets (
    id            integer PRIMARY KEY,
    url           text NOT NULL UNIQUE,          -- '/p238'
    name          text NOT NULL,
    nickname      text NOT NULL,
    description   text,                          -- short intro (Urdu Wikipedia lead)
    birth_year_ah integer,                       -- Hijri, approximate
    death_year_ah integer,
    pin_order     integer                        -- featured on the home page (db/featured.sql)
);
ALTER TABLE poets ADD COLUMN IF NOT EXISTS pin_order integer;

CREATE TABLE IF NOT EXISTS categories (
    id        integer PRIMARY KEY,
    poet_id   integer NOT NULL REFERENCES poets(id) ON DELETE CASCADE,
    parent_id integer REFERENCES categories(id) ON DELETE CASCADE,  -- NULL = the poet's root
    url       text NOT NULL UNIQUE,              -- '/p238/shaeri/c641'
    title     text NOT NULL,
    position  integer NOT NULL DEFAULT 0
);
CREATE INDEX IF NOT EXISTS categories_parent ON categories(parent_id);
CREATE INDEX IF NOT EXISTS categories_poet ON categories(poet_id);

CREATE TABLE IF NOT EXISTS poems (
    id          integer PRIMARY KEY,
    category_id integer NOT NULL REFERENCES categories(id) ON DELETE CASCADE,
    poet_id     integer NOT NULL REFERENCES poets(id) ON DELETE CASCADE,
    url         text NOT NULL UNIQUE,            -- '/p238/shaeri/c641/sh6556'
    title       text NOT NULL,
    full_title  text NOT NULL,
    source_url  text,                            -- Wikisource page
    position    integer NOT NULL DEFAULT 0,
    search_text text NOT NULL DEFAULT ''         -- normalised title + verses
);
CREATE INDEX IF NOT EXISTS poems_category ON poems(category_id);
CREATE INDEX IF NOT EXISTS poems_search ON poems USING gin (search_text gin_trgm_ops);

CREATE TABLE IF NOT EXISTS verses (
    poem_id  integer NOT NULL REFERENCES poems(id) ON DELETE CASCADE,
    vorder   integer NOT NULL,
    position text NOT NULL,                      -- Right | Left | Single | Paragraph
    couplet  integer NOT NULL,
    text     text NOT NULL,
    PRIMARY KEY (poem_id, vorder)
);
