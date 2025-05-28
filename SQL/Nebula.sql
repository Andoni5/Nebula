-- 1. Clean slate: drop any table that might already exist in the public schema
DO $$ DECLARE r RECORD;
BEGIN
  FOR r IN (SELECT tablename FROM pg_tables WHERE schemaname = 'public') LOOP
    EXECUTE 'DROP TABLE IF EXISTS ' || quote_ident(r.tablename) || ' CASCADE';
  END LOOP;
END $$;

-- 2. Drop (and recreate) custom enum types
DROP TYPE IF EXISTS transaction_type;
DROP TYPE IF EXISTS rarity_type;

CREATE TYPE rarity_type AS ENUM ('common', 'rare', 'epic', 'legendary');

-- 3. Core users table (managed by Supabase Auth)
DROP TABLE IF EXISTS public.users CASCADE;
CREATE TABLE public.users (
  id               UUID            PRIMARY KEY,
  username         VARCHAR(50)     NOT NULL UNIQUE,
  email            VARCHAR(100)    NOT NULL UNIQUE,
  password_hash    VARCHAR(255),
  created_at       TIMESTAMP       NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at       TIMESTAMP       NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- 3a. Auth → public.users sync trigger  (NOW builds username + auto-creates stats + default skin in inventory)
CREATE OR REPLACE FUNCTION public.handle_new_user()
RETURNS trigger AS $$
DECLARE
  v_username TEXT;
BEGIN
  v_username := lower(
                  split_part(NEW.email, '@', 1) ||
                  substr(replace(NEW.id::text, '-', ''), 1, 8)
               );

  INSERT INTO public.users (id, email, username)
    VALUES (NEW.id, NEW.email, v_username);

  INSERT INTO public.player_stats (user_id, actual_skin)
    VALUES (NEW.id, 'default');

  INSERT INTO public.inventory (user_id, item_name)
    VALUES (NEW.id, 'default');

  RETURN NEW;
END;
$$ LANGUAGE plpgsql SECURITY DEFINER;

DROP TRIGGER IF EXISTS on_auth_user_created ON auth.users;
CREATE TRIGGER on_auth_user_created
AFTER INSERT ON auth.users
FOR EACH ROW EXECUTE FUNCTION public.handle_new_user();

-- 3b. RLS policies on users
ALTER TABLE public.users ENABLE ROW LEVEL SECURITY;
CREATE POLICY "read own rows"   ON public.users FOR SELECT  USING (id = auth.uid());
CREATE POLICY "update own rows" ON public.users FOR UPDATE USING (id = auth.uid());
CREATE POLICY "insert own data" ON public.users FOR INSERT  WITH CHECK (id = auth.uid());

-- 4. Cosmetic items catalogue (PK = name)
CREATE TABLE public.cosmetic_items (
  name            VARCHAR(100) PRIMARY KEY,
  description     TEXT,
  price_coins     INT             NOT NULL,
  rarity          rarity_type     NOT NULL DEFAULT 'common',
  created_at      TIMESTAMP       NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at      TIMESTAMP       NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- 4a. Seed “default” skin **before** RLS kicks in
INSERT INTO public.cosmetic_items (name, description, price_coins, rarity)
VALUES ('default', 'Default player appearance', 0, 'common')
ON CONFLICT (name) DO NOTHING;

ALTER TABLE public.cosmetic_items ENABLE ROW LEVEL SECURITY;
CREATE POLICY "read all"        ON public.cosmetic_items FOR SELECT  USING (true);
CREATE POLICY "admin update"    ON public.cosmetic_items FOR UPDATE USING (auth.role() = 'service_role');
CREATE POLICY "admin insert"    ON public.cosmetic_items FOR INSERT  WITH CHECK (auth.role() = 'service_role');

-- 5. Player statistics summary (updated_at + timestamp trigger already present)
CREATE TABLE public.player_stats (
  user_id                UUID     PRIMARY KEY,
  best_distance          INT      NOT NULL DEFAULT 0,
  best_coins_earned      INT      NOT NULL DEFAULT 0,
  total_sessions         INT      NOT NULL DEFAULT 0,
  total_distance         BIGINT   NOT NULL DEFAULT 0,
  total_coins_collected  BIGINT   NOT NULL DEFAULT 0,
  total_coins_spent      BIGINT   NOT NULL DEFAULT 0,
  challenges_completed   INT      NOT NULL DEFAULT 0,
  actual_skin            VARCHAR(100),
  created_at             TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at             TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
  CONSTRAINT fk_stats_user FOREIGN KEY (user_id)     REFERENCES public.users(id)            ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT fk_stats_skin FOREIGN KEY (actual_skin) REFERENCES public.cosmetic_items(name) ON DELETE SET NULL  ON UPDATE CASCADE
);
CREATE INDEX ix_stats_best_distance ON public.player_stats(best_distance);
CREATE INDEX ix_stats_best_coins    ON public.player_stats(best_coins_earned);
CREATE INDEX ix_stats_skin          ON public.player_stats(actual_skin);

-- Trigger function to auto-update updated_at
CREATE OR REPLACE FUNCTION public.update_player_stats_timestamp()
RETURNS trigger AS $$
BEGIN
  IF NEW IS DISTINCT FROM OLD THEN
    NEW.updated_at = CURRENT_TIMESTAMP;
  END IF;
  RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- Attach trigger to player_stats
DROP TRIGGER IF EXISTS trg_update_player_stats ON public.player_stats;
CREATE TRIGGER trg_update_player_stats
BEFORE UPDATE ON public.player_stats
FOR EACH ROW EXECUTE FUNCTION public.update_player_stats_timestamp();

ALTER TABLE public.player_stats ENABLE ROW LEVEL SECURITY;
CREATE POLICY "read own rows"   ON public.player_stats FOR SELECT  USING (user_id = auth.uid());
CREATE POLICY "update own rows" ON public.player_stats FOR UPDATE USING (user_id = auth.uid());
CREATE POLICY "insert own data" ON public.player_stats FOR INSERT  WITH CHECK (user_id = auth.uid());

-- 6. Inventory (unchanged)
CREATE TABLE public.inventory (
  user_id     UUID         NOT NULL,
  item_name   VARCHAR(100) NOT NULL,
  acquired_at TIMESTAMP    NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (user_id, item_name),
  CONSTRAINT fk_inventory_user FOREIGN KEY (user_id)   REFERENCES public.users(id)            ON DELETE CASCADE  ON UPDATE CASCADE,
  CONSTRAINT fk_inventory_item FOREIGN KEY (item_name) REFERENCES public.cosmetic_items(name) ON DELETE RESTRICT ON UPDATE CASCADE
);
CREATE INDEX ix_inventory_user       ON public.inventory(user_id);
CREATE INDEX ix_inventory_item_name  ON public.inventory(item_name);

ALTER TABLE public.inventory ENABLE ROW LEVEL SECURITY;
CREATE POLICY "read own rows"   ON public.inventory FOR SELECT  USING (user_id = auth.uid());
CREATE POLICY "update own rows" ON public.inventory FOR UPDATE USING (user_id = auth.uid());
CREATE POLICY "insert own data" ON public.inventory FOR INSERT  WITH CHECK (user_id = auth.uid());

-- 7.1. Creamos el ENUM para el tipo de challenge
CREATE TYPE public.challenge_type AS ENUM ('COINS', 'WALK');

-- 7.2. Daily challenges (modificado)
CREATE TABLE public.daily_challenges (
  id              SERIAL PRIMARY KEY,
  challenge_date  DATE               NOT NULL UNIQUE,
  description     TEXT               NOT NULL,
  amount_needed   INT                NOT NULL DEFAULT 0,
  challenge_type  public.challenge_type NOT NULL DEFAULT 'COINS',
  reward_coins    INT                NOT NULL DEFAULT 0
);

CREATE INDEX ix_challenges_date ON public.daily_challenges(challenge_date);

ALTER TABLE public.daily_challenges ENABLE ROW LEVEL SECURITY;
CREATE POLICY "read all"     ON public.daily_challenges FOR SELECT USING (true);
CREATE POLICY "admin update" ON public.daily_challenges FOR UPDATE USING (auth.role() = 'service_role');
CREATE POLICY "admin insert" ON public.daily_challenges FOR INSERT WITH CHECK (auth.role() = 'service_role');

-- 8. Completed challenges (unchanged)
CREATE TABLE public.completed_challenges (
  user_id        UUID NOT NULL,
  challenge_id   INT  NOT NULL,
  completed_at   TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
  reward_claimed BOOLEAN   NOT NULL DEFAULT FALSE,
  PRIMARY KEY (user_id, challenge_id),
  CONSTRAINT fk_completed_user      FOREIGN KEY (user_id)      REFERENCES public.users(id)             ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT fk_completed_challenge FOREIGN KEY (challenge_id) REFERENCES public.daily_challenges(id)  ON DELETE CASCADE ON UPDATE CASCADE
);
CREATE INDEX ix_completed_user      ON public.completed_challenges(user_id);
CREATE INDEX ix_completed_challenge ON public.completed_challenges(challenge_id);

ALTER TABLE public.completed_challenges ENABLE ROW LEVEL SECURITY;
CREATE POLICY "read own rows"   ON public.completed_challenges FOR SELECT  USING (user_id = auth.uid());
CREATE POLICY "update own rows" ON public.completed_challenges FOR UPDATE USING (user_id = auth.uid());
CREATE POLICY "insert own data" ON public.completed_challenges FOR INSERT  WITH CHECK (user_id = auth.uid());