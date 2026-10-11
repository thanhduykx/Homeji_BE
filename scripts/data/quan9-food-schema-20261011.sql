START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261011091052_AddFoodSourcePlaces') THEN
    CREATE TABLE homeji.food_source_places (
        id uuid NOT NULL,
        name character varying(200) NOT NULL,
        address character varying(500) NOT NULL,
        source_url character varying(1000) NOT NULL,
        maps_search_url character varying(1000) NOT NULL,
        verification_note character varying(1000) NOT NULL,
        collected_at timestamp with time zone NOT NULL,
        CONSTRAINT "PK_food_source_places" PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261011091052_AddFoodSourcePlaces') THEN
    CREATE UNIQUE INDEX "IX_food_source_places_name_address" ON homeji.food_source_places (name, address);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261011091052_AddFoodSourcePlaces') THEN
    ALTER TABLE homeji.food_source_places ENABLE ROW LEVEL SECURITY;
    REVOKE ALL ON homeji.food_source_places FROM PUBLIC;
    DO $roles$ BEGIN
        IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'anon') THEN
            REVOKE ALL ON homeji.food_source_places FROM anon;
        END IF;
        IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'authenticated') THEN
            REVOKE ALL ON homeji.food_source_places FROM authenticated;
        END IF;
    END $roles$;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261011091052_AddFoodSourcePlaces') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261011091052_AddFoodSourcePlaces', '9.0.17');
    END IF;
END $EF$;
COMMIT;
