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
ALTER TABLE poets ADD COLUMN IF NOT EXISTS birth_year_ce integer;  -- Gregorian (عیسوی)
ALTER TABLE poets ADD COLUMN IF NOT EXISTS death_year_ce integer;

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
-- ghazal marks (divan-data extension): radif ('' = rhyme only), matla/maqta couplet numbers, and the
-- radif letter that groups a divan's contents ("ردیف الف … ی"; NULL outside radif-ordered lists)
ALTER TABLE poems ADD COLUMN IF NOT EXISTS radif text;
ALTER TABLE poems ADD COLUMN IF NOT EXISTS matla integer;
ALTER TABLE poems ADD COLUMN IF NOT EXISTS maqta integer;
ALTER TABLE poems ADD COLUMN IF NOT EXISTS radif_letter text;
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

-- Wiktionary for the word sidebar (api/src/dictionary.ts; filled and kept current by dict-sync.ts)
DROP TABLE IF EXISTS dictionary;                 -- the earlier live-lookup cache
CREATE TABLE IF NOT EXISTS wiktionary (
    id     bigserial PRIMARY KEY,
    lang   text NOT NULL,                        -- the word's language: ur | fa | ar
    source text NOT NULL,                        -- en (en.wiktionary, via kaikki.org) | own (that language's Wiktionary)
    title  text NOT NULL,                        -- headword / page title
    key    text NOT NULL,                        -- spelling-insensitive lookup key (dictionary.ts key())
    data   jsonb NOT NULL
);
CREATE INDEX IF NOT EXISTS wiktionary_key ON wiktionary(key);
CREATE INDEX IF NOT EXISTS wiktionary_title ON wiktionary(lang, source, title);
CREATE INDEX IF NOT EXISTS wiktionary_key_trgm ON wiktionary USING gin (key gin_trgm_ops);  -- similar words (regex, %)
CREATE TABLE IF NOT EXISTS ur_glosses (          -- English gloss -> Urdu word, for the pivot through English
    gloss text NOT NULL,
    word  text NOT NULL
);
ALTER TABLE ur_glosses ADD COLUMN IF NOT EXISTS source text NOT NULL DEFAULT 'en';  -- en | own (ur.wiktionary English entries)
CREATE INDEX IF NOT EXISTS ur_glosses_gloss ON ur_glosses(gloss);
CREATE TABLE IF NOT EXISTS dict_meta (           -- upstream file versions and recent-changes timestamps
    name  text PRIMARY KEY,
    value text NOT NULL
);

-- Accounts (api/src/auth.ts): email address and password only; no email is sent
CREATE TABLE IF NOT EXISTS users (
    id            bigserial PRIMARY KEY,
    email         text NOT NULL UNIQUE,          -- stored lowercased
    password_hash text NOT NULL,                 -- scrypt$N$r$p$salt$hash
    created_at    timestamptz NOT NULL DEFAULT now()
);
CREATE TABLE IF NOT EXISTS sessions (
    id         text PRIMARY KEY,                 -- SHA-256 of the cookie token (the token itself is never stored)
    user_id    bigint NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    created_at timestamptz NOT NULL DEFAULT now(),
    expires_at timestamptz NOT NULL
);
CREATE INDEX IF NOT EXISTS sessions_user ON sessions(user_id);
-- admin panel (api/src/admin.ts): roles, disabled accounts, audit log of admin actions
ALTER TABLE users ADD COLUMN IF NOT EXISTS role text NOT NULL DEFAULT 'reader';   -- reader | admin (moderators: #29)
ALTER TABLE users ADD COLUMN IF NOT EXISTS disabled_at timestamptz;               -- set: cannot sign in
CREATE TABLE IF NOT EXISTS audit_log (
    id          bigserial PRIMARY KEY,
    at          timestamptz NOT NULL DEFAULT now(),
    actor_id    bigint REFERENCES users(id) ON DELETE SET NULL,
    actor_email text NOT NULL,                   -- kept when the actor's account is deleted
    action      text NOT NULL,                   -- password-reset | disable | enable | role | delete | promote
    target_id   bigint,                          -- the user acted on (no FK: deleted users stay in the log)
    target_email text,
    detail      jsonb
);
CREATE INDEX IF NOT EXISTS audit_log_at ON audit_log(at DESC);
-- moderators' permissions (api/src/permissions.ts); users.role: reader | mod-l2 | mod-l1 | admin
CREATE TABLE IF NOT EXISTS grants (
    id         bigserial PRIMARY KEY,
    user_id    bigint NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    scope      text NOT NULL CHECK (scope IN ('all', 'poet', 'category', 'poem')),
    scope_id   integer,                          -- poets.id / categories.id / poems.id; NULL for 'all'
    content    text[] NOT NULL,                  -- poets, books, works, dictionary
    actions    text[] NOT NULL,                  -- create, edit, delete, arrange
    granted_by bigint REFERENCES users(id) ON DELETE SET NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    CHECK ((scope = 'all') = (scope_id IS NULL))
);
CREATE INDEX IF NOT EXISTS grants_user ON grants(user_id);
-- profile (owner request): full name and bio, usually in Urdu
ALTER TABLE users ADD COLUMN IF NOT EXISTS full_name text;   -- up to 100 characters
ALTER TABLE users ADD COLUMN IF NOT EXISTS bio text;         -- up to 1,000 characters
-- personal library (api/src/library.ts): saved poets and works, bookmarked couplets and phrases, saved words
CREATE TABLE IF NOT EXISTS library (
    id         bigserial PRIMARY KEY,
    user_id    bigint NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    kind       text NOT NULL,                    -- poet | poem | couplet | phrase | word
    poet_id    integer,                          -- poet
    poem_id    integer,                          -- poem, couplet, phrase, or a word's source work
    couplet    integer,                          -- couplet, or a word's source couplet
    word       text,                             -- word
    phrase     text,                             -- phrase: the bookmarked part of a couplet or paragraph
    note       text,
    created_at timestamptz NOT NULL DEFAULT now()
);
ALTER TABLE library ADD COLUMN IF NOT EXISTS phrase text;
ALTER TABLE library DROP CONSTRAINT IF EXISTS library_kind_check;
ALTER TABLE library ADD CONSTRAINT library_kind_check CHECK (kind IN ('poet', 'poem', 'couplet', 'phrase', 'word'));
DROP INDEX IF EXISTS library_items;
CREATE UNIQUE INDEX IF NOT EXISTS library_places ON library (user_id, kind, coalesce(poet_id, 0), coalesce(poem_id, 0), coalesce(couplet, -1)) WHERE kind IN ('poet', 'poem', 'couplet');
CREATE UNIQUE INDEX IF NOT EXISTS library_phrases ON library (user_id, poem_id, couplet, phrase) WHERE kind = 'phrase';
CREATE UNIQUE INDEX IF NOT EXISTS library_words ON library (user_id, word) WHERE kind = 'word';
