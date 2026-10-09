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
    kind       text NOT NULL,                    -- poet | category | poem | couplet | phrase | word
    poet_id    integer,                          -- poet
    category_id integer,                         -- category: a book or chapter/section
    poem_id    integer,                          -- poem, couplet, phrase, or a word's source work
    couplet    integer,                          -- couplet, or a word's source couplet
    word       text,                             -- word
    phrase     text,                             -- phrase: the bookmarked part of a couplet or paragraph
    note       text,
    created_at timestamptz NOT NULL DEFAULT now()
);
ALTER TABLE library ADD COLUMN IF NOT EXISTS phrase text;
ALTER TABLE library DROP CONSTRAINT IF EXISTS library_kind_check;
ALTER TABLE library ADD COLUMN IF NOT EXISTS category_id integer;
ALTER TABLE library ADD CONSTRAINT library_kind_check CHECK (kind IN ('poet', 'category', 'poem', 'couplet', 'phrase', 'word'));
DROP INDEX IF EXISTS library_items;
DROP INDEX IF EXISTS library_places;
CREATE UNIQUE INDEX IF NOT EXISTS library_bookmarks ON library (user_id, kind, coalesce(poet_id, 0), coalesce(category_id, 0), coalesce(poem_id, 0), coalesce(couplet, -1)) WHERE kind IN ('poet', 'category', 'poem', 'couplet');
CREATE UNIQUE INDEX IF NOT EXISTS library_phrases ON library (user_id, poem_id, couplet, phrase) WHERE kind = 'phrase';
CREATE UNIQUE INDEX IF NOT EXISTS library_words ON library (user_id, word) WHERE kind = 'word';

-- content moderation (api/src/moderation.ts): versions of content, and every step taken on them (#31, #51)
CREATE TABLE IF NOT EXISTS revisions (
    id           bigserial PRIMARY KEY,
    entity       text NOT NULL,                  -- 'work' (later: poet, intro, book, chapter, dictionary, tag)
    entity_id    integer NOT NULL,               -- poems.id for works
    version      integer,                        -- the published version number (NULL until published)
    base_version integer NOT NULL DEFAULT 0,     -- the published version the draft started from (0 = Wikisource text)
    base_content text NOT NULL DEFAULT '',       -- the text the draft started from (for an exact diff)
    content      text NOT NULL,                  -- Divan text
    summary      text,                           -- the edit summary
    status       text NOT NULL CHECK (status IN ('draft', 'submitted', 'approved', 'published', 'returned', 'rejected')),
    author_id    bigint REFERENCES users(id) ON DELETE SET NULL,
    author_email text NOT NULL,                  -- kept when accounts are deleted
    reviewer_email  text,                        -- the L1 moderator who approved
    publisher_email text,
    created_at   timestamptz NOT NULL DEFAULT now(),
    updated_at   timestamptz NOT NULL DEFAULT now(),
    published_at timestamptz
);
ALTER TABLE revisions ADD COLUMN IF NOT EXISTS base_content text NOT NULL DEFAULT '';
ALTER TABLE revisions ADD COLUMN IF NOT EXISTS reviewer_id bigint REFERENCES users(id) ON DELETE SET NULL;
ALTER TABLE revisions ADD COLUMN IF NOT EXISTS commit text;     -- divan-data commit of a published version
ALTER TABLE revisions ADD COLUMN IF NOT EXISTS credits jsonb;   -- public names at publishing: by, reviewedBy, publishedBy
CREATE INDEX IF NOT EXISTS revisions_entity ON revisions(entity, entity_id);
CREATE INDEX IF NOT EXISTS revisions_status ON revisions(status);
CREATE UNIQUE INDEX IF NOT EXISTS revisions_version ON revisions(entity, entity_id, version) WHERE version IS NOT NULL;
CREATE TABLE IF NOT EXISTS revision_events (       -- who did what: created, saved, submitted, approved, returned,
    id          bigserial PRIMARY KEY,             -- rejected, published, commented
    revision_id bigint NOT NULL REFERENCES revisions(id) ON DELETE CASCADE,
    actor_id    bigint REFERENCES users(id) ON DELETE SET NULL,
    actor_email text NOT NULL,
    action      text NOT NULL,
    comment     text,
    at          timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS revision_events_revision ON revision_events(revision_id);
CREATE INDEX IF NOT EXISTS revision_events_at ON revision_events(at DESC);
-- home page sections (api/src/site.ts), edited by moderators with the 'site' permission. A section either lists
-- chosen poets (poets, in order) or every poet born in a Hijri century (century; 0 = year unknown). Sorting:
-- manual (the list's order; a century section lists its chosen poets first), alpha (Urdu alphabetical) or
-- timeline (by birth year). title NULL = the default name (the century's name).
CREATE TABLE IF NOT EXISTS home_sections (
    id       serial PRIMARY KEY,
    position integer NOT NULL,
    title    text,
    century  integer,
    sort     text NOT NULL DEFAULT 'manual' CHECK (sort IN ('manual', 'alpha', 'timeline')),
    poets    integer[] NOT NULL DEFAULT '{}'
);
-- tags (#52, api/src/tags.ts): typed labels on books/chapters (a poet's root category = the poet), works and
-- couplets (couplet > 0). The published record is divan-data/divan/<url>.tags; the import reloads it.
CREATE TABLE IF NOT EXISTS tags (
    id   serial PRIMARY KEY,
    type text NOT NULL,                          -- موضوع، صنف، بحر، شخصیت، مقام، دور، ٹیگ (free)
    name text NOT NULL,
    UNIQUE (type, name)
);
CREATE TABLE IF NOT EXISTS entity_tags (
    tag_id    integer NOT NULL REFERENCES tags(id) ON DELETE CASCADE,
    entity    text NOT NULL CHECK (entity IN ('category', 'work')),
    entity_id integer NOT NULL,                  -- categories.id / poems.id
    couplet   integer NOT NULL DEFAULT 0,        -- a work's couplet; 0 = the whole work or category
    PRIMARY KEY (tag_id, entity, entity_id, couplet)
);
CREATE INDEX IF NOT EXISTS entity_tags_target ON entity_tags(entity, entity_id);
