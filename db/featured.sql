-- Poets pinned to the top of the home page ("مقبول شعرا"), in this order.
-- Applied by api/src/import.ts after every import. Ids/URLs are stable divan-data ids.
UPDATE poets SET pin_order = NULL;
UPDATE poets SET pin_order = v.ord FROM (VALUES
    ('/p238', 1),  -- محمد اقبال
    ('/p266', 2),  -- مرزا غالب
    ('/p303', 3),  -- میر تقی میر
    ('/p99',  4),  -- خواجہ میر درد
    ('/p102', 5)   -- داغ دہلوی
) AS v(url, ord) WHERE poets.url = v.url;
